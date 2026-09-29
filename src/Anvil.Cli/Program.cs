using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anvil;

namespace Anvil.Cli;

public static class Program
{
    public static Task<int> Main(string[] args) => AnvilCli.RunAsync(args);
}

internal static class AnvilCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 1 && (args[0] is "--version" or "-v"))
        {
            Console.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
            return 0;
        }

        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "new" => CreateProject(args[1..]),
            "dev" => await RunDevelopmentServerAsync(args[1..]),
            "run" => await RunDevelopmentServerAsync(args[1..]),
            "ui" => ManageUi(args[1..]),
            "generate" => Generate(args[1..]),
            "docker" => GenerateDeploymentFiles(args[1..]),
            "release" => ReleaseDiagnostics(args[1..]),
            "build" => await RunDotnetCommandAsync("build", args[1..]),
            "publish" => await RunDotnetCommandAsync("publish", args[1..]),
            "check" => await RunDotnetCommandAsync("build", args[1..]),
            "migrate" => await MigrateAsync(args[1..]),
            "doctor" => Doctor(args[1..]),
            "mail" => Mail(args[1..]),
            "jobs" => Jobs(args[1..]),
            "schedule" => Schedule(args[1..]),
            "make" => Make(args[1..]),
            "make:endpoint" => Make(["endpoint", .. args[1..]]),
            "make:page" => Make(["page", .. args[1..]]),
            "make:shard" => Make(["shard", .. args[1..]]),
            "fmt" => await RunDotnetCommandAsync("format", args[1..]),
            "routes" => PrintRoutes(),
            "asset" => PrintAssetHelp(),
            _ => UnknownCommand(args[0])
        };
    }

    private static int CreateProject(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            Console.WriteLine("Usage: anvil new <name> [--output <directory>] [--profile default|identity] [--database sqlite|sqlserver|postgresql|mysql] [--package-source <directory>] [--force]");
            return args.Length == 0 ? 1 : 0;
        }

        var name = args[0];
        if (!IsValidProjectName(name))
        {
            Console.Error.WriteLine("Project name must be a single directory name without path separators.");
            return 1;
        }

        var output = Directory.GetCurrentDirectory();
        var force = false;
        var profile = "default";
        var database = AnvilDatabaseProvider.Sqlite;
        string? packageSource = null;

        for (var index = 1; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output" when index + 1 < args.Length:
                    output = Path.GetFullPath(args[++index]);
                    break;
                case "--force":
                    force = true;
                    break;
                case "--profile" when index + 1 < args.Length:
                    profile = args[++index].ToLowerInvariant();
                    if (profile is not "default" and not "identity")
                    {
                        Console.Error.WriteLine("Profile must be 'default' or 'identity'.");
                        return 1;
                    }
                    break;
                case "--database" when index + 1 < args.Length:
                    if (!TryParseDatabaseProvider(args[++index], out database))
                    {
                        Console.Error.WriteLine("Database must be 'sqlite', 'sqlserver', 'postgresql', or 'mysql'.");
                        return 1;
                    }
                    break;
                case "--package-source" when index + 1 < args.Length:
                    packageSource = Path.GetFullPath(args[++index]);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[index]}");
                    return 1;
            }
        }

        var projectDirectory = Path.Combine(output, name);
        if (Directory.Exists(projectDirectory) && Directory.EnumerateFileSystemEntries(projectDirectory).Any() && !force)
        {
            Console.Error.WriteLine($"Directory is not empty: {projectDirectory}");
            Console.Error.WriteLine("Use --force to generate into it anyway.");
            return 1;
        }

        Directory.CreateDirectory(projectDirectory);
        var namespaceName = ToIdentifier(name);
        var sourceRoot = FindSourceRoot(Directory.GetCurrentDirectory());
        var projectReferences = sourceRoot is null
            ? "    <PackageReference Include=\"Anvil\" Version=\"0.1.0\" />\n    <PackageReference Include=\"Anvil.Razor\" Version=\"0.1.0\" />"
            : $"    <ProjectReference Include=\"{RelativeProjectReference(projectDirectory, Path.Combine(sourceRoot, "src", "Anvil", "Anvil.csproj"))}\" />\n    <ProjectReference Include=\"{RelativeProjectReference(projectDirectory, Path.Combine(sourceRoot, "src", "Anvil.Razor", "Anvil.Razor.csproj"))}\" />";

        WriteFile(projectDirectory, $"{namespaceName}.csproj", ProjectFile(projectReferences, database));
        WriteFile(projectDirectory, "Program.cs", ProgramFile(namespaceName, profile == "identity", database));
        WriteFile(projectDirectory, "Data/AppDbContext.cs", DbContextFile(namespaceName, profile == "identity"));
        WriteFile(projectDirectory, ".anvil/profile.json", $"{{\n  \"profile\": \"{profile}\"\n}}\n");
        if (profile == "identity")
            WriteFile(projectDirectory, "Security/ApplicationUser.cs", ApplicationUserFile(namespaceName));
        WriteFile(projectDirectory, "appsettings.json", AppSettingsFile());
        WriteFile(projectDirectory, "Components/App.razor", AppFile(namespaceName));
        WriteFile(projectDirectory, "Components/Layout/MainLayout.razor", LayoutFile());
        WriteFile(projectDirectory, "Components/Pages/Home.razor", HomeFile());
        WriteFile(projectDirectory, "Components/_Imports.razor", ImportsFile(namespaceName));
        WriteFile(projectDirectory, "wwwroot/app.css", CssFile());
        if (packageSource is not null)
            WriteFile(projectDirectory, "NuGet.config", NuGetConfigFile(packageSource));
        WriteFile(projectDirectory, "Dockerfile", Dockerfile(namespaceName));
        WriteFile(projectDirectory, "compose.yaml", ComposeFile(namespaceName));
        WriteFile(projectDirectory, "DEPLOYMENT.md", DeploymentFile());
        WriteFile(projectDirectory, "appsettings.Production.json", ProductionSettingsFile());

        Console.WriteLine($"Created Anvil app at {projectDirectory}");
        Console.WriteLine($"  profile: {profile}");
        Console.WriteLine($"  database: {database.ToString().ToLowerInvariant()}");
        if (packageSource is not null) Console.WriteLine($"  package source: {packageSource}");
        Console.WriteLine($"  cd {projectDirectory}");
        Console.WriteLine("  anvil dev");
        return 0;
    }

    private static async Task<int> RunDevelopmentServerAsync(string[] args)
    {
        var project = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj").SingleOrDefault();
        var forwarded = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == "--project" && index + 1 < args.Length)
            {
                project = Path.GetFullPath(args[++index]);
                continue;
            }

            forwarded.Add(args[index]);
        }

        if (project is null)
        {
            Console.Error.WriteLine("No project file found. Use --project <path>.");
            return 1;
        }

        var projectDirectory = Path.GetDirectoryName(project)!;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = projectDirectory,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("watch");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("run");

        foreach (var argument in forwarded)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Console.Error.WriteLine("Unable to start dotnet watch.");
            return 1;
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static int ManageUi(string[] args)
    {
        if (args.Length == 0 || args[0] == "list")
        {
            Console.WriteLine("Available UI components:");
            Console.WriteLine("  button");
            Console.WriteLine("  card");
            Console.WriteLine("  input");
            return 0;
        }

        if (args[0] != "add" || args.Length < 2)
        {
            Console.Error.WriteLine("Usage: anvil ui [list|add <component>]");
            return 1;
        }

        var component = ToIdentifier(args[1]);
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "Components");
        var path = Path.Combine(directory, $"{component}.razor");
        if (File.Exists(path) && !args.Contains("--force", StringComparer.Ordinal))
        {
            Console.Error.WriteLine($"Component already exists: {path}");
            return 1;
        }

        Directory.CreateDirectory(directory);
        WriteFile(Directory.GetCurrentDirectory(), $"Components/{component}.razor", UiComponent(component));
        Console.WriteLine($"Added {path}");
        return 0;
    }

    private static int Generate(string[] args)
    {
        var check = args.Contains("--check", StringComparer.Ordinal);
        if (args.Any(argument => argument is not "--check" and not "-h" and not "--help"))
        {
            Console.Error.WriteLine("Usage: anvil generate [--check]");
            return 1;
        }

        var root = Directory.GetCurrentDirectory();
        var manifest = BuildRouteManifest(root);
        var path = Path.Combine(root, "obj", "anvil", "routes.json");
        var expected = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        var contractPath = Path.Combine(root, "obj", "anvil", "endpoint-manifest.json");
        var contract = BuildContractManifest(root, manifest);
        var expectedContract = JsonSerializer.Serialize(contract, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        if (check)
        {
            if (!File.Exists(path) || !string.Equals(File.ReadAllText(path), expected, StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Generated route manifest is stale. Run 'anvil generate'.");
                return 1;
            }

            if (!File.Exists(contractPath) || !string.Equals(File.ReadAllText(contractPath), expectedContract, StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Generated endpoint contract is stale. Run 'anvil generate'.");
                return 1;
            }

            Console.WriteLine("Generated files are current.");
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, expected, Encoding.UTF8);
        File.WriteAllText(contractPath, expectedContract, Encoding.UTF8);
        Console.WriteLine($"Generated route and endpoint manifests: {Path.GetRelativePath(root, path)}, {Path.GetRelativePath(root, contractPath)}");
        return 0;
    }

    private static IReadOnlyList<object> BuildRouteManifest(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
                .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .SelectMany(file => File.ReadLines(file)
                    .Where(line => line.TrimStart().StartsWith("@page ", StringComparison.Ordinal))
                    .Select(line => new
                    {
                        route = line.Trim()[6..].Trim().Trim('"'),
                        file = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/')
                    }))
                .OrderBy(entry => entry.route, StringComparer.Ordinal)
                .ThenBy(entry => entry.file, StringComparer.Ordinal)
                .Cast<object>()
                .ToArray()
            : [];

    private static IReadOnlyList<object> BuildContractManifest(string root, IReadOnlyList<object> routes)
    {
        var entries = routes.Select(route =>
        {
            var path = (string)route.GetType().GetProperty("route")!.GetValue(route)!;
            return new
            {
                id = $"page:get:{path}",
                feature = "pages",
                name = (string?)null,
                method = "GET",
                route = path,
                request = (string?)null,
                response = (string?)null,
                requiresAuthentication = false,
                permission = (string?)null,
                version = (string?)null,
                tags = new[] { "pages" },
                deprecated = false,
                idempotent = true,
                rateLimitPolicy = (string?)null
            };
        }).Cast<object>().ToList();

        var endpointPattern = new Regex("app\\.Map(Get|Post|Put|Patch|Delete)\\(\\\"([^\\\"]+)\\\"", RegexOptions.CultureInvariant);
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                     .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                         && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
        {
            foreach (Match match in endpointPattern.Matches(File.ReadAllText(file)))
            {
                var method = match.Groups[1].Value.ToUpperInvariant();
                var path = match.Groups[2].Value;
                entries.Add(new
                {
                    id = $"endpoint:{method.ToLowerInvariant()}:{path}",
                    feature = "api",
                    name = (string?)null,
                    method,
                    route = path,
                    request = (string?)null,
                    response = (string?)null,
                    requiresAuthentication = true,
                    permission = (string?)null,
                    version = (string?)null,
                    tags = Array.Empty<string>(),
                    deprecated = false,
                    idempotent = method is "GET" or "PUT",
                    rateLimitPolicy = (string?)null
                });
            }
        }

        return entries.OrderBy(entry => JsonSerializer.Serialize(entry), StringComparer.Ordinal).ToArray();
    }

    private static int Make(string[] args)
    {
        if (args.Length < 2 || IsHelp(args[0]) || args[1].StartsWith('-'))
        {
            Console.WriteLine("Usage: anvil make resource|endpoint|page|shard|crud <Name> [--force]");
            return args.Length > 0 && IsHelp(args[0]) ? 0 : 1;
        }

        var kind = args[0].ToLowerInvariant();
        var name = args[1];
        if (!IsTypeName(name))
        {
            Console.Error.WriteLine("Name must be a C# type name (letters, digits, and underscores only).");
            return 1;
        }

        var force = args.Contains("--force", StringComparer.Ordinal);
        return kind switch
        {
            "resource" => ScaffoldResource(name, force),
            "endpoint" => ScaffoldEndpoint(name, force),
            "page" => ScaffoldPage(name, force),
            "shard" => ScaffoldShard(name, force),
            "crud" => ScaffoldCrud(name, force),
            _ => InvalidMakeKind()
        };
    }

    private static int ScaffoldResource(string name, bool force)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Models", name + ".cs");
        return WriteScaffold(path, ResourceFile(name), force);
    }

    private static int ScaffoldEndpoint(string name, bool force)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Endpoints", name + "Endpoints.cs");
        var result = WriteScaffold(path, EndpointFile(name, ApplicationNamespace()), force);
        if (result == 0) EnsureEndpointMapped(name);
        return result;
    }

    private static int ScaffoldPage(string name, bool force)
    {
        var route = "/" + name.ToLowerInvariant();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Components", "Pages", name + ".razor");
        return WriteScaffold(path, PageFile(name, route), force);
    }

    private static int ScaffoldShard(string name, bool force)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Components", "Shards", name + "Shard.razor");
        return WriteScaffold(path, ShardFile(name), force);
    }

    private static int ScaffoldCrud(string name, bool force)
    {
        var root = Directory.GetCurrentDirectory();
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"Models/{name}.cs"] = ResourceFile(name),
            [$"Endpoints/{name}Endpoints.cs"] = EndpointFile(name, ApplicationNamespace()),
            [$"Components/Pages/{name}/Index.razor"] = CrudIndexFile(name),
            [$"Components/Pages/{name}/Edit.razor"] = CrudEditFile(name),
            [$"Components/Pages/{name}/Details.razor"] = CrudDetailsFile(name)
        };

         var existing = files.Keys.Select(relative => Path.Combine(root, relative))
             .Where(File.Exists).ToArray();
           var resourcePath = Path.Combine(root, "Models", name + ".cs");
           var endpointPath = Path.Combine(root, "Endpoints", name + "Endpoints.cs");
           var protectedExisting = existing.Where(path =>
               !string.Equals(Path.GetFullPath(path), Path.GetFullPath(resourcePath), StringComparison.OrdinalIgnoreCase)
               && !string.Equals(Path.GetFullPath(path), Path.GetFullPath(endpointPath), StringComparison.OrdinalIgnoreCase)).ToArray();
         if (protectedExisting.Length > 0 && !force)
         {
             Console.Error.WriteLine($"Scaffold file already exists: {Path.GetRelativePath(root, protectedExisting[0])}");
            Console.Error.WriteLine("Use --force to overwrite scaffold files.");
            return 1;
        }

         foreach (var file in files)
         {
             if (File.Exists(Path.Combine(root, file.Key)) && !force)
                 continue;
             WriteFile(root, file.Key, file.Value);
         }
        EnsureEndpointMapped(name);
        Console.WriteLine($"Created CRUD scaffold for {name}.");
        return 0;
    }

    private static void EnsureEndpointMapped(string name)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Program.cs");
        if (!File.Exists(path))
            return;

        var source = File.ReadAllText(path);
        var mapping = $"app.Map{name}Endpoints();";
        if (source.Contains(mapping, StringComparison.Ordinal))
            return;

        var marker = "app.Run();";
        if (source.Contains(marker, StringComparison.Ordinal))
            File.WriteAllText(path, source.Replace(marker, $"{mapping}\n        {marker}", StringComparison.Ordinal), Encoding.UTF8);
    }

    private static int WriteScaffold(string path, string content, bool force)
    {
        if (File.Exists(path) && !force)
        {
            Console.Error.WriteLine($"Scaffold file already exists: {path}");
            Console.Error.WriteLine("Use --force to overwrite it.");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Encoding.UTF8);
        Console.WriteLine($"Created {path}");
        return 0;
    }

    private static int InvalidMakeKind()
    {
        Console.Error.WriteLine("Usage: anvil make resource|endpoint|page|shard|crud <Name> [--force]");
        return 1;
    }

    private static async Task<int> MigrateAsync(string[] args)
    {
        var status = args.Contains("status", StringComparer.OrdinalIgnoreCase);
        var dryRun = args.Contains("--dry-run", StringComparer.Ordinal);
        var invalid = args.Where(argument => argument is not "status" and not "--dry-run" and not "--context")
            .ToArray();
        if (invalid.Any(argument => !argument.StartsWith('-'))
            || args.Count(argument => argument == "--context") > 1
            || (args.Contains("--context", StringComparer.Ordinal)
                && (Array.IndexOf(args, "--context") == args.Length - 1
                    || args[Array.IndexOf(args, "--context") + 1].StartsWith('-'))))
        {
            Console.Error.WriteLine("Usage: anvil migrate [status] [--dry-run] [--context <name>]");
            return 1;
        }
        var project = FindProject();
        if (project is null)
        {
            Console.Error.WriteLine("No project file found. Run this command from an application directory.");
            return 1;
        }

        var projectDirectory = Path.GetDirectoryName(project)!;
        var migrationsPath = Path.Combine(projectDirectory, "Migrations");
        var migrations = Directory.Exists(migrationsPath);
        var migrationCount = migrations ? Directory.EnumerateFiles(migrationsPath, "*.cs", SearchOption.TopDirectoryOnly).Count() : 0;
        var provider = DetectDatabaseProvider(
            string.Join("\n", Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.TopDirectoryOnly).Select(File.ReadAllText)),
            File.ReadAllText(project));
        var providerLabel = provider is null ? "provider unknown" : $"provider {provider.Value.ToString().ToLowerInvariant()}";
        if (dryRun)
        {
            Console.WriteLine(migrations
                ? status
                    ? $"Would list {migrationCount} migration C# files for {Path.GetFileName(project)} ({providerLabel})."
                    : $"Would apply {migrationCount} reviewed migration C# files for {Path.GetFileName(project)} ({providerLabel})."
                : "No Migrations directory found; nothing would be applied.");
            return 0;
        }

        if (!migrations)
        {
            Console.Error.WriteLine("No Migrations directory found. Create a reviewed migration with 'dotnet ef migrations add'.");
            return 1;
        }

        var efArguments = status
            ? new[] { "migrations", "list", "--project", project }
            : new[] { "database", "update", "--project", project };
        return await RunDotnetCommandAsync("ef", [.. efArguments, .. args.Where(argument => argument is not "--dry-run" and not "status")]);
    }

    private static int Doctor(string[] args)
    {
        var production = args.Contains("--production", StringComparer.Ordinal);
        var providerName = GetOption(args, "--provider");
        if (args.Any(argument => argument is not "--production" and not "--help" and not "-h" and not "--provider"
            && argument != providerName))
        {
            Console.Error.WriteLine("Usage: anvil doctor [--production] [--provider sqlite|sqlserver|postgresql|mysql]");
            return 1;
        }
        if (providerName is not null && !TryParseProvider(providerName, out _))
        {
            Console.Error.WriteLine("Provider must be sqlite, sqlserver, postgresql, or mysql.");
            return 1;
        }

        var root = Directory.GetCurrentDirectory();
        var project = FindProject();
        var blocking = false;
        void Result(string status, string message, bool blocks = false)
        {
            Console.WriteLine($"{status}: {message}");
            blocking |= blocks;
        }

        var projectText = project is null ? string.Empty : File.ReadAllText(project);
        var sourceText = string.Join("\n", Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains("\\obj\\") && !file.Contains("\\bin\\"))
            .Select(File.ReadAllText));
        var productionSettings = Path.Combine(root, "appsettings.Production.json");
        var productionText = File.Exists(productionSettings) ? File.ReadAllText(productionSettings) : string.Empty;
        var profileText = File.Exists(Path.Combine(root, ".anvil", "profile.json"))
            ? File.ReadAllText(Path.Combine(root, ".anvil", "profile.json")) : string.Empty;
        var composeText = File.Exists(Path.Combine(root, "compose.yaml")) ? File.ReadAllText(Path.Combine(root, "compose.yaml")) : string.Empty;
        Result(project is null ? "BLOCKING" : "PASS", project is null ? "No .csproj found." : "Project file found.", project is null);
        Result(File.Exists(Path.Combine(root, "appsettings.json")) ? "PASS" : "WARNING", "Configuration file " + (File.Exists(Path.Combine(root, "appsettings.json")) ? "found." : "not found."));
        var migrationFiles = Directory.Exists(Path.Combine(root, "Migrations"))
            ? Directory.EnumerateFiles(Path.Combine(root, "Migrations"), "*.cs", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var migrationsDirectory = Directory.Exists(Path.Combine(root, "Migrations"));
        Result(migrationsDirectory ? "PASS" : production ? "BLOCKING" : "WARNING", $"Migrations {(migrationsDirectory ? $"found ({migrationFiles} C# files)." : "not found.")}", production && !migrationsDirectory);
        var provider = DetectDatabaseProvider(sourceText, projectText);
        Result(provider is null ? "WARNING" : "PASS", provider is null ? "Database provider was not detected." : $"Database provider: {provider.Value} ({provider.Value.GetPackageId()}).");
        Result(File.Exists(Path.Combine(root, "Dockerfile")) ? "PASS" : production ? "BLOCKING" : "WARNING", "Dockerfile " + (File.Exists(Path.Combine(root, "Dockerfile")) ? "found." : "not found."), production && !File.Exists(Path.Combine(root, "Dockerfile")));
        Result(File.Exists(Path.Combine(root, "compose.yaml")) ? "PASS" : "WARNING", "Compose file " + (File.Exists(Path.Combine(root, "compose.yaml")) ? "found." : "not found."));
        Result(production && !File.Exists(productionSettings) ? "BLOCKING" : "PASS", production ? "Production configuration " + (File.Exists(productionSettings) ? "found." : "not found.") : "Development checks complete.", production && !File.Exists(productionSettings));

        if (production)
        {
            Result(sourceText.Contains("AddAnvilProduction", StringComparison.Ordinal) ? "PASS" : "BLOCKING", "Production security configuration " + (sourceText.Contains("AddAnvilProduction", StringComparison.Ordinal) ? "enabled." : "not enabled."), !sourceText.Contains("AddAnvilProduction", StringComparison.Ordinal));
            var healthConfigured = sourceText.Contains("MapAnvilHealthChecks", StringComparison.Ordinal)
                || sourceText.Contains("MapHealthChecks", StringComparison.Ordinal)
                || sourceText.Contains("MapAnvilReadiness", StringComparison.Ordinal);
            Result(healthConfigured ? "PASS" : "BLOCKING", "Health/readiness endpoints " + (healthConfigured ? "configured." : "not configured."), !healthConfigured);
            var httpsConfigured = sourceText.Contains("UseHsts", StringComparison.Ordinal)
                || sourceText.Contains("UseHttpsRedirection", StringComparison.Ordinal)
                || sourceText.Contains("UseAnvilProduction", StringComparison.Ordinal);
            Result(httpsConfigured ? "PASS" : "WARNING", "HTTPS/HSTS configuration " + (httpsConfigured ? "detected." : "not detected."));
            Result(productionText.Contains("Password", StringComparison.OrdinalIgnoreCase) || productionText.Contains("Secret", StringComparison.OrdinalIgnoreCase) || productionText.Contains("Token", StringComparison.OrdinalIgnoreCase) ? "BLOCKING" : "PASS", "Production settings do not contain obvious secret values.", productionText.Contains("Password", StringComparison.OrdinalIgnoreCase) || productionText.Contains("Secret", StringComparison.OrdinalIgnoreCase) || productionText.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Result(File.Exists(Path.Combine(root, "DEPLOYMENT.md")) ? "PASS" : "WARNING", "Deployment guidance " + (File.Exists(Path.Combine(root, "DEPLOYMENT.md")) ? "found." : "not found."));
        Result(projectText.Contains("net10.0", StringComparison.OrdinalIgnoreCase) ? "PASS" : "WARNING", "Target framework metadata inspected.");
        Result(sourceText.Contains("AddAnvilPersistence", StringComparison.Ordinal) ? "PASS" : "WARNING", "Persistence registration " + (sourceText.Contains("AddAnvilPersistence", StringComparison.Ordinal) ? "detected." : "not detected."));

        if (providerName is not null && TryParseProvider(providerName, out var requestedProvider))
        {
            var package = requestedProvider.GetPackageId();
            var method = requestedProvider.GetConfigurationMethod();
            var packageConfigured = projectText.Contains(package, StringComparison.OrdinalIgnoreCase);
            var providerConfigured = sourceText.Contains(method, StringComparison.Ordinal);
            Result(packageConfigured ? "PASS" : production ? "BLOCKING" : "WARNING", $"Database provider package {package} " + (packageConfigured ? "found." : "not found."), production && !packageConfigured);
            Result(providerConfigured ? "PASS" : production ? "BLOCKING" : "WARNING", $"Database provider configuration {method} " + (providerConfigured ? "detected." : "not detected."), production && !providerConfigured);
        }
        else
        {
            Result("WARNING", "Database provider was not specified; pass --provider for deployment diagnostics.");
        }

        if (profileText.Contains("identity", StringComparison.OrdinalIgnoreCase))
        {
            var identityConfigured = sourceText.Contains("AddAnvilIdentity", StringComparison.Ordinal);
            Result(identityConfigured ? "PASS" : "BLOCKING", "Identity profile " + (identityConfigured ? "is configured." : "is missing its Identity registration."), !identityConfigured);
        }
        Result(sourceText.Contains("AddAnvilSmtpMail", StringComparison.Ordinal) || sourceText.Contains("AddAnvilFileMail", StringComparison.Ordinal) || sourceText.Contains("AddAnvilMail", StringComparison.Ordinal)
            ? "PASS" : "WARNING", "Mail transport " + (sourceText.Contains("AddAnvil", StringComparison.Ordinal) && sourceText.Contains("Mail", StringComparison.Ordinal) ? "registration detected." : "not detected."));
        Result(sourceText.Contains("AddAnvilBackground", StringComparison.Ordinal) || File.Exists(Path.Combine(root, ".anvil", "jobs.json")) ? "PASS" : "WARNING", "Background jobs configuration inspected.");
        Result(sourceText.Contains("OpenTelemetry", StringComparison.Ordinal) || sourceText.Contains("AddAnvilOpenTelemetry", StringComparison.Ordinal) ? "PASS" : "WARNING", "Observability configuration inspected.");
        if (File.Exists(Path.Combine(root, "Dockerfile")))
            Result(composeText.Contains("health/live", StringComparison.Ordinal) ? "PASS" : "WARNING", "Container health probe " + (composeText.Contains("health/live", StringComparison.Ordinal) ? "configured." : "not configured."));
        }
        return blocking ? 1 : 0;
    }

    private static int Mail(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]) || args[0] == "status")
        {
            if (args.Any(argument => argument is not "status" and not "--help" and not "-h"))
            {
                Console.Error.WriteLine("Usage: anvil mail [status|preview <message.json>]");
                return 1;
            }

            var root = Directory.GetCurrentDirectory();
            var source = string.Join("\n", Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains("\\obj\\") && !file.Contains("\\bin\\"))
                .Select(File.ReadAllText));
            var smtp = source.Contains("AddAnvilSmtpMail", StringComparison.Ordinal);
            var file = source.Contains("AddAnvilFileMail", StringComparison.Ordinal);
            var memory = source.Contains("AddAnvilMail", StringComparison.Ordinal) && !smtp && !file;
            Console.WriteLine(smtp ? "PASS: SMTP mail transport detected." : file ? "WARNING: file mail transport detected (development only)." : memory ? "WARNING: in-memory mail transport detected (test/development only)." : "WARNING: no mail transport registration detected.");
            return 0;
        }

        if (args[0] != "preview" || args.Length != 2 || !File.Exists(args[1]))
        {
            Console.Error.WriteLine("Usage: anvil mail [status|preview <message.json>]");
            return 1;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(args[1]));
            var message = document.RootElement;
            var subject = message.GetProperty("subject").GetString() ?? string.Empty;
            var html = message.GetProperty("html").GetString() ?? string.Empty;
            Console.WriteLine($"<!doctype html><html><head><title>{System.Net.WebUtility.HtmlEncode(subject)}</title></head><body>{html}</body></html>");
            return 0;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException)
        {
            Console.Error.WriteLine("Message JSON must contain string properties 'subject' and 'html'.");
            return 1;
        }
    }

    private static string? GetOption(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool TryParseProvider(string value, out AnvilDatabaseProvider provider) => value.ToLowerInvariant() switch
    {
        "sqlite" => SetProvider(AnvilDatabaseProvider.Sqlite, out provider),
        "sqlserver" or "sql-server" => SetProvider(AnvilDatabaseProvider.SqlServer, out provider),
        "postgresql" or "postgres" => SetProvider(AnvilDatabaseProvider.PostgreSql, out provider),
        "mysql" => SetProvider(AnvilDatabaseProvider.MySql, out provider),
        _ => SetProvider(default, out provider, false)
    };

    private static bool SetProvider(AnvilDatabaseProvider value, out AnvilDatabaseProvider provider, bool valid = true)
    {
        provider = value;
        return valid;
    }

    private static int GenerateDeploymentFiles(string[] args)
    {
        var force = args.Contains("--force", StringComparer.Ordinal);
        if (args.Any(argument => argument is not "--force" and not "--help" and not "-h"))
        {
            Console.Error.WriteLine("Usage: anvil docker [--force]");
            return 1;
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Dockerfile"] = Dockerfile(ToIdentifier(Path.GetFileName(Directory.GetCurrentDirectory()))),
            ["compose.yaml"] = ComposeFile(ToIdentifier(Path.GetFileName(Directory.GetCurrentDirectory()))),
            ["DEPLOYMENT.md"] = DeploymentFile(),
            ["appsettings.Production.json"] = ProductionSettingsFile()
        };
        var existing = files.Keys.Where(file => File.Exists(file)).ToArray();
        if (existing.Length > 0 && !force)
        {
            Console.Error.WriteLine($"Deployment file already exists: {existing[0]}");
            Console.Error.WriteLine("Use --force to overwrite deployment templates.");
            return 1;
        }

        foreach (var file in files) WriteFile(Directory.GetCurrentDirectory(), file.Key, file.Value);
        Console.WriteLine("Generated Dockerfile, compose.yaml, appsettings.Production.json, and DEPLOYMENT.md.");
        return 0;
    }

    private static int ReleaseDiagnostics(string[] args)
    {
        if (args.Any(argument => argument is not "--check" and not "--help" and not "-h"))
        {
            Console.Error.WriteLine("Usage: anvil release --check");
            return 1;
        }

        return Doctor(["--production"]);
    }

    private static int Jobs(string[] args)
    {
        if (args.Length > 0 && args[0] is not "status" and not "list" and not "--help" and not "-h")
        {
            Console.Error.WriteLine("Usage: anvil jobs [status|list]");
            return 1;
        }

        var path = Path.Combine(Directory.GetCurrentDirectory(), ".anvil", "jobs.json");
        if (!File.Exists(path))
        {
            Console.WriteLine("No durable jobs found.");
            return 0;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var jobs = document.RootElement.EnumerateArray().ToArray();
        Console.WriteLine($"Durable jobs: {jobs.Length}");
        foreach (var item in jobs)
        {
            var job = item.GetProperty("job");
            Console.WriteLine($"{job.GetProperty("id").GetString()} {job.GetProperty("name").GetString()} {item.GetProperty("status").GetString()}");
        }
        return 0;
    }

    private static int Schedule(string[] args)
    {
        if (args.Length > 0 && args[0] is not "list" and not "--help" and not "-h")
        {
            Console.Error.WriteLine("Usage: anvil schedule list");
            return 1;
        }

        var path = Path.Combine(Directory.GetCurrentDirectory(), ".anvil", "jobs.schedules.json");
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "anvil.schedules.json");
        if (!File.Exists(path))
        {
            Console.WriteLine("No schedules configured.");
            return 0;
        }

        Console.WriteLine(File.ReadAllText(path));
        return 0;
    }

    private static string? FindProject() => Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj").SingleOrDefault();

    private static bool TryParseDatabaseProvider(string value, out AnvilDatabaseProvider provider)
    {
        provider = value.ToLowerInvariant() switch
        {
            "sqlite" => AnvilDatabaseProvider.Sqlite,
            "sqlserver" or "sql-server" => AnvilDatabaseProvider.SqlServer,
            "postgresql" or "postgres" => AnvilDatabaseProvider.PostgreSql,
            "mysql" => AnvilDatabaseProvider.MySql,
            _ => default
        };
        return value.ToLowerInvariant() is "sqlite" or "sqlserver" or "sql-server" or "postgresql" or "postgres" or "mysql";
    }

    private static AnvilDatabaseProvider? DetectDatabaseProvider(string source, string project)
    {
        if (source.Contains("AddAnvilSqlitePersistence", StringComparison.Ordinal) || project.Contains("EntityFrameworkCore.Sqlite", StringComparison.OrdinalIgnoreCase)) return AnvilDatabaseProvider.Sqlite;
        if (source.Contains("AddAnvilSqlServerPersistence", StringComparison.Ordinal) || project.Contains("EntityFrameworkCore.SqlServer", StringComparison.OrdinalIgnoreCase)) return AnvilDatabaseProvider.SqlServer;
        if (source.Contains("AddAnvilPostgreSqlPersistence", StringComparison.Ordinal) || project.Contains("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase)) return AnvilDatabaseProvider.PostgreSql;
        if (source.Contains("AddAnvilMySqlPersistence", StringComparison.Ordinal) || project.Contains("Pomelo.EntityFrameworkCore.MySql", StringComparison.OrdinalIgnoreCase)) return AnvilDatabaseProvider.MySql;
        return null;
    }

    private static string ApplicationNamespace()
    {
        var project = FindProject();
        return project is null ? "Anvil.Generated" : ToIdentifier(Path.GetFileNameWithoutExtension(project));
    }

    private static async Task<int> RunDotnetCommandAsync(string command, string[] args)
    {
        var project = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj").SingleOrDefault();
        if (project is null)
        {
            Console.Error.WriteLine("No project file found. Run this command from an application directory.");
            return 1;
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(project)!,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(command);
        if (command != "ef")
            startInfo.ArgumentList.Add(project);
        if (command == "publish" && !ContainsOutputOption(args))
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("Release");
            startInfo.ArgumentList.Add("-r");
            startInfo.ArgumentList.Add("win-x64");
            startInfo.ArgumentList.Add("--self-contained");
            startInfo.ArgumentList.Add("true");
            startInfo.ArgumentList.Add("-p:PublishSingleFile=true");
            startInfo.ArgumentList.Add("-p:IncludeNativeLibrariesForSelfExtract=true");
            startInfo.ArgumentList.Add("-p:PublishTrimmed=false");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add("D:\\dev-tools-path");
        }
        foreach (var argument in args) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo);
        if (process is null) return 1;
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static bool ContainsOutputOption(string[] args)
    {
        return args.Any(argument =>
            argument.Equals("-o", StringComparison.OrdinalIgnoreCase)
            || argument.Equals("--output", StringComparison.OrdinalIgnoreCase)
            || argument.StartsWith("-o:", StringComparison.OrdinalIgnoreCase)
            || argument.StartsWith("--output=", StringComparison.OrdinalIgnoreCase));
    }

    private static int PrintRoutes()
    {
        var root = Directory.GetCurrentDirectory();
        var routes = Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
                .SelectMany(file => File.ReadLines(file)
                    .Where(line => line.TrimStart().StartsWith("@page ", StringComparison.Ordinal))
                    .Select(line => (File: Path.GetRelativePath(root, file), Route: line.Trim()[6..].Trim().Trim('"'))))
                .ToArray()
            : [];

        if (routes.Length == 0)
        {
            Console.WriteLine("No Razor page routes found.");
            return 0;
        }

        foreach (var route in routes.OrderBy(route => route.Route, StringComparer.Ordinal))
            Console.WriteLine($"{route.Route} ({route.File})");
        return 0;
    }

    private static int PrintAssetHelp()
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        if (!Directory.Exists(directory))
        {
            Console.WriteLine("No wwwroot directory found.");
            return 0;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine(Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/'));
        return 0;
    }

    private static string? FindSourceRoot(string startingDirectory)
    {
        var directory = new DirectoryInfo(startingDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "Anvil", "Anvil.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string RelativeProjectReference(string projectDirectory, string projectFile)
    {
        return Path.GetRelativePath(projectDirectory, projectFile).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static void WriteFile(string projectDirectory, string relativePath, string content)
    {
        var path = Path.Combine(projectDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    private static string ToIdentifier(string name)
    {
        var builder = new StringBuilder();
        foreach (var character in name)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
            }
        }

        if (builder.Length == 0 || char.IsDigit(builder[0]))
        {
            builder.Insert(0, "App");
        }

        return builder.ToString();
    }

    private static bool IsValidProjectName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name is not "." and not ".."
        && !Path.IsPathRooted(name)
        && name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0;

    private static bool IsTypeName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && (char.IsLetter(name[0]) || name[0] == '_')
        && name.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static bool IsHelp(string value) => value is "--help" or "-h" or "help";

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Anvil - a minimal server-rendered .NET framework");
        Console.WriteLine($"Version: {typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  anvil new <name>       Create a new Anvil application");
        Console.WriteLine("  anvil dev               Run the current app with dotnet watch");
        Console.WriteLine("  anvil ui [list|add]     Manage editable UI components");
        Console.WriteLine("  anvil build|publish     Build or publish the current app");
        Console.WriteLine("  anvil docker            Generate deployment templates");
        Console.WriteLine("  anvil release --check   Run release diagnostics");
        Console.WriteLine("  anvil check|fmt         Check or format the current app");
        Console.WriteLine("  anvil doctor             Diagnose deployment readiness");
        Console.WriteLine("  anvil mail               Inspect or preview mail");
        Console.WriteLine("  anvil routes|asset      Inspect routes or asset guidance");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  anvil --version");
        Console.WriteLine("  anvil dev --project <path>");
        Console.WriteLine("  anvil new <name> --output <directory> [--profile default|identity] [--database sqlite|sqlserver|postgresql|mysql] [--package-source <directory>] [--force]");
        Console.WriteLine("  anvil make resource|endpoint|page|shard|crud <Name> [--force]");
        Console.WriteLine("  anvil make:page <Name> [--force]");
        Console.WriteLine("  anvil make:shard <Name> [--force]");
        Console.WriteLine("  anvil generate [--check]");
        Console.WriteLine("  anvil migrate [--dry-run]");
        Console.WriteLine("  anvil doctor [--production]");
        Console.WriteLine("  anvil doctor --provider sqlite|sqlserver|postgresql|mysql");
        Console.WriteLine("  anvil mail [status|preview <message.json>]");
        Console.WriteLine("  anvil docker [--force]");
        Console.WriteLine("  anvil release --check");
        Console.WriteLine("  anvil jobs status");
        Console.WriteLine("  anvil schedule list");
    }

    private static string ResourceFile(string name) => $$"""
        using System.ComponentModel.DataAnnotations;
        using Anvil;

        namespace {{ApplicationNamespace()}}.Models;

        /// <summary>Application-owned data for the {{name}} resource.</summary>
        public sealed class {{name}} : ITenantOwned
        {
            public int Id { get; set; }
            public string TenantId { get; set; } = string.Empty;
            [Required, StringLength(200)]
            public string Name { get; set; } = string.Empty;
        }
        """;

    private static string EndpointFile(string name, string namespaceName) => $$"""
        using Anvil;
        using System.ComponentModel.DataAnnotations;
        using {{namespaceName}}.Data;
        using {{namespaceName}}.Models;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.Extensions.DependencyInjection;

        public static class {{name}}Endpoints
        {
            public static void Map{{name}}Endpoints(this WebApplication app)
            {
                var contracts = app.Services.GetRequiredService<AnvilApiRegistry>();
                contracts.Add(new AnvilApiDescription("GET", "/{{name.ToLowerInvariant()}}s", "{{name.ToLowerInvariant()}}.list", Feature: "{{name}}", Permission: "{{name.ToLowerInvariant()}}.view", RequiresAuthentication: true, Tags: ["{{name}}"]));
                contracts.Add(new AnvilApiDescription("POST", "/{{name.ToLowerInvariant()}}s", "{{name.ToLowerInvariant()}}.create", RequestType: typeof({{name}}Create), ResponseType: typeof({{name}}), Feature: "{{name}}", Permission: "{{name.ToLowerInvariant()}}.create", RequiresAuthentication: true, Tags: ["{{name}}"]));
                contracts.Add(new AnvilApiDescription("PATCH", "/{{name.ToLowerInvariant()}}s/{id:int}", "{{name.ToLowerInvariant()}}.update", RequestType: typeof({{name}}Patch), ResponseType: typeof({{name}}), Feature: "{{name}}", Permission: "{{name.ToLowerInvariant()}}.update", RequiresAuthentication: true, Tags: ["{{name}}"]));
                contracts.Add(new AnvilApiDescription("DELETE", "/{{name.ToLowerInvariant()}}s/{id:int}", "{{name.ToLowerInvariant()}}.delete", Feature: "{{name}}", Permission: "{{name.ToLowerInvariant()}}.delete", RequiresAuthentication: true, Tags: ["{{name}}"]));

                app.MapGet("/{{name.ToLowerInvariant()}}s", async (string? search, AppDbContext db, TenantContext tenant, CancellationToken cancellationToken) =>
                    Results.Ok(await db.Set<{{name}}>().Where(item => item.TenantId == tenant.RequireTenantId() && (search == null || item.Name.Contains(search))).ToListAsync(cancellationToken)))
                    .RequireAuthorization()
                    .WithTags("{{name}}")
                    .WithName("{{name.ToLowerInvariant()}}.list");

                app.MapPost("/{{name.ToLowerInvariant()}}s", async ({{name}}Create request, AppDbContext db, TenantContext tenant, IAuditWriter audit, CancellationToken cancellationToken) =>
                {
                    var item = new {{name}} { Name = request.Name, TenantId = tenant.RequireTenantId() };
                    db.Add(item);
                    await db.SaveChangesAsync(cancellationToken);
                    await audit.WriteAsync(new AuditRequest("{{name}}", "create", item.Id.ToString(), new Dictionary<string, object?> { ["Name"] = item.Name }), cancellationToken);
                    return Results.Created("/{{name.ToLowerInvariant()}}s/" + item.Id, item);
                })
                .RequireAuthorization()
                .WithTags("{{name}}")
                .WithName("{{name.ToLowerInvariant()}}.create");

                app.MapPatch("/{{name.ToLowerInvariant()}}s/{id:int}", async (int id, {{name}}Patch request, AppDbContext db, TenantContext tenant, IAuditWriter audit, CancellationToken cancellationToken) =>
                {
                    var item = await db.Set<{{name}}>().SingleOrDefaultAsync(value => value.Id == id && value.TenantId == tenant.RequireTenantId(), cancellationToken);
                    if (item is null) return Results.NotFound();
                    if (request.Name is not null) item.Name = request.Name;
                    await db.SaveChangesAsync(cancellationToken);
                    await audit.WriteAsync(new AuditRequest("{{name}}", "update", id.ToString(), new Dictionary<string, object?> { ["Name"] = request.Name }), cancellationToken);
                    return Results.Ok(item);
                })
                .RequireAuthorization()
                .WithTags("{{name}}")
                .WithName("{{name.ToLowerInvariant()}}.update");

                app.MapDelete("/{{name.ToLowerInvariant()}}s/{id:int}", async (int id, AppDbContext db, TenantContext tenant, IAuditWriter audit, CancellationToken cancellationToken) =>
                {
                    var item = await db.Set<{{name}}>().SingleOrDefaultAsync(value => value.Id == id && value.TenantId == tenant.RequireTenantId(), cancellationToken);
                    if (item is null) return Results.NotFound();
                    db.Remove(item);
                    await db.SaveChangesAsync(cancellationToken);
                    await audit.WriteAsync(new AuditRequest("{{name}}", "delete", id.ToString()), cancellationToken);
                    return Results.NoContent();
                })
                .RequireAuthorization()
                .WithTags("{{name}}");
            }
        }

        public sealed record {{name}}Create([Required, StringLength(200)] string Name);
        public sealed record {{name}}Patch([StringLength(200)] string? Name);
        """;

    private static string PageFile(string name, string route) => $$"""
        @page "{{route}}"

        <PageTitle>{{name}} | Anvil</PageTitle>

        <section>
            <h1>{{name}}</h1>
            <p>This page was scaffolded by Anvil. Replace this content with the application workflow.</p>
        </section>
        """;

    private static string ShardFile(string name) => $$"""
        <section id="{{name.ToLowerInvariant()}}-shard" data-anvil-shard="/api/{{name.ToLowerInvariant()}}/shard" data-anvil-shard-target="{{name.ToLowerInvariant()}}-shard" aria-live="polite">
            <div data-anvil-shard-loading>Loading {{name}}...</div>
            @ChildContent
        </section>

        @code {
            [Parameter] public RenderFragment? ChildContent { get; set; }
        }
        """;

    private static string CrudIndexFile(string name) => $$"""
        @attribute [Microsoft.AspNetCore.Authorization.Authorize]
        @page "/{{name.ToLowerInvariant()}}s"

        <PageTitle>{{name}}s</PageTitle>
        <h1>{{name}}s</h1>
        <form method="get" data-anvil-partial-form>
            <label>Search <input name="search" /></label>
            <button type="submit">Search</button>
        </form>
        <div id="{{name.ToLowerInvariant()}}-table">
            <p>Load tenant-scoped {{name}} data from the generated endpoint.</p>
        </div>
        <a href="/{{name.ToLowerInvariant()}}s/new">Create {{name}}</a>
        """;

    private static string CrudEditFile(string name) => $$"""
        @attribute [Microsoft.AspNetCore.Authorization.Authorize]
        @page "/{{name.ToLowerInvariant()}}s/new"

        <PageTitle>New {{name}}</PageTitle>
        <h1>New {{name}}</h1>
        <form method="post" data-anvil-partial-form data-anvil-target="#{{name.ToLowerInvariant()}}-table">
            <label>Name <input name="Name" required /></label>
            <button type="submit">Save</button>
        </form>
        """;

    private static string CrudDetailsFile(string name) => $$"""
        @attribute [Microsoft.AspNetCore.Authorization.Authorize]
        @page "/{{name.ToLowerInvariant()}}s/{{"{id:int}"}}"

        <PageTitle>{{name}} details</PageTitle>
        <h1>{{name}} details</h1>
        <p>Load and authorize the resource identified by <code>id</code> here.</p>
        """;

    private static string ProjectFile(string references, AnvilDatabaseProvider database) => $$"""
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="{{database.GetPackageId()}}" Version="10.0.1" />
            <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.1">
              <PrivateAssets>all</PrivateAssets>
              <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            </PackageReference>
        {{references}}
          </ItemGroup>
        </Project>
        """;

    private static string ProgramFile(string namespaceName, bool identity, AnvilDatabaseProvider database)
    {
        var registration = database switch
        {
            AnvilDatabaseProvider.Sqlite => "Sqlite",
            AnvilDatabaseProvider.SqlServer => "SqlServer",
            AnvilDatabaseProvider.PostgreSql => "PostgreSql",
            AnvilDatabaseProvider.MySql => "MySql",
            _ => throw new ArgumentOutOfRangeException(nameof(database))
        };
        var configuration = database switch
        {
            AnvilDatabaseProvider.Sqlite => "options.UseSqlite(connectionString)",
            AnvilDatabaseProvider.SqlServer => "options.UseSqlServer(connectionString)",
            AnvilDatabaseProvider.PostgreSql => "options.UseNpgsql(connectionString)",
            AnvilDatabaseProvider.MySql => "options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))",
            _ => throw new ArgumentOutOfRangeException(nameof(database))
        };

        return $$"""
        using Anvil;
        using {{namespaceName}}.Components;
        using {{namespaceName}}.Data;
        using Microsoft.EntityFrameworkCore;

        var builder = WebApplication.CreateBuilder(args);
         builder.Services.AddAnvil();
         builder.Services.AddAnvilProduction();
         builder.Services.AddAnvil{{registration}}Persistence<AppDbContext>(
             builder.Configuration,
             (options, connectionString) => {{configuration}});
        builder.Services.AddAnvilTenancy(options => options.AllowDevelopmentHeader = true);
        builder.Services.AddAnvilAudit<AppDbContext>();
        builder.Services.AddAnvilOpenApi();
        {{(identity ? $"builder.Services.AddAnvilIdentity<{namespaceName}.Security.ApplicationUser, AppDbContext>();" : "")}}

        var app = builder.Build();
         app.UseAnvilProduction();
         app.UseStaticFiles();
        {{(identity ? "app.UseAuthentication();\n        app.UseAuthorization();" : "")}}
        app.UseAnvilTenancy();
        app.UseAnvil();
         app.MapAnvil<App>();
         app.MapAnvilHealthChecks();
        {{(identity ? $"app.MapAnvilIdentityEndpoints<{namespaceName}.Security.ApplicationUser>();" : "")}}
        app.MapAnvilOpenApi();
        app.MapAnvilManifest();
        app.Run();

        public partial class Program;
        """;
    }

    private static string DbContextFile(string namespaceName, bool identity) => $$"""
        using Anvil;
        using Microsoft.EntityFrameworkCore;
        {{(identity ? "using Microsoft.AspNetCore.Identity.EntityFrameworkCore;\n" : "")}}

        namespace {{namespaceName}}.Data;

        /// <summary>Application-owned EF Core context. Add entities and migrations here.</summary>
        // IdentityDbContext<ApplicationUser> uses the application-owned user type below.
        public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : {{(identity ? $"IdentityDbContext<{namespaceName}.Security.ApplicationUser>" : "DbContext")}}(options)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                modelBuilder.Entity<AuditEntry>().ConfigureAnvilAudit();
            }
        }
        """;

    private static string ApplicationUserFile(string namespaceName) => $$"""
        using Microsoft.AspNetCore.Identity;

        namespace {{namespaceName}}.Security;

        /// <summary>Application-owned Identity user. Add profile fields explicitly.</summary>
        public sealed class ApplicationUser : IdentityUser
        {
        }
        """;

    private static string AppSettingsFile() => """
        {
          "ConnectionStrings": {
            "DefaultConnection": "Data Source=app.db"
          }
        }
        """;

    private static string NuGetConfigFile(string packageSource) => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="anvil-local" value="{{System.Security.SecurityElement.Escape(packageSource)}}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
          </packageSources>
        </configuration>
        """;

    private static string AppFile(string namespaceName) => $$"""
        @using Microsoft.AspNetCore.Components.Routing
        @using Microsoft.AspNetCore.Components.Web
        @namespace {{namespaceName}}.Components

        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <base href="/" />
            <link rel="stylesheet" href="app.css" />
            <HeadOutlet />
        </head>
        <body>
            <Router AppAssembly="@typeof(App).Assembly">
                <Found Context="routeData">
                    <RouteView RouteData="@routeData" DefaultLayout="@typeof(Layout.MainLayout)" />
                </Found>
                <NotFound>
                    <LayoutView Layout="@typeof(Layout.MainLayout)">
                        <h1>Not found</h1>
                    </LayoutView>
                </NotFound>
            </Router>
            <script src="_framework/blazor.web.js"></script>
        </body>
        </html>
        """;

    private static string LayoutFile() => """
        @inherits LayoutComponentBase

        <header>
            <a href="/">Anvil</a>
        </header>
        <main>
            @Body
        </main>
        """;

    private static string HomeFile() => """
        @page "/"

        <PageTitle>Home</PageTitle>
        <h1>Hello from Anvil</h1>
        <p>Your server-rendered application is ready.</p>
        """;

    private static string ImportsFile(string namespaceName) => $$"""
        @using Microsoft.AspNetCore.Components
        @using Microsoft.AspNetCore.Components.Web
        @using {{namespaceName}}.Components.Layout
        """;

    private static string CssFile() => """
        body { font-family: system-ui, sans-serif; margin: 0 auto; max-width: 52rem; padding: 2rem; }
        header { border-bottom: 1px solid #d7dde5; margin-bottom: 2rem; padding-bottom: 1rem; }
        """;

    private static string Dockerfile(string applicationName) => $$"""
        # syntax=docker/dockerfile:1
        FROM mcr.microsoft.com/dotnet/sdk:10.0.1 AS build
        WORKDIR /src
        COPY . .
        RUN dotnet publish -c Release -o /app/publish

        FROM mcr.microsoft.com/dotnet/aspnet:10.0.1 AS runtime
        WORKDIR /app
        ENV ASPNETCORE_URLS=http://+:8080
        EXPOSE 8080
        RUN addgroup --system --gid 10001 anvil && adduser --system --uid 10001 --ingroup anvil anvil
        COPY --from=build /app/publish .
        RUN mkdir -p /app/data-protection-keys && chown -R anvil:anvil /app
        USER anvil
        ENTRYPOINT ["dotnet", "{{applicationName}}.dll"]
        """;

    private static string ComposeFile(string applicationName) => $$"""
        services:
          app:
            build: .
            image: {{applicationName.ToLowerInvariant()}}:local
            ports:
              - "8080:8080"
            environment:
              ASPNETCORE_ENVIRONMENT: Production
              ConnectionStrings__DefaultConnection: ${DEFAULT_CONNECTION_STRING:?set DEFAULT_CONNECTION_STRING}
            volumes:
              - data-protection-keys:/app/data-protection-keys
            healthcheck:
              test: ["CMD", "wget", "--spider", "--quiet", "http://localhost:8080/health/live"]
              interval: 30s
              timeout: 5s
              retries: 3

          mailpit:
            image: axllent/mailpit:v1.21.8
            profiles: [development]
            ports:
              - "1025:1025"
              - "8025:8025"

        volumes:
          data-protection-keys:
        """.Replace("APP_DLL", applicationName + ".dll", StringComparison.Ordinal);

    private static string ProductionSettingsFile() => """
        {
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "AllowedHosts": "*"
        }
        """;

    private static string DeploymentFile() => """
        # Production deployment

        1. Set `ConnectionStrings__DefaultConnection` and all mail/provider secrets through the deployment secret store, never in `appsettings.Production.json`.
        2. Persist `/app/data-protection-keys` across container replacements and restrict its volume to the application identity.
        3. Put the application behind an HTTPS reverse proxy. Configure trusted forwarded headers explicitly for that proxy.
        4. Map `/health/live` to the container liveness probe and `/health/ready` to the load balancer readiness probe.
        5. Run reviewed, committed EF migrations during release, before switching traffic. Back up the database first.
        6. Keep the previous image available for rollback and verify the health endpoints after deployment.

        `mailpit` is development-only. Use SMTP or an application-owned provider in production.
        """;

    private static string UiComponent(string component) => component.ToLowerInvariant() switch
    {
        "button" => "<button class=\"anvil-button\" @attributes=\"AdditionalAttributes\">@ChildContent</button>\n\n@code { [Parameter] public RenderFragment? ChildContent { get; set; } [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; } }\n",
        "card" => "<section class=\"anvil-card\">@ChildContent</section>\n\n@code { [Parameter] public RenderFragment? ChildContent { get; set; } }\n",
        "input" => "<input class=\"anvil-input\" @attributes=\"AdditionalAttributes\" />\n\n@code { [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; } }\n",
        _ => throw new InvalidOperationException($"Unknown UI component: {component}")
    };
}
