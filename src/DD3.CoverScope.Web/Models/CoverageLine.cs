namespace DD3.CoverScope.Models;

public record CoverageLine(int Number, int Hits, int? BranchesCovered = null, int? BranchesTotal = null)
{
    public bool IsCovered => Hits > 0;
    public bool IsBranch => BranchesTotal > 0;
    public bool IsPartial => IsBranch && BranchesCovered < BranchesTotal;
}
