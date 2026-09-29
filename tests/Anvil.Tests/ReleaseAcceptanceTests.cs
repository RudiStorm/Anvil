using System.Diagnostics;
using System.IO.Compression;

namespace Anvil.Tests;

public sealed class ReleaseAcceptanceTests
{
    [Fact]
    public async Task Generated_project_passes_cli_acceptance_sequence()
    {
        var root = FindRepositoryRoot();
        var workspace = Path.Combine(root, ".release-acceptance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            var configuration = File.Exists(Path.Combine(root, "src", "Anvil.Cli", "bin", "Release", "net10.0", "Anvil.Cli.dll"))
                ? "Release"
                : "Debug";
            var cli = Path.Combine(root, "src", "Anvil.Cli", "bin", configuration, "net10.0", "Anvil.Cli.dll");
            Assert.True(File.Exists(cli), $"CLI must be built before acceptance tests: {cli}");

            await RunCli(cli, root, workspace, ["new", "MyApp", "--profile", "identity"]);
            var app = Path.Combine(workspace, "MyApp");
            await RunCli(cli, root, app, ["make", "resource", "Customer"]);
            await RunCli(cli, root, app, ["make", "crud", "Customer"]);
            await RunCli(cli, root, app, ["make:page", "Reports"]);
            await RunCli(cli, root, app, ["make:shard", "ReviewQueue"]);
            Assert.True(File.Exists(Path.Combine(app, "Endpoints", "ReviewQueueShardEndpoints.cs")));
            await RunCli(cli, root, app, ["generate"]);
            await RunCli(cli, root, app, ["generate", "--check"]);
            await RunCli(cli, root, app, ["check"]);
            await RunProcess("dotnet", ["build", Path.Combine(app, "MyApp.csproj")], app);
            await RunCli(cli, root, app, ["doctor", "--production"]);

            var program = await File.ReadAllTextAsync(Path.Combine(app, "Program.cs"));
            Assert.Contains("AddAnvilProduction", program);
            Assert.Contains("MapAnvilHealthChecks", program);
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task Packed_framework_packages_contain_metadata_and_runtime_assets()
    {
        var root = FindRepositoryRoot();
        var output = Path.Combine(Path.GetTempPath(), "anvil-pack", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);

        try
        {
            await RunProcess("dotnet", ["pack", Path.Combine(root, "src", "Anvil", "Anvil.csproj"), "--no-restore", "-c", "Release", "-o", output], root);
            await RunProcess("dotnet", ["pack", Path.Combine(root, "src", "Anvil.Razor", "Anvil.Razor.csproj"), "--no-restore", "-c", "Release", "-o", output], root);

            using var core = ZipFile.OpenRead(Path.Combine(output, "Anvil.0.1.0.nupkg"));
            using var razor = ZipFile.OpenRead(Path.Combine(output, "Anvil.Razor.0.1.0.nupkg"));
            Assert.Contains(core.Entries, entry => entry.FullName == "PACKAGE-NOTICE.md");
            Assert.Contains(razor.Entries, entry => entry.FullName == "PACKAGE-NOTICE.md");
            Assert.Contains(razor.Entries, entry => entry.FullName.EndsWith("htmx-4.0.0.min.js", StringComparison.Ordinal));
            Assert.DoesNotContain(razor.Entries, entry => entry.FullName.Contains("appsettings", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    private static Task RunCli(string cli, string root, string workingDirectory, string[] arguments) =>
        RunProcess("dotnet", [cli, .. arguments], workingDirectory, root);

    private static async Task RunProcess(string fileName, string[] arguments, string workingDirectory, string? sourceRoot = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        if (sourceRoot is not null) startInfo.Environment["ANVIL_SOURCE_ROOT"] = sourceRoot;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"{fileName} {string.Join(' ', arguments)} failed ({process.ExitCode}).\n{output}\n{error}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Anvil.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the Anvil solution root.");
    }
}
