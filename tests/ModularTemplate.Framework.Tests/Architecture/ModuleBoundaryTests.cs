using System.Xml.Linq;

using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Architecture;

public sealed class ModuleBoundaryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ModuleProjects_DoNotReferenceOtherModuleInfrastructureProjects()
    {
        string modulesRoot = GetModulesRoot();

        string[] moduleInfrastructureProjectNames = Directory
            .EnumerateDirectories(modulesRoot, "ModularTemplate.*.Infrastructure")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        var violations = new List<string>();

        foreach (string projectPath in Directory.EnumerateFiles(modulesRoot, "*.csproj", SearchOption.AllDirectories))
        {
            string? projectDirectoryName = Path.GetFileName(Path.GetDirectoryName(projectPath));
            if (projectDirectoryName is null)
            {
                continue;
            }

            foreach (string referencedProject in ReadProjectReferences(projectPath))
            {
                string referencedProjectName = Path.GetFileNameWithoutExtension(referencedProject);
                bool referencesOtherModuleInfrastructure = moduleInfrastructureProjectNames
                    .Any(infrastructureProjectName =>
                        string.Equals(referencedProjectName, infrastructureProjectName, StringComparison.Ordinal)
                        && !string.Equals(projectDirectoryName, infrastructureProjectName, StringComparison.Ordinal));

                if (referencesOtherModuleInfrastructure)
                {
                    violations.Add(
                        $"{Path.GetFileName(projectPath)} references {referencedProjectName}, crossing a module Infrastructure boundary.");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ModuleContractsProjects_DoNotReferenceModuleImplementationOrInfrastructureProjects()
    {
        string modulesRoot = GetModulesRoot();
        string[] moduleImplementationProjectNames = GetModuleProjectNames(
            modulesRoot,
            projectName => !projectName.EndsWith(".Contracts", StringComparison.Ordinal)
                && !projectName.EndsWith(".Infrastructure", StringComparison.Ordinal));
        string[] moduleInfrastructureProjectNames = GetModuleProjectNames(
            modulesRoot,
            projectName => projectName.EndsWith(".Infrastructure", StringComparison.Ordinal));

        var violations = new List<string>();

        foreach (string projectPath in Directory.EnumerateFiles(modulesRoot, "ModularTemplate.*.Contracts.csproj", SearchOption.AllDirectories))
        {
            foreach (string referencedProject in ReadProjectReferences(projectPath))
            {
                string referencedProjectName = Path.GetFileNameWithoutExtension(referencedProject);
                if (moduleImplementationProjectNames.Contains(referencedProjectName, StringComparer.Ordinal)
                    || moduleInfrastructureProjectNames.Contains(referencedProjectName, StringComparer.Ordinal))
                {
                    violations.Add(
                        $"{Path.GetFileName(projectPath)} references {referencedProjectName}, leaking implementation details into Contracts.");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ModuleImplementationProjects_DoNotReferenceOtherModuleImplementations()
    {
        string modulesRoot = GetModulesRoot();
        string[] moduleImplementationProjectNames = GetModuleProjectNames(
            modulesRoot,
            projectName => !projectName.EndsWith(".Contracts", StringComparison.Ordinal)
                && !projectName.EndsWith(".Infrastructure", StringComparison.Ordinal));

        var violations = new List<string>();

        foreach (string projectPath in Directory.EnumerateFiles(modulesRoot, "ModularTemplate.*.csproj", SearchOption.AllDirectories))
        {
            string projectName = Path.GetFileNameWithoutExtension(projectPath);
            if (!moduleImplementationProjectNames.Contains(projectName, StringComparer.Ordinal))
            {
                continue;
            }

            foreach (string referencedProject in ReadProjectReferences(projectPath))
            {
                string referencedProjectName = Path.GetFileNameWithoutExtension(referencedProject);
                bool referencesOtherModuleImplementation = moduleImplementationProjectNames
                    .Any(moduleProjectName =>
                        string.Equals(referencedProjectName, moduleProjectName, StringComparison.Ordinal)
                        && !string.Equals(projectName, moduleProjectName, StringComparison.Ordinal));

                if (referencesOtherModuleImplementation)
                {
                    violations.Add(
                        $"{Path.GetFileName(projectPath)} references {referencedProjectName}, crossing a module implementation boundary.");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OnlyCompositionProjectsReferenceModuleInfrastructureProjects()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string serverSrcRoot = Path.Combine(repositoryRoot.FullName, "template", "server", "src");
        string modulesRoot = GetModulesRoot();
        string[] moduleInfrastructureProjectNames = GetModuleProjectNames(
            modulesRoot,
            projectName => projectName.EndsWith(".Infrastructure", StringComparison.Ordinal));
        string[] allowedCompositionProjects =
        [
            "ModularTemplate.Host",
            "ModularTemplate.Migrator"
        ];

        var violations = new List<string>();

        foreach (string projectPath in Directory.EnumerateFiles(serverSrcRoot, "*.csproj", SearchOption.AllDirectories))
        {
            string projectName = Path.GetFileNameWithoutExtension(projectPath);
            if (allowedCompositionProjects.Contains(projectName, StringComparer.Ordinal))
            {
                continue;
            }

            foreach (string referencedProject in ReadProjectReferences(projectPath))
            {
                string referencedProjectName = Path.GetFileNameWithoutExtension(referencedProject);
                if (moduleInfrastructureProjectNames.Contains(referencedProjectName, StringComparer.Ordinal))
                {
                    violations.Add(
                        $"{Path.GetFileName(projectPath)} references {referencedProjectName}; module Infrastructure composition belongs in Host or Migrator.");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    private static string[] GetModuleProjectNames(
        string modulesRoot,
        Func<string, bool> predicate)
    {
        return Directory
            .EnumerateFiles(modulesRoot, "ModularTemplate.*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(predicate)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetModulesRoot()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        return Path.Combine(
            repositoryRoot.FullName,
            "template",
            "server",
            "src",
            "modules");
    }

    private static IEnumerable<string> ReadProjectReferences(string projectPath)
    {
        XDocument project = XDocument.Load(projectPath);
        return project
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .OfType<string>();
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "template", "server", "src", "modules")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
