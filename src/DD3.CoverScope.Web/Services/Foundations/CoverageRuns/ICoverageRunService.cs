using CoverageRunSettings = DD3.CoverScope.Models.CoverageRunSettings;
using DD3.CoverScope.Models;
using DD3.CoverScope.Models.CoverageTargets;
namespace DD3.CoverScope.Services.Foundations.CoverageRuns;
public interface ICoverageRunService
{
    Task<CoverageRunContext> BeginAsync(CoverageTarget target, CoverageRunSettings settings, CancellationToken cancellationToken = default);
    Task<CoverageRun> CompleteAsync(CoverageRunContext context, CoverageRunStatus status, IReadOnlyList<CoverageRunArtifact> artifacts, CancellationToken cancellationToken = default);
    Task<CoverageRun> ReadAsync(string manifestPath, CancellationToken cancellationToken = default);
    Task<CoverageRun?> ReadForArtifactAsync(string artifactPath, CancellationToken cancellationToken = default);
    Task<CoverageRunContext> ChangeStatusAsync(CoverageRunContext context, CoverageRunStatus status,
        CancellationToken cancellationToken = default);
}
