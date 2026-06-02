using Bondstone.Commands;
using ModularTemplate.Identity.Users;

namespace ModularTemplate.Identity.Access;

public sealed class InitialAdminOptions
{
    public string? Provider { get; init; }

    public string? Subject { get; init; }

    public bool Force { get; init; }
}

public sealed record GrantInitialAdminAccessCommand(
    string? Provider,
    string? Subject,
    bool Force) : IModuleCommand<GrantInitialAdminAccessResult>;

public enum GrantInitialAdminAccessResult
{
    Skipped,
    Granted,
    AlreadyGranted,
    RevokedRequiresForce
}

public sealed class GrantInitialAdminAccessCommandHandler(
    ILocalUserRepository localUserRepository,
    IApplicationAccessRepository applicationAccessRepository)
    : IModuleCommandHandler<GrantInitialAdminAccessCommand, GrantInitialAdminAccessResult>
{
    public async ValueTask<GrantInitialAdminAccessResult> HandleAsync(
        GrantInitialAdminAccessCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Provider)
            || string.IsNullOrWhiteSpace(command.Subject))
        {
            return GrantInitialAdminAccessResult.Skipped;
        }

        LocalUser? user = await localUserRepository.GetByProviderSubjectAsync(
            command.Provider,
            command.Subject,
            cancellationToken);

        if (user is null)
        {
            user = LocalUser.Create(command.Provider, command.Subject, null, null);
            localUserRepository.Add(user);
        }

        ApplicationAccess? access = await applicationAccessRepository.GetByLocalUserIdAsync(
            user.Id,
            cancellationToken);

        if (access is null)
        {
            applicationAccessRepository.Add(ApplicationAccess.GrantTo(user.Id));
            return GrantInitialAdminAccessResult.Granted;
        }

        if (access.IsActive)
        {
            return GrantInitialAdminAccessResult.AlreadyGranted;
        }

        if (!command.Force)
        {
            return GrantInitialAdminAccessResult.RevokedRequiresForce;
        }

        access.Grant();
        return GrantInitialAdminAccessResult.Granted;
    }
}
