using Mediator;
using ModularTemplate.Identity.Contracts.CurrentUser;

namespace ModularTemplate.Identity.CurrentUser;

public sealed class CurrentUserProvider(IMediator mediator) : ICurrentUserProvider
{
    public async Task<CurrentUserContext> GetCurrentUserAsync(
        AuthenticatedIdentity? identity,
        CancellationToken cancellationToken)
    {
        return await mediator.Send(
            new SynchronizeCurrentUserCommand(identity),
            cancellationToken);
    }
}
