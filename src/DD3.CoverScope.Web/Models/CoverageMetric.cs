namespace DD3.CoverScope.Models;

public record CoverageMetric(int Covered, int Total)
{
    public double Percent => Total == 0 ? 0 : (double)Covered / Total * 100;
    public string Display => Total == 0 ? "—" : $"{Percent:0.0}%";
    public bool HasGaps => Total > 0 && Covered < Total;
}
