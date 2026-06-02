namespace Bondstone.Messaging;

public interface IModuleMessageInbox
{
    Task HandleOnceAsync(
        string moduleName,
        string messageId,
        string messageIdentity,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken);
}
