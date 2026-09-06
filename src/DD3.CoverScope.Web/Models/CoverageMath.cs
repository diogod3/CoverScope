namespace DD3.CoverScope.Models;

public static class CoverageMath
{
    public static CoverageMetric Combine(IEnumerable<CoverageMetric> metrics)
    {
        var covered = 0;
        var total = 0;
        foreach (var metric in metrics)
        {
            covered += metric.Covered;
            total += metric.Total;
        }
        return new CoverageMetric(covered, total);
    }
}
