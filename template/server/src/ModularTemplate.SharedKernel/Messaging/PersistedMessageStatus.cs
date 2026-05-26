namespace ModularTemplate.SharedKernel.Messaging;

public enum PersistedMessageStatus
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4,
    DeadLettered = 5,
    Cancelled = 6
}
