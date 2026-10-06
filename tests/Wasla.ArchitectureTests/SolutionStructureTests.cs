using System.Xml.Linq;

namespace Wasla.ArchitectureTests;

/// <summary>
/// Enforces the module dependency rules from docs/architecture.md §6 by inspecting the
/// project and package references of every csproj in the solution.
/// </summary>
public sealed class SolutionStructureTests
{
    private const string BuildingBlocksProject = "Wasla.BuildingBlocks";

    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string[] ModuleLayerSuffixes = ["Domain", "Application", "Infrastructure"];

    [Fact]
    public void Domain_projects_reference_only_building_blocks()
    {
        foreach (var project in ModuleProjects("Domain"))
        {
            Assert.Contains(BuildingBlocksProject, project.ProjectReferences);
            Assert.All(project.ProjectReferences, reference =>
                Assert.True(
                    reference == BuildingBlocksProject,
                    $"{project.Name} must only reference {BuildingBlocksProject} but references {reference}."));
        }
    }

    [Fact]
    public void Application_projects_reference_only_their_own_domain_and_building_blocks()
    {
        foreach (var project in ModuleProjects("Application"))
        {
            var allowed = new[] { OwnModuleProject(project.Name, "Domain"), BuildingBlocksProject };

            Assert.All(project.ProjectReferences, reference =>
                Assert.True(
                    allowed.Contains(reference),
                    $"{project.Name} references {reference}, which is not allowed."));
        }
    }

    [Fact]
    public void Infrastructure_projects_reference_only_their_own_module_and_building_blocks()
    {
        foreach (var project in ModuleProjects("Infrastructure"))
        {
            var allowed = new[]
            {
                OwnModuleProject(project.Name, "Domain"),
                OwnModuleProject(project.Name, "Application"),
                BuildingBlocksProject,
            };

            Assert.All(project.ProjectReferences, reference =>
                Assert.True(
                    allowed.Contains(reference),
                    $"{project.Name} references {reference}, which is not allowed."));
        }
    }

    [Fact]
    public void No_cross_module_references_exist()
    {
        foreach (var project in AllProjects())
        {
            var ownModule = ModuleNameOf(project.Name);

            // The rule applies to module projects only: the host (Wasla.Api), the shared
            // kernel and test projects may legitimately reference modules.
            if (ownModule is null)
            {
                continue;
            }

            foreach (var reference in project.ProjectReferences.Where(IsModuleProject))
            {
                var referencedModule = ModuleNameOf(reference);

                Assert.True(
                    string.Equals(ownModule, referencedModule, StringComparison.Ordinal),
                    $"{project.Name} must not reference {reference} (cross-module dependency).");
            }
        }
    }

    [Fact]
    public void Api_references_all_module_infrastructure_projects()
    {
        var apiProject = AllProjects().Single(project => project.Name == "Wasla.Api");

        var expectedInfrastructureProjects = AllProjects()
            .Select(project => project.Name)
            .Where(name => name.EndsWith(".Infrastructure", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(12, expectedInfrastructureProjects.Length);

        foreach (var infrastructureProject in expectedInfrastructureProjects)
        {
            Assert.Contains(infrastructureProject, apiProject.ProjectReferences);
        }
    }

    [Fact]
    public void Domain_and_application_projects_do_not_reference_infrastructure_packages()
    {
        string[] forbiddenPrefixes =
        [
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "StackExchange.Redis",
            "Serilog",
        ];

        var projects = ModuleProjects("Domain").Concat(ModuleProjects("Application"));

        foreach (var project in projects)
        {
            foreach (var package in project.PackageReferences)
            {
                Assert.False(
                    forbiddenPrefixes.Any(prefix => package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
                    $"{project.Name} must not reference infrastructure package {package}.");
            }
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wasla.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate the repository root (Wasla.sln was not found above the test directory).");
    }

    private static IReadOnlyList<ProjectInfo> AllProjects()
    {
        var projectFiles = Directory
            .EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(RepoRoot, "tests"), "*.csproj", SearchOption.AllDirectories));

        return projectFiles.Select(ReadProject).ToArray();
    }

    private static ProjectInfo ReadProject(string path)
    {
        var document = XDocument.Load(path);
        var name = Path.GetFileNameWithoutExtension(path);

        var projectReferences = document.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => include is not null)
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .ToList();

        var packageReferences = document.Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => include is not null)
            .Select(include => include!)
            .ToList();

        return new ProjectInfo(name, projectReferences, packageReferences);
    }

    private static IEnumerable<ProjectInfo> ModuleProjects(string layerSuffix) =>
        AllProjects().Where(project =>
            project.Name.EndsWith($".{layerSuffix}", StringComparison.Ordinal) && IsModuleProject(project.Name));

    private static bool IsModuleProject(string projectName)
    {
        var parts = projectName.Split('.');

        return parts.Length == 3
            && parts[0] == "Wasla"
            && ModuleLayerSuffixes.Contains(parts[2]);
    }

    private static string? ModuleNameOf(string projectName) =>
        IsModuleProject(projectName) ? projectName.Split('.')[1] : null;

    private static string OwnModuleProject(string projectName, string targetLayer) =>
        $"Wasla.{ModuleNameOf(projectName)}.{targetLayer}";

    private sealed record ProjectInfo(string Name, List<string> ProjectReferences, List<string> PackageReferences);
}
