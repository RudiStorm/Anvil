using Anvil.Cli;

namespace Anvil.Tests;

public sealed class CliTests
{
    [Theory]
    [InlineData("../outside")]
    [InlineData("..")] 
    [InlineData("C:\\outside")]
    public async Task New_rejects_path_traversal_project_names(string name)
    {
        var exitCode = await AnvilCli.RunAsync(["new", name]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Help_returns_success()
    {
        var exitCode = await AnvilCli.RunAsync(["--help"]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Version_returns_success()
    {
        Assert.Equal(0, await AnvilCli.RunAsync(["--version"]));
    }

    [Fact]
    public async Task Routes_and_assets_commands_inspect_the_current_application()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-cli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Components", "Pages"));
        Directory.CreateDirectory(Path.Combine(root, "wwwroot"));
        await File.WriteAllTextAsync(Path.Combine(root, "Components", "Pages", "Home.razor"), "@page \"/\"");
        await File.WriteAllTextAsync(Path.Combine(root, "wwwroot", "app.css"), "body{}");
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, await AnvilCli.RunAsync(["routes"]));
            Assert.Equal(0, await AnvilCli.RunAsync(["asset"]));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Routes_command_includes_operational_endpoints()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-routes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), "app.MapAnvilOpenApi(); app.MapAnvilManifest(); app.MapAnvilHealthChecks(); app.MapAnvilFragmentGet<Rows>(\"/rows\", _ => new { });");
        var originalDirectory = Directory.GetCurrentDirectory();
        var originalOutput = Console.Out;
        using var output = new StringWriter();

        try
        {
            Directory.SetCurrentDirectory(root);
            Console.SetOut(output);
            Assert.Equal(0, await AnvilCli.RunAsync(["routes"]));
        }
        finally
        {
            Console.SetOut(originalOutput);
            Directory.SetCurrentDirectory(originalDirectory);
            Directory.Delete(root, recursive: true);
        }

        var routes = output.ToString();
        Assert.Contains("/openapi.json", routes);
        Assert.Contains("/anvil.contract.json", routes);
        Assert.Contains("/health/live", routes);
        Assert.Contains("/health/ready", routes);
        Assert.Contains("/rows", routes);
    }

    [Fact]
    public async Task Generate_writes_a_deterministic_manifest_and_detects_stale_routes()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-generate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Components"));
        await File.WriteAllTextAsync(Path.Combine(root, "Components", "Home.razor"), "@page \"/\"");
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, await AnvilCli.RunAsync(["generate"]));
            Assert.Equal(0, await AnvilCli.RunAsync(["generate", "--check"]));
            var contract = await File.ReadAllTextAsync(Path.Combine(root, "obj", "anvil", "endpoint-manifest.json"));
            Assert.Contains("page:get:/", contract);
            await File.AppendAllTextAsync(Path.Combine(root, "Components", "Home.razor"), "\n@page \"/home\"");
            Assert.Equal(1, await AnvilCli.RunAsync(["generate", "--check"]));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Make_crud_creates_editable_resource_endpoint_and_pages_without_overwriting()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-scaffold", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, await AnvilCli.RunAsync(["make", "crud", "Customer"]));
            Assert.True(File.Exists(Path.Combine(root, "Models", "Customer.cs")));
            Assert.True(File.Exists(Path.Combine(root, "Endpoints", "CustomerEndpoints.cs")));
            Assert.True(File.Exists(Path.Combine(root, "Components", "Pages", "Customer", "Index.razor")));
            Assert.Contains("data-anvil-partial-form", await File.ReadAllTextAsync(Path.Combine(root, "Components", "Pages", "Customer", "Index.razor")));
            Assert.Equal(1, await AnvilCli.RunAsync(["make", "resource", "Customer"]));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Make_page_and_shard_create_editable_razor_bases()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-page-shard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, await AnvilCli.RunAsync(["make:page", "Reports"]));
            Assert.Equal(0, await AnvilCli.RunAsync(["make:shard", "Revenue"]));

            var page = await File.ReadAllTextAsync(Path.Combine(root, "Components", "Pages", "Reports.razor"));
            var shard = await File.ReadAllTextAsync(Path.Combine(root, "Components", "Shards", "RevenueShard.razor"));
            var endpoint = await File.ReadAllTextAsync(Path.Combine(root, "Endpoints", "RevenueShardEndpoints.cs"));
            Assert.Contains("@page \"/reports\"", page);
            Assert.Contains("data-anvil-shard=\"/api/revenue/shard\"", shard);
            Assert.Contains("@ChildContent", shard);
            Assert.Contains("MapRevenueShardEndpoints", endpoint);
            Assert.Contains("MapAnvilShard", endpoint);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Migrate_dry_run_is_safe_without_running_dotnet_ef()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-migrate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "Sample.csproj"), "<Project />");
        Directory.CreateDirectory(Path.Combine(root, "Migrations"));
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, await AnvilCli.RunAsync(["migrate", "--dry-run"]));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task New_generates_an_application_owned_sqlite_context_and_configuration()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-new", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, await AnvilCli.RunAsync(["new", "Store"]));
            var project = Path.Combine(root, "Store", "Store.csproj");
            var program = await File.ReadAllTextAsync(Path.Combine(root, "Store", "Program.cs"));
            Assert.Contains("Microsoft.EntityFrameworkCore.Sqlite", await File.ReadAllTextAsync(project));
            Assert.Contains("AddAnvilSqlitePersistence<AppDbContext>", program);
            Assert.True(File.Exists(Path.Combine(root, "Store", "Data", "AppDbContext.cs")));
            Assert.True(File.Exists(Path.Combine(root, "Store", "appsettings.json")));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("sqlserver", "Microsoft.EntityFrameworkCore.SqlServer", "AddAnvilSqlServerPersistence<AppDbContext>", "UseSqlServer")]
    [InlineData("postgresql", "Npgsql.EntityFrameworkCore.PostgreSQL", "AddAnvilPostgreSqlPersistence<AppDbContext>", "UseNpgsql")]
    [InlineData("mysql", "Pomelo.EntityFrameworkCore.MySql", "AddAnvilMySqlPersistence<AppDbContext>", "UseMySql")]
    public async Task New_generates_explicit_provider_template(string provider, string package, string registration, string method)
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-provider", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, await AnvilCli.RunAsync(["new", "Store", "--database", provider]));
            var app = Path.Combine(root, "Store");
            Assert.Contains(package, await File.ReadAllTextAsync(Path.Combine(app, "Store.csproj")));
            var program = await File.ReadAllTextAsync(Path.Combine(app, "Program.cs"));
            Assert.Contains(registration, program);
            Assert.Contains(method, program);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task New_can_configure_a_local_framework_package_source()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-package-source", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, await AnvilCli.RunAsync(["new", "PackageApp", "--package-source", "D:\\dev-tools-path\\packages"]));
            var config = await File.ReadAllTextAsync(Path.Combine(root, "PackageApp", "NuGet.config"));
            Assert.Contains("D:\\dev-tools-path\\packages", config);
            Assert.Contains("Anvil.Razor", await File.ReadAllTextAsync(Path.Combine(root, "PackageApp", "PackageApp.csproj")));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Identity_profile_generates_persistence_auth_tenancy_audit_and_openapi_boundaries()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-identity", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, await AnvilCli.RunAsync(["new", "Portal", "--profile", "identity"]));
            var app = Path.Combine(root, "Portal");
            var program = await File.ReadAllTextAsync(Path.Combine(app, "Program.cs"));
            var context = await File.ReadAllTextAsync(Path.Combine(app, "Data", "AppDbContext.cs"));

            Assert.Contains("AddAnvilIdentity<Portal.Security.ApplicationUser, AppDbContext>", program);
            Assert.Contains("UseAuthentication", program);
            Assert.Contains("AddAnvilTenancy", program);
            Assert.Contains("options.Required = false", program);
            Assert.Contains("AddAnvilAudit<AppDbContext>", program);
            Assert.Contains("MapAnvilOpenApi", program);
            Assert.Contains("IdentityDbContext<ApplicationUser>", context);
            Assert.True(File.Exists(Path.Combine(app, "Security", "ApplicationUser.cs")));
            Assert.Contains("identity", await File.ReadAllTextAsync(Path.Combine(app, ".anvil", "profile.json")));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Crud_scaffold_is_tenant_safe_authorized_audited_and_supports_patch_updates()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-crud-contract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "Portal.csproj"), "<Project />");
            await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), "app.Run();");
            Assert.Equal(0, await AnvilCli.RunAsync(["make", "crud", "Customer"]));
            var endpoint = await File.ReadAllTextAsync(Path.Combine(root, "Endpoints", "CustomerEndpoints.cs"));
            var model = await File.ReadAllTextAsync(Path.Combine(root, "Models", "Customer.cs"));

            Assert.Contains("RequireAuthorization", endpoint);
            Assert.Contains("RequireTenantId", endpoint);
            Assert.Contains("IAuditWriter", endpoint);
            Assert.Contains("MapPatch", endpoint);
            Assert.Contains("ITenantOwned", model);
            Assert.Contains("TenantId", model);
            Assert.Contains("data-anvil-partial-form", await File.ReadAllTextAsync(Path.Combine(root, "Components", "Pages", "Customer", "Index.razor")));
            Assert.Contains("MapCustomerEndpoints", await File.ReadAllTextAsync(Path.Combine(root, "Program.cs")));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Docker_generates_pinned_non_root_templates_without_overwriting()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-docker", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(0, await AnvilCli.RunAsync(["docker"]));
            var dockerfile = await File.ReadAllTextAsync(Path.Combine(root, "Dockerfile"));
            var compose = await File.ReadAllTextAsync(Path.Combine(root, "compose.yaml"));
            Assert.Contains("USER anvil", dockerfile);
            Assert.Contains("10.0.1", dockerfile);
            Assert.Contains("axllent/mailpit:v1.21.8", compose);
            Assert.Equal(1, await AnvilCli.RunAsync(["docker"]));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Production_doctor_blocks_missing_security_and_release_configuration()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-doctor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Migrations"));
        await File.WriteAllTextAsync(Path.Combine(root, "App.csproj"), "<Project><TargetFramework>net10.0</TargetFramework></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "appsettings.Production.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(root, "Dockerfile"), "FROM test");
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            Assert.Equal(1, await AnvilCli.RunAsync(["doctor", "--production"]));
            await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), "builder.Services.AddAnvilProduction(); app.MapAnvilHealthChecks();");
            await File.WriteAllTextAsync(Path.Combine(root, "DEPLOYMENT.md"), "guidance");
            Assert.Equal(0, await AnvilCli.RunAsync(["release", "--check"]));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Mail_status_and_preview_are_safe_and_deterministic()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-mail-cli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), "builder.Services.AddAnvilSmtpMail(_ => { });");
            await File.WriteAllTextAsync(Path.Combine(root, "message.json"), "{\"subject\":\"Welcome <now>\",\"html\":\"<p>Hello</p>\"}");
            Assert.Equal(0, await AnvilCli.RunAsync(["mail", "status"]));
            Assert.Equal(0, await AnvilCli.RunAsync(["mail", "preview", "message.json"]));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Doctor_provider_diagnostics_require_the_matching_package_and_configuration()
    {
        var root = Path.Combine(Path.GetTempPath(), "anvil-provider-doctor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "App.csproj"), "<Project><PackageReference Include=\"Microsoft.EntityFrameworkCore.Sqlite\" /></Project>");
            await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), "builder.Services.AddAnvilProduction(); builder.Services.AddAnvilSqlitePersistence<AppDbContext>(builder.Configuration, (o, c) => o.UseSqlite(c)); app.MapAnvilHealthChecks();");
            await File.WriteAllTextAsync(Path.Combine(root, "appsettings.Production.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(root, "Dockerfile"), "FROM test");
            await File.WriteAllTextAsync(Path.Combine(root, "DEPLOYMENT.md"), "guidance");
            Directory.CreateDirectory(Path.Combine(root, "Migrations"));
            await File.WriteAllTextAsync(Path.Combine(root, "Migrations", "Initial.cs"), "// reviewed");
            Assert.Equal(0, await AnvilCli.RunAsync(["doctor", "--production", "--provider", "sqlite"]));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, recursive: true);
        }
    }
}
