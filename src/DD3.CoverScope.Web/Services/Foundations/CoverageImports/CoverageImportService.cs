using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Brokers.Identifiers;
using DD3.CoverScope.Brokers.DateTimes;
namespace DD3.CoverScope.Services.Foundations.CoverageImports;

public interface ICoverageImportService
{
    ValueTask<string> ImportAsync(Stream input, CancellationToken cancellationToken = default);
}
public partial class CoverageImportService(IFileSystemBroker fileSystemBroker,
    IIdentifierBroker identifierBroker, IDateTimeBroker dateTimeBroker,
    IDiagnosticsBroker diagnosticsBroker) : ICoverageImportService
{
    public ValueTask<string> ImportAsync(Stream input, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () =>
    {
        ValidateInput(input);
        var directory = Path.Combine(fileSystemBroker.GetApplicationDataDirectory(), "CoverScope", "imports");
        fileSystemBroker.CreateDirectory(directory);
        var path = Path.Combine(directory, identifierBroker.CreateIdentifier(dateTimeBroker.GetCurrentDateTime()) + ".cobertura.xml");
        await fileSystemBroker.WriteStreamAsync(path, input, cancellationToken);
        return path;
    }));
}
