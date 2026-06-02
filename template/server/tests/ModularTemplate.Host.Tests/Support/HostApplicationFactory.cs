using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ModularTemplate.Host.Tests.Authentication;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Users;

namespace ModularTemplate.Host.Tests.Support;

public sealed class HostApplicationFactory(
    Action<IServiceCollection>? configureServices = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.Scheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.Scheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme,
                    _ => { });

            services.RemoveAll<ILocalUserRepository>();
            services.RemoveAll<IApplicationAccessRepository>();
            services.RemoveModuleCommandPipelineBehaviors();
            services.AddSingleton<HostTestIdentityContext>();
            services.AddSingleton<ILocalUserRepository>(services => services.GetRequiredService<HostTestIdentityContext>());
            services.AddSingleton<IApplicationAccessRepository>(services => services.GetRequiredService<HostTestIdentityContext>());

            configureServices?.Invoke(services);
        });
    }
}

internal sealed class HostTestIdentityContext :
    ILocalUserRepository,
    IApplicationAccessRepository
{
    private readonly List<ApplicationAccess> _accessRecords = [];
    private readonly List<LocalUser> _users = [];

    public Task<LocalUser?> GetByProviderSubjectAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_users.SingleOrDefault(x => x.Provider == provider && x.Subject == subject));
    }

    public void Add(LocalUser user)
    {
        _users.Add(user);
        if (user.Subject.EndsWith("-with-access", StringComparison.Ordinal)
            && !_accessRecords.Any(x => x.LocalUserId == user.Id))
        {
            _accessRecords.Add(ApplicationAccess.GrantTo(user.Id));
        }
    }

    public Task<ApplicationAccess?> GetByLocalUserIdAsync(
        Guid localUserId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_accessRecords.SingleOrDefault(x => x.LocalUserId == localUserId));
    }

    public Task<bool> HasActiveAccessAsync(
        Guid localUserId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_accessRecords.Any(x => x.LocalUserId == localUserId && x.IsActive));
    }

    public void Add(ApplicationAccess access)
    {
        _accessRecords.Add(access);
    }
}
