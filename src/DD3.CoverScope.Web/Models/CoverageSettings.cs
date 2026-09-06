namespace DD3.CoverScope.Models;

public class CoverageSettings
{
    public string Exclude { get; set; } = string.Empty;
    public string ExcludeByFile { get; set; } = string.Empty;
    public string ExcludeByAttribute { get; set; } = string.Empty;
    public bool SkipAutoProps { get; set; }
}
