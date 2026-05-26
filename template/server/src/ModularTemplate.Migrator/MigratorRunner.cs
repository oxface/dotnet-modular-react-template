using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Operations.Infrastructure.Persistence;

namespace ModularTemplate.Migrator;

public static class MigratorRunner
{
    public static async Task<int> RunAsync(
        string[] args,
        IConfiguration configuration,
        IServiceProvider services,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!MigratorCommand.TryParse(args, out MigratorCommand command, out string? parseError))
        {
            await error.WriteLineAsync(parseError);
            return 2;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();

        IdentityDbContext identityContext =
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await identityContext.Database.MigrateAsync(cancellationToken);

        OperationsDbContext operationsContext =
            scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
        await operationsContext.Database.MigrateAsync(cancellationToken);

        string? configurationError = null;
        InitialAdminOptions? initialAdmin = command.InitialAdmin
            ?? ReadConfiguredInitialAdmin(configuration, out configurationError);
        if (configurationError is not null)
        {
            await error.WriteLineAsync(configurationError);
            return 2;
        }

        if (initialAdmin is null)
        {
            return 0;
        }

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        GrantInitialAdminAccessResult result = await mediator.Send(
            new GrantInitialAdminAccessCommand(
                initialAdmin.Provider,
                initialAdmin.Subject,
                initialAdmin.Force),
            cancellationToken);

        await output.WriteLineAsync($"Initial admin setup: {result}.");
        return result == GrantInitialAdminAccessResult.RevokedRequiresForce ? 1 : 0;
    }

    private static InitialAdminOptions? ReadConfiguredInitialAdmin(
        IConfiguration configuration,
        out string? error)
    {
        error = null;

        InitialAdminOptions options = configuration
            .GetSection("Identity:InitialAdmin")
            .Get<InitialAdminOptions>()
            ?? new InitialAdminOptions();

        bool hasProvider = !string.IsNullOrWhiteSpace(options.Provider);
        bool hasSubject = !string.IsNullOrWhiteSpace(options.Subject);
        if (!hasProvider && !hasSubject)
        {
            return null;
        }

        if (!hasProvider || !hasSubject)
        {
            error = "Identity:InitialAdmin requires both Provider and Subject when either value is configured.";
            return null;
        }

        return options;
    }
}
