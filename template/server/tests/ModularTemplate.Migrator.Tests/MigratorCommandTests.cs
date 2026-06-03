using Shouldly;

namespace ModularTemplate.Migrator.Tests;

public sealed class MigratorCommandTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void TryParse_WhenNoArgs_UsesConfigurationOnly()
    {
        bool parsed = MigratorCommand.TryParse([], out MigratorCommand command, out string? error);

        parsed.ShouldBeTrue();
        error.ShouldBeNull();
        command.InitialAdmin.ShouldBeNull();
        command.MigrationScope.ShouldBe(MigratorMigrationScope.All);
        command.UseConfiguredInitialAdmin.ShouldBeTrue();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("transport", MigratorMigrationScope.Transport)]
    [InlineData("modules", MigratorMigrationScope.Modules)]
    public void TryParse_WhenMigrateScopeIsProvided_ReturnsScopedMigration(
        string scope,
        MigratorMigrationScope expectedScope)
    {
        bool parsed = MigratorCommand.TryParse(["migrate", scope], out MigratorCommand command, out string? error);

        parsed.ShouldBeTrue();
        error.ShouldBeNull();
        command.MigrationScope.ShouldBe(expectedScope);
        command.InitialAdmin.ShouldBeNull();
        command.UseConfiguredInitialAdmin.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryParse_WhenSingleModuleMigrationIsProvided_ReturnsModuleName()
    {
        bool parsed = MigratorCommand.TryParse(
            ["migrate", "module", "identity"],
            out MigratorCommand command,
            out string? error);

        parsed.ShouldBeTrue();
        error.ShouldBeNull();
        command.MigrationScope.ShouldBe(MigratorMigrationScope.Module);
        command.ModuleName.ShouldBe("identity");
        command.UseConfiguredInitialAdmin.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryParse_WhenGrantAdminArgsAreComplete_ReturnsInitialAdmin()
    {
        bool parsed = MigratorCommand.TryParse(
            ["identity", "grant-admin", "--provider", "oidc", "--subject", "subject-1", "--force"],
            out MigratorCommand command,
            out string? error);

        parsed.ShouldBeTrue();
        error.ShouldBeNull();
        command.MigrationScope.ShouldBe(MigratorMigrationScope.All);
        command.InitialAdmin.ShouldNotBeNull();
        command.InitialAdmin.Provider.ShouldBe("oidc");
        command.InitialAdmin.Subject.ShouldBe("subject-1");
        command.InitialAdmin.Force.ShouldBeTrue();
        command.UseConfiguredInitialAdmin.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TryParse_WhenGrantAdminArgsAreIncomplete_ReturnsError()
    {
        bool parsed = MigratorCommand.TryParse(
            ["identity", "grant-admin", "--provider", "oidc"],
            out _,
            out string? error);

        parsed.ShouldBeFalse();
        error.ShouldBe("Both --provider and --subject are required.");
    }
}
