using System.Text.Json;
using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services;

public sealed class CoverageSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CoverScope",
        "settings.json");

    public async Task<CoverageSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(settingsPath)) return new CoverageSettings();
            await using var stream = File.OpenRead(settingsPath);
            return await JsonSerializer.DeserializeAsync<CoverageSettings>(stream, JsonOptions, cancellationToken)
                ?? new CoverageSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new CoverageSettings();
        }
    }

    public async Task SaveAsync(CoverageSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        await using var stream = File.Create(settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }
}
