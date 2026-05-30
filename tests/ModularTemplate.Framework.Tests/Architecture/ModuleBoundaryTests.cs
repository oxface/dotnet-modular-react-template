using System.Xml.Linq;

using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Architecture;

public sealed class ModuleBoundaryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ModuleProjects_DoNotReferenceOtherModuleInfrastructureProjects()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        string modulesRoot = Path.Combine(
            repositoryRoot.FullName,
            "template",
            "server",
            "src",
            "modules");

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
