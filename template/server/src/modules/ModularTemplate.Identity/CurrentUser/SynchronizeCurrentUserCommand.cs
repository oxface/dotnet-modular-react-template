using Mediator;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Contracts.CurrentUser;
using ModularTemplate.Identity.Users;

namespace ModularTemplate.Identity.CurrentUser;

public sealed record SynchronizeCurrentUserCommand(
    AuthenticatedIdentity? Identity) : ICommand<CurrentUserContext>;

public sealed class SynchronizeCurrentUserCommandHandler(
    ILocalUserRepository localUserRepository,
    IApplicationAccessRepository applicationAccessRepository)
    : ICommandHandler<SynchronizeCurrentUserCommand, CurrentUserContext>
{
    public async ValueTask<CurrentUserContext> Handle(
        SynchronizeCurrentUserCommand command,
        CancellationToken cancellationToken)
    {
        AuthenticatedIdentity? identity = command.Identity;
        if (identity is null || string.IsNullOrWhiteSpace(identity.Subject))
        {
            return CurrentUserContext.Unauthenticated;
        }

        LocalUser? localUser = await localUserRepository.GetByProviderSubjectAsync(
            identity.Provider,
            identity.Subject,
            cancellationToken);

        if (localUser is null)
        {
            localUser = LocalUser.Create(
                identity.Provider,
                identity.Subject,
                identity.DisplayName,
                identity.Email);
            localUserRepository.Add(localUser);
        }
        else
        {
            localUser.MarkSeen(identity.DisplayName, identity.Email);
        }

        bool hasAccess = await applicationAccessRepository.HasActiveAccessAsync(localUser.Id, cancellationToken);

        return new CurrentUserContext(
            true,
            localUser.Id,
            localUser.DisplayName,
            localUser.Email?.Value,
            hasAccess);
    }
}
