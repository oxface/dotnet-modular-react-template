using Bondstone.Commands;
using ModularTemplate.Identity.Contracts.CurrentUser;

namespace ModularTemplate.Identity.CurrentUser;

public sealed class CurrentUserProvider(IModuleCommandBus commandBus) : ICurrentUserProvider
{
    public async Task<CurrentUserContext> GetCurrentUserAsync(
        AuthenticatedIdentity? identity,
        CancellationToken cancellationToken)
    {
        return await commandBus.SendAsync(
            new SynchronizeCurrentUserCommand(identity),
            cancellationToken);
    }
}
