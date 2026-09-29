namespace Anvil.Tests;

public sealed class AnvilDocumentationTests
{
    [Fact]
    public void Consolidated_documentation_contains_all_primary_agent_sections()
    {
        var root = FindRepositoryRoot();
        var documentation = File.ReadAllText(Path.Combine(root, "AnvilDocumentation.md"));

        foreach (var heading in new[]
        {
            "## Core Principles",
            "## Repository Layout",
            "## CLI Reference",
            "## Razor Pages and Components",
            "## RequestContext",
            "## Routing and Links",
            "## API Endpoints",
            "## Forms and Validation",
            "## Fragments and Shards",
            "## Browser Runtime",
            "## Authentication and Authorization",
            "## Persistence, Migrations, and Tenancy",
            "## Audit and Compliance",
            "## Jobs and Operations",
            "## Streaming, Mail, Localization, and Assets",
            "## Production",
            "## Testing and Agent Workflow"
        })
        {
            Assert.Contains(heading, documentation, StringComparison.Ordinal);
        }

        Assert.Contains("anvil make:page Reports", documentation, StringComparison.Ordinal);
        Assert.Contains("anvil make:shard Revenue", documentation, StringComparison.Ordinal);
        Assert.Contains("data-anvil-partial-form", documentation, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Anvil.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
