namespace ModularTemplate.Identity.Access;

public interface IApplicationAccessRepository
{
    Task<ApplicationAccess?> GetByLocalUserIdAsync(
        Guid localUserId,
        CancellationToken cancellationToken);

    Task<bool> HasActiveAccessAsync(
        Guid localUserId,
        CancellationToken cancellationToken);

    void Add(ApplicationAccess access);
}
