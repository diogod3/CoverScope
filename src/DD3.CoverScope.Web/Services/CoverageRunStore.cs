using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services;

public sealed record CoverageRunContext(string DirectoryPath, CoverageRunManifest Manifest);

public sealed class CoverageRunStore
{
    public const int SupportedSchemaVersion = 1;
    private const int MaximumAllocationAttempts = 16;
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly CoverageRunIdGenerator idGenerator;
    private readonly TimeProvider timeProvider;

    public CoverageRunStore(CoverageRunIdGenerator idGenerator, TimeProvider timeProvider)
    {
        this.idGenerator = idGenerator;
        this.timeProvider = timeProvider;
    }

    public async Task<CoverageRunContext> BeginAsync(
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        var fullTargetPath = Path.GetFullPath(targetPath);
        if (!File.Exists(fullTargetPath))
            throw new FileNotFoundException("The selected solution or project does not exist.", fullTargetPath);
        if (!SolutionFileBrowser.IsSupportedFile(fullTargetPath))
            throw new ArgumentException("Select a .sln, .slnx, or supported project file.", nameof(targetPath));

        var targetRoot = ResolveTargetRoot(fullTargetPath);
        var reportsRoot = Path.Combine(targetRoot, ".coverscope", "reports");
        try
        {
            Directory.CreateDirectory(reportsRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"CoverScope cannot write reports to '{reportsRoot}'. Check the directory permissions and try again.", ex);
        }

        string? runId = null;
        string? runDirectory = null;
        for (var attempt = 0; attempt < MaximumAllocationAttempts; attempt++)
        {
            var candidateId = idGenerator.Create();
            var candidateDirectory = Path.Combine(reportsRoot, candidateId);
            var reservationPath = Path.Combine(reportsRoot, $".{candidateId}.reserve");
            var reservationAcquired = false;

            try
            {
                using (new FileStream(
                    reservationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                }
                reservationAcquired = true;

                if (Directory.Exists(candidateDirectory))
                    continue;

                Directory.CreateDirectory(candidateDirectory);
                runId = candidateId;
                runDirectory = candidateDirectory;
                break;
            }
            catch (IOException) when (!reservationAcquired && File.Exists(reservationPath))
            {
                continue;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"CoverScope cannot create the run directory '{candidateDirectory}'. Check the directory permissions and try again.", ex);
            }
            finally
            {
                if (reservationAcquired && File.Exists(reservationPath))
                    File.Delete(reservationPath);
            }
        }

        if (runId is null || runDirectory is null)
            throw new IOException($"CoverScope could not allocate a unique run directory under '{reportsRoot}'.");

        var extension = Path.GetExtension(fullTargetPath);
        var targetKind = extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                ? "solution"
                : "project";
        var manifest = new CoverageRunManifest(
            SupportedSchemaVersion,
            runId,
            new CoverageRunTarget(
                Path.GetFileNameWithoutExtension(fullTargetPath),
                targetKind,
                Path.GetRelativePath(targetRoot, fullTargetPath)),
            timeProvider.GetUtcNow(),
            null,
            CoverageRunStatus.InProgress,
            new CoverageRunProducer("CoverScope", GetProducerVersion()),
            []);

        try
        {
            await WriteAsync(runDirectory, manifest, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"CoverScope cannot write the run manifest in '{runDirectory}'. Check the directory permissions and try again.", ex);
        }
        return new(runDirectory, manifest);
    }

    public async Task<CoverageRunManifest> CompleteAsync(
        CoverageRunContext context,
        CoverageRunStatus status,
        IReadOnlyList<CoverageRunArtifact> artifacts,
        CancellationToken cancellationToken = default)
    {
        if (status == CoverageRunStatus.InProgress)
            throw new ArgumentException("A completed run cannot retain the in-progress status.", nameof(status));

        ValidateArtifacts(context.DirectoryPath, artifacts, requireExisting: true);
        var manifest = context.Manifest with
        {
            CompletedAtUtc = timeProvider.GetUtcNow(),
            Status = status,
            Artifacts = artifacts
        };
        await WriteAsync(context.DirectoryPath, manifest, cancellationToken);
        return manifest;
    }

    public async Task<CoverageRunManifest> ReadAsync(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        var fullManifestPath = Path.GetFullPath(manifestPath);
        CoverageRunManifest? manifest;
        try
        {
            await using var stream = File.OpenRead(fullManifestPath);
            manifest = await JsonSerializer.DeserializeAsync<CoverageRunManifest>(
                stream,
                SerializerOptions,
                cancellationToken);
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
        if (string.IsNullOrWhiteSpace(manifest.RunId) || manifest.Target is null || manifest.Producer is null || manifest.Artifacts is null)
            throw new InvalidDataException($"The CoverScope run manifest '{fullManifestPath}' is missing required fields.");

        ValidateRelativePath(manifest.Target.RelativePath, "target");
        ValidateArtifacts(Path.GetDirectoryName(fullManifestPath)!, manifest.Artifacts);
        return manifest;
    }

    public async Task<CoverageRunManifest?> ReadForArtifactAsync(
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
                return File.Exists(manifestPath)
                    ? await ReadAsync(manifestPath, cancellationToken)
                    : null;
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
        CoverageRunOutcome.Succeeded when hasCoverage => CoverageRunStatus.Completed,
        CoverageRunOutcome.TestsFailed when hasCoverage => CoverageRunStatus.CompletedWithTestFailures,
        CoverageRunOutcome.Cancelled => CoverageRunStatus.Cancelled,
        _ => CoverageRunStatus.Failed
    };

    private static async Task WriteAsync(
        string runDirectory,
        CoverageRunManifest manifest,
        CancellationToken cancellationToken)
    {
        ValidateArtifacts(runDirectory, manifest.Artifacts);
        var manifestPath = Path.Combine(runDirectory, "run.json");
        var temporaryPath = Path.Combine(runDirectory, $".run-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, manifest, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void ValidateArtifacts(
        string runDirectory,
        IEnumerable<CoverageRunArtifact> artifacts,
        bool requireExisting = false)
    {
        var fullRunDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(runDirectory));
        var runPrefix = fullRunDirectory + Path.DirectorySeparatorChar;
        foreach (var artifact in artifacts)
        {
            ValidateRelativePath(artifact.Path, "artifact");
            var resolved = Path.GetFullPath(artifact.Path, fullRunDirectory);
            if (!resolved.StartsWith(runPrefix, PathComparison))
                throw new InvalidDataException($"Artifact path '{artifact.Path}' escapes the run directory.");
            if (requireExisting && !File.Exists(resolved))
                throw new FileNotFoundException($"The run artifact '{artifact.Path}' was not written successfully.", resolved);
        }
    }

    private static void ValidateRelativePath(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            throw new InvalidDataException($"The {description} path '{path}' must be relative.");
        if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment == ".."))
            throw new InvalidDataException($"The {description} path '{path}' cannot contain parent traversal.");
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string GetProducerVersion()
    {
        var version = typeof(CoverageRunStore).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return string.IsNullOrWhiteSpace(version) ? "unknown" : version.Split('+', 2)[0];
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new UtcDateTimeOffsetConverter());
        return options;
    }

    private sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (!DateTimeOffset.TryParseExact(
                    value,
                    Format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var result))
                throw new JsonException("Expected an ISO 8601 UTC timestamp with a Z suffix.");
            return result;
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
    }
}
