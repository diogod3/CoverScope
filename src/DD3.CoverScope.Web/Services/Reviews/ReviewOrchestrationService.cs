using DD3.CoverScope.Brokers;
using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

public sealed class ReviewOrchestrationService(
    GitComparisonService git,
    VerificationService verification,
    CodeComparisonService code,
    ReviewStore store,
    IHostApplicationLifetime lifetime,
    ILogger<ReviewOrchestrationService> logger)
{
    private readonly object sync = new();
    private readonly Dictionary<string, ActiveRun> active = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    public event Action? Changed;

    public ReviewRecord? ActiveRecord(string repository)
    { lock (sync) { return active.GetValueOrDefault(repository)?.Record; } }
    public string Phase(string repository)
    { lock (sync) { return active.GetValueOrDefault(repository)?.Phase ?? ""; } }

    public async Task<ReviewRecord> StartAsync(ReviewRequest request)
    {
        var repository = await git.InspectAsync(request.TargetPath);
        await git.AssertCleanAsync(repository.Root, CancellationToken.None);
        var comparison = await git.ResolveAsync(repository, request, CancellationToken.None);
        var record = new ReviewRecord(Guid.NewGuid().ToString("N"), repository.Root, comparison.TargetPath, comparison.TargetRef, DateTimeOffset.UtcNow)
        { Commit = comparison.Commit, Baseline = comparison.Baseline, TargetCommit = comparison.TargetCommit };
        var lease = store.Acquire(repository.Root);
        var run = new ActiveRun(record, lease, CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping));
        try
        {
            var prior = await store.HistoryAsync(repository.Root);
            if (prior.Any(x => x.Status is ReviewStatus.Running or ReviewStatus.Preparing or ReviewStatus.Finalising or ReviewStatus.Cancelling or ReviewStatus.CancellationIncomplete))
            { throw new InvalidOperationException("An earlier run did not stop cleanly. Open history and resolve that interrupted run before starting another."); }
            lock (sync)
            {
                if (!active.TryAdd(repository.Root, run)) { throw new InvalidOperationException("A run is already active for this worktree."); }
            }
            await store.SaveSettingsAsync(repository.Root, request.Settings);
            await store.SaveRecordAsync(record);
            run.Execution = ObserveExecutionAsync(run, comparison, request.Settings);
            Notify();
            return record;
        }
        catch
        {
            lock (sync) { if (active.GetValueOrDefault(repository.Root) == run) { active.Remove(repository.Root); } }
            run.Dispose();
            throw;
        }
    }

    public async Task CancelAsync(string repository)
    {
        ActiveRun? run;
        lock (sync)
        {
            run = active.GetValueOrDefault(repository);
            if (run is not null && run.Record.Status is not (ReviewStatus.Finished or ReviewStatus.Failed or ReviewStatus.Cancelled))
            { run.Cancellation.Cancel(); }
        }
        if (run is null) { return; }
        await run.Transition.WaitAsync();
        try
        {
            if (run.Record.Status is ReviewStatus.Finished or ReviewStatus.Failed or ReviewStatus.Cancelled) { return; }
            run.Cancellation.Cancel();
            run.Record = run.Record with { Status = ReviewStatus.Cancelling };
            run.Phase = "Stopping work and discarding analysis";
            await store.SaveRecordAsync(run.Record);
        }
        finally { run.Transition.Release(); Notify(); }
        await run.Execution;
    }

    // Recovery requires an explicit acknowledgement after the reviewer has checked external processes.
    public async Task RecoverAsync(ReviewRecord record)
    {
        lock (sync)
        {
            if (active.ContainsKey(record.Repository)) { throw new InvalidOperationException("This application still owns the run. Cancel it before recovery."); }
        }
        using var lease = store.Acquire(record.Repository);
        var current = (await store.ReadAsync(record)).Record;
        if (current.Status is not (ReviewStatus.Running or ReviewStatus.Preparing or ReviewStatus.Finalising or ReviewStatus.Cancelling or ReviewStatus.CancellationIncomplete)) { return; }
        store.DiscardAnalysis(current);
        await store.SaveRecordAsync(current with { Status = ReviewStatus.Interrupted, EndedAt = DateTimeOffset.UtcNow,
            OperationalError = "Interrupted attempt. External work was checked by the reviewer and analysis discarded." });
        Notify();
    }

    public async Task<int> CleanupHistoryAsync(string repository)
    {
        // The same cross-process lease used by collection prevents deleting live inputs.
        using var lease = store.Acquire(repository);
        var history = await store.HistoryAsync(repository);
        var failures = 0;
        foreach (var record in history.Where(x => x.Status is ReviewStatus.Finished or ReviewStatus.Failed or ReviewStatus.Cancelled or ReviewStatus.Interrupted))
        {
            var cleaned = await CleanupAsync(record);
            await store.SaveRecordAsync(cleaned);
            if (cleaned.CleanupError is not null) { failures++; }
        }
        Notify();
        return failures;
    }

    private async Task<ReviewRecord> CleanupAsync(ReviewRecord record)
    {
        try
        {
            await Task.Run(() => store.CleanupTemporaryFiles(record));
            return record with { CleanupError = null };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Temporary files for review {RunId} could not be fully removed.", record.Id);
            return record with { CleanupError = "Temporary files remain. Use Clean temporary files in Runs to retry. " + ex.Message };
        }
    }

    private async Task ObserveExecutionAsync(ActiveRun run, ComparisonContext comparison, ReviewSettings settings)
    {
        try { await ExecuteAsync(run, comparison, settings); }
        catch (Exception ex)
        {
            // Retain the lease and visible record when final persistence itself fails.
            logger.LogError(ex, "Review {RunId} could not be finalised.", run.Record.Id);
            run.Record = run.Record with { Status = ReviewStatus.CancellationIncomplete,
                OperationalError = "Run finalisation could not be confirmed: " + ex.Message };
            run.Phase = "Finalisation requires recovery";
            try { await store.SaveRecordAsync(run.Record); }
            catch (Exception persistenceError) { logger.LogError(persistenceError, "Could not persist the unresolved review record."); }
            Notify();
        }
    }

    private async Task ExecuteAsync(ActiveRun run, ComparisonContext comparison, ReviewSettings settings)
    {
        var token = run.Cancellation.Token;
        var evidence = new ReviewEvidence { Comparison = comparison, Settings = settings };
        var directory = store.AnalysisDirectory(run.Record);
        using var observation = new SourceObservation(comparison.Repository);
        var stopped = true;
        var terminalRecorded = false;
        try
        {
            run.Record = run.Record with { Status = ReviewStatus.Running };
            await store.SaveRecordAsync(run.Record, token);
            SetPhase(run, "Reading committed changes");
            evidence.GitVersion = (await git.VersionAsync(comparison.Repository, token)).Trim();
            evidence.Files = await git.ChangesAsync(comparison, token);
            await CheckSourceAsync(comparison, token);
            if (settings.Tests)
            {
                SetPhase(run, "Building and running tests with coverage");
                await verification.TestsAsync(evidence, directory, Notify, token, currentToken => CheckSourceAsync(comparison, currentToken));
                SetPhase(run, "Capturing committed coverage source");
                await git.CaptureCoverageSourcesAsync(evidence, token);
            }
            await CheckSourceAsync(comparison, token);
            if (settings.Formatting)
            {
                SetPhase(run, "Analysing formatting");
                await verification.FormattingAsync(evidence, directory, Notify, token);
            }
            await CheckSourceAsync(comparison, token);
            evidence.Code = await code.AnalyseAsync(comparison, settings, directory, phase => SetPhase(run, phase), token);
            await CheckSourceAsync(comparison, token);
            evidence.SourceMatches = !observation.SourceTouched(await git.TrackedPathsAsync(comparison.Repository, comparison.Commit, token));
            evidence.SourceConsistency = evidence.SourceMatches ? "No source changes detected; external and ignored inputs are not isolated." : "Source changes or monitoring failure detected during collection. Source overlays are disabled.";
            await run.Transition.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                run.Record = run.Record with { Status = ReviewStatus.Finalising };
                SetPhase(run, "Saving review report");
                await store.SaveEvidenceAsync(run.Record, evidence, token);
                token.ThrowIfCancellationRequested();
                SetPhase(run, "Cleaning temporary files");
                run.Record = await CleanupAsync(run.Record);
                token.ThrowIfCancellationRequested();
                var finished = run.Record with { Status = ReviewStatus.Finished, EndedAt = DateTimeOffset.UtcNow };
                await store.SaveRecordAsync(finished, CancellationToken.None);
                lock (sync)
                {
                    token.ThrowIfCancellationRequested();
                    run.Record = finished;
                    terminalRecorded = true;
                }
            }
            finally { run.Transition.Release(); }
        }
        catch (ProcessStoppingException ex)
        {
            stopped = false;
            run.Cancellation.Cancel();
            run.Record = run.Record with { Status = ReviewStatus.CancellationIncomplete, OperationalError = ex.Message };
            await store.SaveRecordAsync(run.Record);
        }
        catch (OperationCanceledException)
        { await DiscardAsync(run); terminalRecorded = true; }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested) { await DiscardAsync(run); terminalRecorded = true; }
            else
            {
                evidence.SourceMatches = false;
                evidence.SourceConsistency = "Collection incomplete: " + ex.Message;
                run.Record = run.Record with { Status = ReviewStatus.Failed, EndedAt = DateTimeOffset.UtcNow, OperationalError = ex.Message };
                await store.SaveEvidenceAsync(run.Record, evidence, CancellationToken.None);
                run.Record = await CleanupAsync(run.Record);
                await store.SaveRecordAsync(run.Record);
                terminalRecorded = true;
            }
        }
        finally
        {
            run.Phase = run.Record.Status.ToString();
            if (stopped && terminalRecorded)
            {
                lock (sync) { active.Remove(run.Record.Repository); }
                run.Dispose();
            }
            Notify();
        }
    }

    private async Task DiscardAsync(ActiveRun run)
    {
        await run.Transition.WaitAsync();
        run.Record = run.Record with { Status = ReviewStatus.Cancelling };
        SetPhase(run, "Discarding all analysis");
        try
        {
            await store.SaveRecordAsync(run.Record);
            store.DiscardAnalysis(run.Record);
            run.Record = run.Record with { Status = ReviewStatus.Cancelled, EndedAt = DateTimeOffset.UtcNow, OperationalError = null };
            await store.SaveRecordAsync(run.Record);
        }
        catch (Exception ex)
        {
            run.Record = run.Record with { Status = ReviewStatus.CancellationIncomplete, OperationalError = "Analysis cleanup or finalisation failed: " + ex.Message };
            await store.SaveRecordAsync(run.Record);
        }
        finally { run.Transition.Release(); }
    }

    private async Task CheckSourceAsync(ComparisonContext comparison, CancellationToken token)
    {
        await git.AssertCleanAsync(comparison.Repository, token);
        var current = await git.InspectAsync(comparison.TargetPath, token);
        if (current.Commit != comparison.Commit) { throw new InvalidOperationException("The checked-out commit changed during collection."); }
    }

    private void SetPhase(ActiveRun run, string phase) { run.Phase = phase; Notify(); }
    private void Notify()
    {
        foreach (var subscriber in Changed?.GetInvocationList() ?? [])
        {
            try { ((Action)subscriber)(); }
            catch (Exception ex) { logger.LogDebug(ex, "A review subscriber disconnected."); }
        }
    }

    private sealed class ActiveRun(ReviewRecord record, IDisposable lease, CancellationTokenSource cancellation) : IDisposable
    {
        public ReviewRecord Record = record;
        public string Phase = "Preparing";
        public CancellationTokenSource Cancellation = cancellation;
        public SemaphoreSlim Transition { get; } = new(1, 1);
        public Task Execution { get; set; } = Task.CompletedTask;
        public void Dispose() { lease.Dispose(); /* Transition/token may still be observed by a waiting cancellation request. */ }
    }
}
