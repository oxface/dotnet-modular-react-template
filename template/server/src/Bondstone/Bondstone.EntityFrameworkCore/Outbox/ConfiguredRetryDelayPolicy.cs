using Microsoft.Extensions.Options;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Outbox;

public sealed class ConfiguredRetryDelayPolicy(IOptions<DurableMessagingOptions> options) : IRetryDelayPolicy
{
    private readonly DurableMessagingOptions _options = options.Value;

    public TimeSpan GetDelay(int attempt)
    {
        if (attempt <= 0)
        {
            return TimeSpan.Zero;
        }

        if (_options.RetryDelays.Count == 0)
        {
            return TimeSpan.Zero;
        }

        int index = Math.Min(attempt - 1, _options.RetryDelays.Count - 1);
        return _options.RetryDelays[index];
    }
}
