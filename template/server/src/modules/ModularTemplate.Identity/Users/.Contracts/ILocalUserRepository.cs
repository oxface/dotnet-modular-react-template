namespace ModularTemplate.Identity.Users;

public interface ILocalUserRepository
{
    Task<LocalUser?> GetByProviderSubjectAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken);

    void Add(LocalUser user);
}
