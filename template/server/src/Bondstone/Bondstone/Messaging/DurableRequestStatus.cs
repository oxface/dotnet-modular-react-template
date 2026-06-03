namespace Bondstone.Messaging;

public enum DurableRequestStatus
{
    Completed = 1,
    Failed = 2,
    Cancelled = 3,
    TimedOut = 4
}
