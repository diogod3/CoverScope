using DD3.CoverScope.Models;

namespace DD3.CoverScope.Services;

public enum CoverageCollectionPhase
{
    PreparingCollection,
    RestoringProjects,
    RunningTests,
    ProcessingReports,
    BuildingMetrics
}

public class CoverageCollectionState
{
    public bool IsActive { get; private set; }
    public CoverageCollectionPhase Phase { get; private set; } = CoverageCollectionPhase.PreparingCollection;
    public DateTimeOffset StartedAt { get; private set; }
    public CoverageRunOutcome? LastOutcome { get; private set; }

    public string PhaseLabel => GetLabel(Phase);

    public void Start(DateTimeOffset? startedAt = null)
    {
        IsActive = true;
        Phase = CoverageCollectionPhase.PreparingCollection;
        StartedAt = startedAt ?? DateTimeOffset.UtcNow;
        LastOutcome = null;
    }

    public bool Advance(CoverageCollectionPhase phase)
    {
        if (!IsActive || phase <= Phase)
            return false;

        Phase = phase;
        return true;
    }

    public void Complete(CoverageRunOutcome outcome)
    {
        IsActive = false;
        LastOutcome = outcome;
    }

    public static string GetLabel(CoverageCollectionPhase phase) => phase switch
    {
        CoverageCollectionPhase.PreparingCollection => "Preparing collection",
        CoverageCollectionPhase.RestoringProjects => "Restoring projects",
        CoverageCollectionPhase.RunningTests => "Running tests",
        CoverageCollectionPhase.ProcessingReports => "Processing reports",
        CoverageCollectionPhase.BuildingMetrics => "Building metrics",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
    };
}
