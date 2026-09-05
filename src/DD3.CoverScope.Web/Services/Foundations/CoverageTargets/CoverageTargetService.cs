using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Models.CoverageTargets;

namespace DD3.CoverScope.Services.Foundations.CoverageTargets;

public partial class CoverageTargetService : ICoverageTargetService
{
    private readonly IFileSystemBroker fileSystemBroker;
    private readonly IDiagnosticsBroker diagnosticsBroker;

    public CoverageTargetService(
        IFileSystemBroker fileSystemBroker,
        IDiagnosticsBroker diagnosticsBroker)
    {
        this.fileSystemBroker = fileSystemBroker;
        this.diagnosticsBroker = diagnosticsBroker;
    }

    public ValueTask<CoverageTarget> RetrieveCoverageTargetAsync(
        string path,
        string baseDirectory,
        CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateTargetPath(path, baseDirectory);

            string fullPath = System.IO.Path.GetFullPath(path, baseDirectory);
            CoverageTargetType type = ValidateTargetType(fullPath);
            bool exists = this.fileSystemBroker.FileExists(fullPath);

            ValidateTargetExists(exists, fullPath);

            var target = new CoverageTarget(
                System.IO.Path.GetFileNameWithoutExtension(fullPath),
                fullPath,
                type);

            return ValueTask.FromResult(target);
        }));
}
