namespace DD3.CoverScope.Services.Foundations.CoverageImports;
public partial class CoverageImportService
{
    private static void ValidateInput(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead) throw new ArgumentException("The import stream must be readable.", nameof(input));
    }
}
