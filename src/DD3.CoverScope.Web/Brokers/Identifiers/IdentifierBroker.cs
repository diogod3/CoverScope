namespace DD3.CoverScope.Brokers.Identifiers;

public interface IIdentifierBroker { Guid CreateIdentifier(DateTimeOffset timestamp); }

public class IdentifierBroker : IIdentifierBroker
{
    public Guid CreateIdentifier(DateTimeOffset timestamp) => Guid.CreateVersion7(timestamp);
}
