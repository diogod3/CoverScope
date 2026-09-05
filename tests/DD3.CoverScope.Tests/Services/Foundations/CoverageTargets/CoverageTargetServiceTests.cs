using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Services.Foundations.CoverageTargets;

namespace DD3.CoverScope.Tests.Services.Foundations.CoverageTargets;

public partial class CoverageTargetServiceTests
{
    private readonly List<string> events = [];
    private readonly TestFileSystemBroker fileSystemBroker;
    private readonly TestDiagnosticsBroker diagnosticsBroker;
    private readonly CoverageTargetService service;
    private readonly string baseDirectory =
        Path.Combine(Path.GetTempPath(), $"coverscope-target-{Guid.NewGuid():N}");

    public CoverageTargetServiceTests()
    {
        fileSystemBroker = new TestFileSystemBroker(events);
        diagnosticsBroker = new TestDiagnosticsBroker(events);
        service = new CoverageTargetService(fileSystemBroker, diagnosticsBroker);
    }

    private class TestFileSystemBroker(List<string> events) : IFileSystemBroker
    {
        public bool Exists { get; set; } = true;
        public Exception? Failure { get; set; }
        public List<string> RequestedPaths { get; } = [];

        public bool FileExists(string path)
        {
            events.Add("filesystem");
            RequestedPaths.Add(path);
            if (Failure is not null)
                throw Failure;
            return Exists;
        }

        public bool DirectoryExists(string path) =>
            throw new InvalidOperationException("Target retrieval must not browse directories.");

        public IReadOnlyList<string> EnumerateFiles(string directoryPath) =>
            throw new InvalidOperationException("Target retrieval must not enumerate files.");

        public IReadOnlyList<string> EnumerateDirectories(string directoryPath) =>
            throw new InvalidOperationException("Target retrieval must not enumerate directories.");
    }

    private class TestDiagnosticsBroker(List<string> events) : IDiagnosticsBroker
    {
        public List<string> ActivityNames { get; } = [];
        public Exception? ObservedException { get; private set; }

        public async ValueTask<TResult> Trace<TResult>(
            Func<ValueTask<TResult>> operation,
            string activityName)
        {
            events.Add("trace");
            ActivityNames.Add(activityName);
            try
            {
                var result = await operation();
                events.Add("completed");
                return result;
            }
            catch (Exception exception)
            {
                ObservedException = exception;
                events.Add("failed");
                throw;
            }
        }
    }
}
