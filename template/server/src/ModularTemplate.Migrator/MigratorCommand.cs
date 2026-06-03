using ModularTemplate.Identity.Access;

namespace ModularTemplate.Migrator;

public sealed record MigratorCommand(
    MigratorMigrationScope MigrationScope,
    string? ModuleName,
    InitialAdminOptions? InitialAdmin,
    bool UseConfiguredInitialAdmin)
{
    public static bool TryParse(
        string[] args,
        out MigratorCommand command,
        out string? error)
    {
        command = new MigratorCommand(
            MigratorMigrationScope.All,
            ModuleName: null,
            InitialAdmin: null,
            UseConfiguredInitialAdmin: true);
        error = null;

        if (args is [] or ["migrate"])
        {
            return true;
        }

        if (args is ["migrate", "transport"])
        {
            command = command with
            {
                MigrationScope = MigratorMigrationScope.Transport,
                UseConfiguredInitialAdmin = false
            };
            return true;
        }

        if (args is ["migrate", "modules"])
        {
            command = command with
            {
                MigrationScope = MigratorMigrationScope.Modules,
                UseConfiguredInitialAdmin = false
            };
            return true;
        }

        if (args is ["migrate", "module", string moduleName])
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                error = "Module name is required.";
                return false;
            }

            command = command with
            {
                MigrationScope = MigratorMigrationScope.Module,
                ModuleName = moduleName,
                UseConfiguredInitialAdmin = false
            };
            return true;
        }

        if (args is not ["identity", "grant-admin", ..])
        {
            error = "Usage: migrator [migrate [transport|modules|module <name>]] | identity grant-admin --provider <issuer> --subject <subject> [--force]";
            return false;
        }

        string? provider = null;
        string? subject = null;
        bool force = false;

        for (int index = 2; index < args.Length; index++)
        {
            string arg = args[index];
            switch (arg)
            {
                case "--provider":
                    if (!TryReadValue(args, ref index, out provider))
                    {
                        error = "--provider requires a value.";
                        return false;
                    }

                    break;
                case "--subject":
                    if (!TryReadValue(args, ref index, out subject))
                    {
                        error = "--subject requires a value.";
                        return false;
                    }

                    break;
                case "--force":
                    force = true;
                    break;
                default:
                    error = $"Unknown argument '{arg}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(subject))
        {
            error = "Both --provider and --subject are required.";
            return false;
        }

        command = command with
        {
            InitialAdmin = new InitialAdminOptions
            {
                Provider = provider,
                Subject = subject,
                Force = force
            },
            UseConfiguredInitialAdmin = false
        };
        return true;
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        value = null;
        int valueIndex = index + 1;
        if (valueIndex >= args.Length || args[valueIndex].StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        value = args[valueIndex];
        index = valueIndex;
        return true;
    }
}

public enum MigratorMigrationScope
{
    All,
    Transport,
    Modules,
    Module
}
