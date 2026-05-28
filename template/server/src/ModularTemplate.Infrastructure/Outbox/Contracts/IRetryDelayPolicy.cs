namespace ModularTemplate.Infrastructure.Outbox;

public interface IRetryDelayPolicy
{
    TimeSpan GetDelay(int attempt);
}
