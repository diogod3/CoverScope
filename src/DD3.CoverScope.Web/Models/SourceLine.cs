namespace DD3.CoverScope.Models;

public record SourceLine(int Number, string Text, CoverageLine? Coverage)
{
    public string State => Coverage switch
    {
        null => "neutral",
        { IsPartial: true } => "partial",
        { IsCovered: true } => "covered",
        _ => "uncovered"
    };
}
