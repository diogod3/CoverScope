using DD3.CoverScope.Models.CoverageTargets;

namespace DD3.CoverScope.Services.Foundations.CoverageTargets;

public partial interface ICoverageTargetService
{
    ValueTask<CoverageTarget> RetrieveCoverageTargetAsync(
        string path,
        string baseDirectory,
        CancellationToken cancellationToken = default);
}
