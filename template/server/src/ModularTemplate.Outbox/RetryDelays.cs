namespace ModularTemplate.Outbox;

public static class RetryDelays
{
    public static TimeSpan ForAttempt(int attempt)
    {
        return attempt switch
        {
            <= 1 => TimeSpan.Zero,
            2 => TimeSpan.FromSeconds(10),
            3 => TimeSpan.FromMinutes(1),
            4 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(15)
        };
    }
}
