using CoverageSettings = DD3.CoverScope.Models.CoverageSettings;
using DD3.CoverScope.Brokers.Diagnostics;
using DD3.CoverScope.Brokers.FileSystems;
using DD3.CoverScope.Brokers.Serializations;
using DD3.CoverScope.Models;
namespace DD3.CoverScope.Services.Foundations.CoverageSettings;

public interface ICoverageSettingsService
{
    Task<CoverageSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CoverageSettings settings, CancellationToken cancellationToken = default);
}

public partial class CoverageSettingsService(IFileSystemBroker fileSystemBroker,
    ISerializationBroker serializationBroker, IDiagnosticsBroker diagnosticsBroker) : ICoverageSettingsService
{
    private string SettingsPath => Path.Combine(fileSystemBroker.GetApplicationDataDirectory(), "CoverScope", "settings.json");

    public Task<CoverageSettings> LoadAsync(CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = SettingsPath;
        if (!fileSystemBroker.FileExists(path)) return new CoverageSettings();
        var settings = serializationBroker.Deserialize<CoverageSettings>(
            await fileSystemBroker.ReadAllTextAsync(path, cancellationToken)) ?? new();
        ValidateSettings(settings);
        return settings;
    })).AsTask();

    public Task SaveAsync(CoverageSettings settings, CancellationToken cancellationToken = default) =>
        Trace(() => TryCatch(async () =>
    {
        ValidateSettings(settings);
        var path = SettingsPath;
        fileSystemBroker.CreateDirectory(Path.GetDirectoryName(path)!);
        await fileSystemBroker.WriteAllTextAsync(path, serializationBroker.Serialize(settings), cancellationToken);
        return true;
    })).AsTask();
}
