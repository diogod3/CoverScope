using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DD3.CoverScope.Brokers;
using DD3.CoverScope.Models.Reviews;

namespace DD3.CoverScope.Services.Reviews;

public sealed class ReviewStore(IFileSystemBroker files, string? rootPath = null)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string root = rootPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoverScope", "reviews-v2");

    public string RepositoryDirectory(string repository)
    {
        var identity = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repository));
        if (OperatingSystem.IsWindows()) { identity = identity.ToUpperInvariant(); }
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        return Path.Combine(root, key);
    }

    public string RunDirectory(ReviewRecord record)
    {
        if (!Guid.TryParseExact(record.Id, "N", out _)) { throw new InvalidDataException("Invalid run identity."); }
        return Path.Combine(RepositoryDirectory(record.Repository), record.Id);
    }

    public string AnalysisDirectory(ReviewRecord record) => Path.Combine(RunDirectory(record), "analysis");

    public IDisposable Acquire(string repository)
    {
        var directory = RepositoryDirectory(repository);
        files.CreateDirectory(directory);
        try { return files.Lease(Path.Combine(directory, "active.lock")); }
        catch (IOException ex) { throw new InvalidOperationException("Another CoverScope process owns a run for this worktree.", ex); }
    }

    public async Task SaveRecordAsync(ReviewRecord record, CancellationToken token = default)
    {
        var directory = RunDirectory(record);
        files.CreateDirectory(directory);
        await WriteAtomicAsync(Path.Combine(directory, "record.json"), record, token);
    }

    public Task SaveEvidenceAsync(ReviewRecord record, ReviewEvidence evidence, CancellationToken token)
    {
        var directory = AnalysisDirectory(record);
        files.CreateDirectory(directory);
        return WriteAtomicAsync(Path.Combine(directory, "evidence.json"), evidence, token);
    }

    public void DiscardAnalysis(ReviewRecord record)
    {
        var directory = AnalysisDirectory(record);
        if (files.DirectoryExists(directory)) { files.DeleteDirectory(directory); }
        if (files.DirectoryExists(directory)) { throw new IOException("Run analysis could not be discarded."); }
    }

    public async Task<ReviewSnapshot> ReadAsync(ReviewRecord requested, CancellationToken token = default)
    {
        var record = JsonSerializer.Deserialize<ReviewRecord>(await files.ReadAsync(Path.Combine(RunDirectory(requested), "record.json"), token), Json)
            ?? throw new InvalidDataException("Missing run record.");
        if (record.SchemaVersion != 2 || record.Repository != requested.Repository || record.Id != requested.Id)
        { throw new InvalidDataException("The run record does not match this repository or schema."); }
        var path = Path.Combine(AnalysisDirectory(record), "evidence.json");
        var readable = record.Status is ReviewStatus.Finished or ReviewStatus.Failed or ReviewStatus.Interrupted;
        var evidence = readable && files.FileExists(path)
            ? JsonSerializer.Deserialize<ReviewEvidence>(await files.ReadAsync(path, token), Json) : null;
        return new(record, evidence);
    }

    public async Task<IReadOnlyList<ReviewRecord>> HistoryAsync(string repository, CancellationToken token = default)
    {
        var directory = RepositoryDirectory(repository);
        if (!files.DirectoryExists(directory)) { return []; }
        var records = new List<ReviewRecord>();
        foreach (var path in files.Files(directory, "record.json", SearchOption.AllDirectories))
        {
            try
            {
                var record = JsonSerializer.Deserialize<ReviewRecord>(await files.ReadAsync(path, token), Json);
                if (record is { SchemaVersion: 2 } && record.Repository == repository) { records.Add(record); }
            }
            catch (JsonException) { /* An interrupted atomic write must not hide other runs. */ }
        }
        return records.OrderByDescending(x => x.StartedAt).ToArray();
    }

    public async Task<ReviewSettings> SettingsAsync(string repository, CancellationToken token = default)
    {
        var path = Path.Combine(RepositoryDirectory(repository), "settings.json");
        if (!files.FileExists(path)) { return new(); }
        return JsonSerializer.Deserialize<ReviewSettings>(await files.ReadAsync(path, token), Json) ?? new();
    }

    public Task SaveSettingsAsync(string repository, ReviewSettings settings, CancellationToken token = default)
    {
        files.CreateDirectory(RepositoryDirectory(repository));
        return WriteAtomicAsync(Path.Combine(RepositoryDirectory(repository), "settings.json"), settings, token);
    }

    private async Task WriteAtomicAsync<T>(string path, T value, CancellationToken token)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await files.WriteAsync(temporary, JsonSerializer.Serialize(value, Json), token);
            token.ThrowIfCancellationRequested();
            files.Replace(temporary, path);
        }
        finally { if (files.FileExists(temporary)) { files.DeleteFile(temporary); } }
    }
}
