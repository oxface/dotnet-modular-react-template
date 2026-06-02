namespace Bondstone.EntityFrameworkCore.Outbox;

public interface IRetryDelayPolicy
{
    TimeSpan GetDelay(int attempt);
}
