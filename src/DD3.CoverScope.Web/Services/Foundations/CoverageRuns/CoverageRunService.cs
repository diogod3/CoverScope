using CoverageRunSettings = DD3.CoverScope.Models.CoverageRunSettings;
using DD3.CoverScope.Models.Exceptions;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.Identifiers;
using DD3.CoverScope.Brokers.DateTimes;
using DD3.CoverScope.Brokers.Serializations;
using System.Reflection;
using System.Text.Json;
using DD3.CoverScope.Models;
using DD3.CoverScope.Models.CoverageTargets;

namespace DD3.CoverScope.Services.Foundations.CoverageRuns;

public partial class CoverageRunService : ICoverageRunService
{
    public const int SupportedSchemaVersion = 2;
    private const int MaximumAllocationAttempts = 16;
    private readonly IFileSystemBroker fileSystemBroker;
    private readonly ISerializationBroker serializationBroker;
    private readonly IIdentifierBroker identifierBroker;
    private readonly IDateTimeBroker dateTimeBroker;
    private readonly IDiagnosticsBroker diagnosticsBroker;

    public CoverageRunService(IFileSystemBroker fileSystemBroker, ISerializationBroker serializationBroker,
        IIdentifierBroker identifierBroker, IDateTimeBroker dateTimeBroker, IDiagnosticsBroker diagnosticsBroker)
    {
        this.fileSystemBroker = fileSystemBroker;
        this.serializationBroker = serializationBroker;
        this.identifierBroker = identifierBroker;
        this.dateTimeBroker = dateTimeBroker;
        this.diagnosticsBroker = diagnosticsBroker;
    }

    public Task<CoverageRunContext> BeginAsync(CoverageTarget target, CoverageRunSettings settings, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () => await BeginAsyncCore(target, settings, cancellationToken))).AsTask();

    public Task<CoverageRun> CompleteAsync(CoverageRunContext context, CoverageRunStatus status, IReadOnlyList<CoverageRunArtifact> artifacts, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () => await CompleteAsyncCore(context, status, artifacts, cancellationToken))).AsTask();

    public Task<CoverageRun> ReadAsync(string manifestPath, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () => await ReadAsyncCore(manifestPath, cancellationToken))).AsTask();

    public Task<CoverageRun?> ReadForArtifactAsync(string artifactPath, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () => await ReadForArtifactAsyncCore(artifactPath, cancellationToken))).AsTask();

    public Task<CoverageRunContext> ChangeStatusAsync(CoverageRunContext context, CoverageRunStatus status,
        CancellationToken cancellationToken = default) => Trace(() => TryCatch(async () =>
    {
        var previous = context.Manifest.Status;
        if (!((previous == CoverageRunStatus.Created && status == CoverageRunStatus.Running)
            || (previous is CoverageRunStatus.Created or CoverageRunStatus.Running && status == CoverageRunStatus.CancellationRequested)))
            throw new ArgumentException($"Invalid run transition from {previous} to {status}.");
        var manifest = context.Manifest with { Status = status };
        await WriteAsync(context.DirectoryPath, manifest, cancellationToken);
        return context with { Manifest = manifest };
    })).AsTask();

    private async Task<CoverageRunContext> BeginAsyncCore(
        CoverageTarget target,
        CoverageRunSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = dateTimeBroker.GetCurrentDateTime().ToUniversalTime();
        var fullTargetPath = target.Path;
        if (string.IsNullOrWhiteSpace(fullTargetPath) || !Path.IsPathFullyQualified(fullTargetPath))
            throw new ArgumentException("A resolved target with an absolute path is required.", nameof(target));

        var targetRoot = ResolveTargetRoot(fullTargetPath);
        var reportsRoot = Path.Combine(targetRoot, ".coverscope", "reports");
        try
        {
            fileSystemBroker.CreateDirectory(reportsRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"CoverScope cannot write reports to '{reportsRoot}'. Check the directory permissions and try again.", ex);
        }

        Guid? runId = null;
        string? runDirectory = null;
        for (var attempt = 0; attempt < MaximumAllocationAttempts; attempt++)
        {
            var candidateId = identifierBroker.CreateIdentifier(startedAt);
            var candidateDirectory = Path.Combine(reportsRoot, candidateId.ToString("D"));
            var reservationPath = Path.Combine(reportsRoot, $".{candidateId}.reserve");
            var reservationAcquired = false;

            try
            {
                fileSystemBroker.CreateNewFile(reservationPath);
                reservationAcquired = true;

                if (fileSystemBroker.DirectoryExists(candidateDirectory))
                    continue;

                fileSystemBroker.CreateDirectory(candidateDirectory);
                runId = candidateId;
                runDirectory = candidateDirectory;
                break;
            }
            catch (IOException) when (!reservationAcquired && fileSystemBroker.FileExists(reservationPath))
            {
                continue;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"CoverScope cannot create the run directory '{candidateDirectory}'. Check the directory permissions and try again.", ex);
            }
            finally
            {
                if (reservationAcquired && fileSystemBroker.FileExists(reservationPath))
                {
                    try { fileSystemBroker.DeleteFile(reservationPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        if (runId is null || runDirectory is null)
            throw new IOException($"CoverScope could not allocate a unique run directory under '{reportsRoot}'.");

        var manifest = new CoverageRun(
            SupportedSchemaVersion,
            runId.Value,
            new CoverageRunTarget(
                target.Name,
                target.Type,
                Path.GetRelativePath(targetRoot, fullTargetPath)),
            settings,
            startedAt,
            null,
            CoverageRunStatus.Created,
            new CoverageRunProducer("CoverScope", GetProducerVersion()),
            []);

        try
        {
            using var finalization = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await WriteAsync(runDirectory, manifest, finalization.Token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"CoverScope cannot write the run manifest in '{runDirectory}'. Check the directory permissions and try again.", ex);
        }
        return new(runDirectory, manifest, fullTargetPath);
    }

    private async Task<CoverageRun> CompleteAsyncCore(
        CoverageRunContext context,
        CoverageRunStatus status,
        IReadOnlyList<CoverageRunArtifact> artifacts,
        CancellationToken cancellationToken = default)
    {
        if (status is CoverageRunStatus.Created or CoverageRunStatus.Running or CoverageRunStatus.CancellationRequested)
            throw new ArgumentException("A completed run cannot retain the in-progress status.", nameof(status));

        ArgumentNullException.ThrowIfNull(context);
        if (context.Manifest.CompletedAt is not null)
            throw new ArgumentException("A terminal run cannot be completed again.", nameof(context));
        ValidateArtifacts(context.DirectoryPath, artifacts, requireExisting: true);
        var manifest = context.Manifest with
        {
            CompletedAt = dateTimeBroker.GetCurrentDateTime().ToUniversalTime(),
            Status = status,
            Artifacts = artifacts.ToArray()
        };
        await WriteAsync(context.DirectoryPath, manifest, cancellationToken);
        return manifest;
    }

    private async Task<CoverageRun> ReadAsyncCore(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        var fullManifestPath = Path.GetFullPath(manifestPath);
        CoverageRun? manifest;
        try
        {
            var json = await fileSystemBroker.ReadAllTextAsync(fullManifestPath, cancellationToken);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("schemaVersion", out var schema))
                throw new InvalidDataException("The run manifest is missing its schema version.");
            if (schema.GetInt32() != SupportedSchemaVersion)
                throw new NotSupportedException($"CoverScope run schema version {schema.GetInt32()} is not supported; expected version {SupportedSchemaVersion}.");
            manifest = serializationBroker.Deserialize<CoverageRun>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The CoverScope run manifest '{fullManifestPath}' is malformed or incomplete.", ex);
        }

        if (manifest is null)
            throw new InvalidDataException($"The CoverScope run manifest '{fullManifestPath}' is empty.");
        if (manifest.SchemaVersion != SupportedSchemaVersion)
            throw new NotSupportedException(
                $"CoverScope run schema version {manifest.SchemaVersion} is not supported; expected version {SupportedSchemaVersion}.");
        if (manifest.Id == Guid.Empty || manifest.Id.Version != 7 || manifest.Settings is null || manifest.Target is null || manifest.Producer is null || manifest.Artifacts is null)
            throw new InvalidDataException($"The CoverScope run manifest '{fullManifestPath}' is missing required fields.");

        ValidateRun(manifest);
        ValidateRelativePath(manifest.Target.RelativePath, "target");
        ValidateArtifacts(Path.GetDirectoryName(fullManifestPath)!, manifest.Artifacts);
        return manifest;
    }

    private async Task<CoverageRun?> ReadForArtifactAsyncCore(
        string artifactPath,
        CancellationToken cancellationToken = default)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(artifactPath))!);
        while (directory.Parent is not null)
        {
            if (directory.Parent.Name.Equals("reports", StringComparison.OrdinalIgnoreCase)
                && directory.Parent.Parent?.Name.Equals(".coverscope", StringComparison.OrdinalIgnoreCase) == true)
            {
                var manifestPath = Path.Combine(directory.FullName, "run.json");
                if (!fileSystemBroker.FileExists(manifestPath)) return null;
                try { return await ReadAsync(manifestPath, cancellationToken); }
                catch (Models.Exceptions.CoverageOperationValidationException) { return null; }
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static string ResolveTargetRoot(string targetPath) =>
        Path.GetDirectoryName(Path.GetFullPath(targetPath))
        ?? throw new ArgumentException("The target path does not have a containing directory.", nameof(targetPath));

    public static CoverageRunStatus ToStatus(CoverageRunOutcome outcome, bool hasCoverage) => outcome switch
    {
        CoverageRunOutcome.Succeeded when hasCoverage => CoverageRunStatus.Succeeded,
        CoverageRunOutcome.TestsFailed when hasCoverage => CoverageRunStatus.TestsFailed,
        CoverageRunOutcome.Cancelled => CoverageRunStatus.Cancelled,
        _ => CoverageRunStatus.ExecutionFailed
    };

    private async Task WriteAsync(string runDirectory, CoverageRun manifest, CancellationToken cancellationToken)
    {
        ValidateRun(manifest);
        ValidateArtifacts(runDirectory, manifest.Artifacts);
        await fileSystemBroker.WriteAllTextAsync(
            Path.Combine(runDirectory, "run.json"), serializationBroker.Serialize(manifest), cancellationToken);
    }

    private static string GetProducerVersion()
    {
        var version = typeof(CoverageRunService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return string.IsNullOrWhiteSpace(version) ? "unknown" : version.Split('+', 2)[0];
    }

}
