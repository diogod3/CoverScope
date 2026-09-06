namespace DD3.CoverScope.Brokers.DateTimes;

public interface IDateTimeBroker { DateTimeOffset GetCurrentDateTime(); }

public class DateTimeBroker(TimeProvider timeProvider) : IDateTimeBroker
{
    public DateTimeOffset GetCurrentDateTime() => timeProvider.GetUtcNow();
}
