using CoverageArtifacts = DD3.CoverScope.Models.CoverageArtifacts;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services.Foundations.CoverageArtifacts;

public interface ICoverageArtifactService
{
    ValueTask<CoverageArtifacts> RetrieveAsync(string directory, CancellationToken cancellationToken = default);
}

public partial class CoverageArtifactService(IFileSystemBroker fileSystemBroker,
    IDiagnosticsBroker diagnosticsBroker) : ICoverageArtifactService
{
    public ValueTask<CoverageArtifacts> RetrieveAsync(string directory, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(() =>
    {
        ValidateDirectory(directory);
        cancellationToken.ThrowIfCancellationRequested();
        var reports = fileSystemBroker.EnumerateFiles(directory, "coverage.cobertura.xml", SearchOption.AllDirectories)
            .OrderByDescending(fileSystemBroker.GetLastWriteTime).ToArray();
        var tests = fileSystemBroker.EnumerateFiles(directory, "*.trx", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        return ValueTask.FromResult(new CoverageArtifacts(reports, tests));
    }));
}
