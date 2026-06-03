using Bondstone.Commands;
using ModularTemplate.Identity.Contracts.CurrentUser;

namespace ModularTemplate.Identity.CurrentUser;

public sealed class CurrentUserProvider(
    IModuleCommandExecutor<SynchronizeCurrentUserCommand, CurrentUserContext> commandExecutor)
    : ICurrentUserProvider
{
    public async Task<CurrentUserContext> GetCurrentUserAsync(
        AuthenticatedIdentity? identity,
        CancellationToken cancellationToken)
    {
        return await commandExecutor.SendAsync(
            new SynchronizeCurrentUserCommand(identity),
            cancellationToken);
    }
}
