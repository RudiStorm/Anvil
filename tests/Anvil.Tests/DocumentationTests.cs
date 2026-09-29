namespace Anvil.Tests;

public sealed class DocumentationTests
{
    [Fact]
    public void Reference_document_covers_the_public_framework_surfaces()
    {
        var root = FindRepositoryRoot();
        var reference = File.ReadAllText(Path.Combine(root, "docs", "reference.md"));

        foreach (var heading in new[]
        {
            "## Application Setup",
            "## Razor Rendering",
            "## RequestContext",
            "## Routing and Links",
            "## API Endpoints",
            "## Forms and Partial Updates",
            "## Browser Runtime",
            "## Streaming, SSE, and WebSockets",
            "## Authentication and Authorization",
            "## Persistence and Migrations",
            "## Tenancy",
            "## Audit and Compliance",
            "## Background Jobs and Outbox",
            "## Operations Integrations",
            "## Mail, Localization, and Content",
            "## Production and Diagnostics",
            "## Testing"
        })
        {
            Assert.Contains(heading, reference, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Anvil.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
