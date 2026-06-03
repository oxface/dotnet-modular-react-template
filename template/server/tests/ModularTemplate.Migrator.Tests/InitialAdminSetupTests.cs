using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Migrator.Tests.Support;
using ModularTemplate.Products.Infrastructure.Persistence;
using Shouldly;

namespace ModularTemplate.Migrator.Tests;

public sealed class InitialAdminSetupTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task RunAsync_WhenInitialAdminIsConfigured_MigratesAndGrantsAccessIdempotently()
    {
        using IHost host = CreateHost();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int firstExitCode = await MigratorRunner.RunAsync(
            [],
            host.Services.GetRequiredService<IConfiguration>(),
            host.Services,
            output,
            error,
            CancellationToken.None);
        int secondExitCode = await MigratorRunner.RunAsync(
            [],
            host.Services.GetRequiredService<IConfiguration>(),
            host.Services,
            output,
            error,
            CancellationToken.None);

        firstExitCode.ShouldBe(0);
        secondExitCode.ShouldBe(0);
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = dbContext.LocalUsers.Single(x => x.Provider == "oidc" && x.Subject == "subject-1");
        dbContext.ApplicationAccess.Count(x => x.LocalUserId == user.Id).ShouldBe(1);
        dbContext.ApplicationAccess.Single(x => x.LocalUserId == user.Id).IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RunAsync_WhenInitialAdminWasRevoked_ReturnsFailureUntilForced()
    {
        using IHost host = CreateHost();
        using var output = new StringWriter();
        using var error = new StringWriter();
        await MigratorRunner.RunAsync(
            [],
            host.Services.GetRequiredService<IConfiguration>(),
            host.Services,
            output,
            error,
            CancellationToken.None);
        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            ApplicationAccess access = dbContext.ApplicationAccess.Single();
            access.Revoke();
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        int blockedExitCode = await MigratorRunner.RunAsync(
            [],
            host.Services.GetRequiredService<IConfiguration>(),
            host.Services,
            output,
            error,
            CancellationToken.None);
        int forcedExitCode = await MigratorRunner.RunAsync(
            ["identity", "grant-admin", "--provider", "oidc", "--subject", "subject-1", "--force"],
            host.Services.GetRequiredService<IConfiguration>(),
            host.Services,
            output,
            error,
            CancellationToken.None);

        blockedExitCode.ShouldBe(1);
        forcedExitCode.ShouldBe(0);
        await using AsyncServiceScope verifyScope = host.Services.CreateAsyncScope();
        IdentityDbContext verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        verifyDbContext.ApplicationAccess.Single().IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RunAsync_WhenDatabaseIsFresh_MigratesModuleOwnedOutboxTables()
    {
        using IHost host = CreateHost(configureInitialAdmin: false);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await MigratorRunner.RunAsync(
            [],
            host.Services.GetRequiredService<IConfiguration>(),
            host.Services,
            output,
            error,
            CancellationToken.None);

        exitCode.ShouldBe(0);
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IdentityDbContext identityContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        ProductsDbContext productsContext = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
        await AssertSchemaExistsAsync(identityContext, "transport");
        await AssertTablesExistAsync(
            identityContext,
            "transport",
            [
                "rebus_subscriptions",
            ]);
        await AssertTablesExistAsync(
            identityContext,
            "identity",
            [
                "application_access",
                "local_users",
                "domain_events",
                "inbox_messages",
                "outbox_messages",
                "__EFMigrationsHistory",
            ]);
        await AssertTablesExistAsync(
            productsContext,
            "products",
            [
                "products",
                "domain_events",
                "inbox_messages",
                "outbox_messages",
                "__EFMigrationsHistory",
            ]);
    }

    private IHost CreateHost(bool configureInitialAdmin = true)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:modular-template-host"] = fixture.ConnectionString;
        if (configureInitialAdmin)
        {
            builder.Configuration["Identity:InitialAdmin:Provider"] = "oidc";
            builder.Configuration["Identity:InitialAdmin:Subject"] = "subject-1";
        }

        builder.AddMigratorComposition();

        return builder.Build();
    }

    private static async Task AssertTablesExistAsync(
        DbContext dbContext,
        string schema,
        IReadOnlyCollection<string> tableNames)
    {
        string[] tables = await dbContext.Database
            .SqlQueryRaw<string>(
                """
                SELECT table_name AS "Value"
                FROM information_schema.tables
                WHERE table_schema = {0}
                """,
                schema)
            .ToArrayAsync(CancellationToken.None);

        foreach (string tableName in tableNames)
        {
            tables.ShouldContain(tableName);
        }
    }

    private static async Task AssertSchemaExistsAsync(DbContext dbContext, string schema)
    {
        string? matchingSchema = await dbContext.Database
            .SqlQueryRaw<string>(
                """
                SELECT schema_name AS "Value"
                FROM information_schema.schemata
                WHERE schema_name = {0}
                """,
                schema)
            .SingleOrDefaultAsync(CancellationToken.None);

        matchingSchema.ShouldBe(schema);
    }
}
