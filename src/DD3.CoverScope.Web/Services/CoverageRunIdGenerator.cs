namespace DD3.CoverScope.Services;

public sealed class CoverageRunIdGenerator
{
    private readonly TimeProvider timeProvider;
    private readonly Func<Guid> createGuid;

    public CoverageRunIdGenerator(TimeProvider timeProvider)
        : this(timeProvider, Guid.NewGuid)
    {
    }

    internal CoverageRunIdGenerator(TimeProvider timeProvider, Func<Guid> createGuid)
    {
        this.timeProvider = timeProvider;
        this.createGuid = createGuid;
    }

    public string Create() =>
        $"{timeProvider.GetUtcNow():yyyyMMdd'T'HHmmss'Z'}-{createGuid():N}"[..25];
}
