using Anvil;
using Anvil.Razor;
using Anvil.Sample;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAnvil();
builder.Services.AddAnvilOpenApi();
builder.Services.AddAnvilReadiness();
builder.Services.AddAnvilPersistence<SampleDbContext>(options => options.UseInMemoryDatabase("anvil-sample"));
builder.Services.AddAnvilIdentity<SampleUser, SampleDbContext>(identity =>
{
    identity.SignIn.RequireConfirmedEmail = false;
});
builder.Services.AddAnvilIdentityContracts();
builder.Services.AddAnvilTenancy(options =>
{
    options.Required = false;
    options.AllowDevelopmentHeader = true;
});
builder.Services.AddAnvilAudit<SampleDbContext>();
builder.Services.AddAnvilCompliance();
builder.Services.AddAnvilBackgroundJobs();
builder.Services.AddAnvilScheduling();
builder.Services.AddAnvilTransactionalOutbox<SampleDbContext>();
builder.Services.AddAnvilNotifications();
builder.Services.AddAnvilLocalStorage(options => options.RootPath = Path.Combine(AppContext.BaseDirectory, "sample-storage"));
builder.Services.AddAnvilImportExport();
builder.Services.AddAnvilWebhooks();
builder.Services.AddAnvilCaching();
builder.Services.AddAnvilObservability();
builder.Services.AddSingleton<ProductCatalog>();
builder.Services.AddSingleton<WorkspaceSettings>();

var app = builder.Build();
app.UseAnvilErrors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAnvilTenancy();
app.UseAnvil();
app.MapAnvil<Anvil.Sample.Components.App>();
app.MapAnvilIdentityEndpoints<SampleUser>();
app.MapAnvilGet("/api/health", async _ =>
{
    await Task.CompletedTask;
    return Results.Ok(new { status = "ok" });
}, "health");
app.MapAnvilGroup("/group").MapGet("/ping", () => Results.Ok(new { status = "group" }));
app.MapGet("/files/{*path}", (string? path) => Results.Ok(new { path }));
app.MapAnvilGet("/api/link", async request =>
{
    await Task.CompletedTask;
    return Results.Ok(new { url = request.RouteLink("health") });
});
app.MapAnvilFragmentGet<Anvil.Sample.Components.InventoryRows>(
    "/api/inventory/table",
    InventoryFragmentParameters,
    context => $"/products?search={Uri.EscapeDataString(context.Request.Query["search"].ToString())}");
app.MapAnvilFragmentPost<InventorySearchRequest, Anvil.Sample.Components.InventoryRows>(
    "/api/inventory/table",
    (context, request) =>
    {
        var catalog = context.HttpContext.RequestServices.GetRequiredService<ProductCatalog>();
        return Task.FromResult<object?>(new { Products = catalog.Search(request.Search).ToArray() });
    });
app.MapAnvilFragmentPost<CreateProductRequest, Anvil.Sample.Components.InventoryRows>(
    "/api/products",
    (context, request) =>
    {
        var catalog = context.HttpContext.RequestServices.GetRequiredService<ProductCatalog>();
        catalog.Add(request.Name, request.Description);
        return Task.FromResult<object?>(new { Products = catalog.Search(null).ToArray() });
    });
app.MapAnvilDelete("/api/products/{id:int}", async context =>
{
    if (!int.TryParse(context.RouteValues["id"]?.ToString(), out var id))
        return Results.NotFound();

    var catalog = context.HttpContext.RequestServices.GetRequiredService<ProductCatalog>();
    await Task.CompletedTask;
    return catalog.Remove(id)
        ? Results.Content(string.Empty, "text/html; charset=utf-8")
        : Results.NotFound();
});
app.MapAnvilFormPost<WorkspaceSettingsRequest>("/api/settings", async (context, request) =>
{
    var settings = context.HttpContext.RequestServices.GetRequiredService<WorkspaceSettings>();
    settings.Name = request.Name.Trim();
    settings.Timezone = request.Timezone;
    settings.WeeklySummaries = request.WeeklySummaries;
    await Task.CompletedTask;
    return Results.Content("<p class=\"form-status\" role=\"status\">Settings saved.</p>", "text/html; charset=utf-8");
});
app.MapAnvilFormPost<SignInRequest>("/api/account/sign-in", async (context, request) =>
{
    var sessions = context.HttpContext.RequestServices.GetRequiredService<AnvilSessionManager>();
    await sessions.StartAsync(new Dictionary<string, string>
    {
        ["email"] = request.Email
    }, context.RequestAborted);
    return Results.Redirect("/integrations");
});
app.MapAnvilFormPost<EmptyFormRequest>("/api/account/sign-out", async (context, _) =>
{
    var sessions = context.HttpContext.RequestServices.GetRequiredService<AnvilSessionManager>();
    await sessions.StopAsync(context.RequestAborted);
    return Results.Redirect("/integrations");
});
app.MapPost("/api/uploads", async (RequestContext context) =>
{
    var files = await context.ReadMultipartFilesAsync(maxBytes: 5 * 1024 * 1024);
    var names = string.Join(", ", files.Select(file => file.FileName));
    return Results.Content($"<p role=\"status\">Received: {System.Net.WebUtility.HtmlEncode(names)}</p>", "text/html; charset=utf-8");
});
app.MapAnvilFormPost<ContactRequest>("/api/contact", async (context, request) =>
{
    var mailer = context.HttpContext.RequestServices.GetRequiredService<AnvilMailer>();
    await mailer.SendAsync(new AnvilMailMessage(
        "noreply@anvil.test",
        request.To,
        "Anvil sample message",
        $"<p>{System.Net.WebUtility.HtmlEncode(request.Message)}</p>"), context.RequestAborted);
    return Results.Content("<p role=\"status\">Email queued.</p>", "text/html; charset=utf-8");
});
app.MapAnvilGet("/api/live", async _ =>
{
    await Task.CompletedTask;
    return Results.Content($"<span>Updated at {DateTimeOffset.UtcNow:O}</span>", "text/html");
});
app.MapAnvilGet("/api/cookie-redirect", async context =>
{
    context.SetCookie("redirect-check", "ok");
    await Task.CompletedTask;
    return Results.Redirect("/");
});
app.MapAnvilGet("/api/cookie-error", async context =>
{
    context.SetCookie("error-check", "ok");
    await Task.CompletedTask;
    return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
});
app.MapAnvilGet("/api/dashboard/activity", async _ =>
{
    await Task.CompletedTask;
    return Results.Content($"""
        <div class="activity-item"><span class="avatar avatar-violet">ML</span><div><p>Maya Lewis placed order #10482</p><small>Updated at {DateTimeOffset.UtcNow:HH:mm:ss} UTC</small></div></div>
        <div class="activity-item"><span class="avatar avatar-blue">AK</span><div><p>Alex Kim updated the Espresso listing</p><small>34 min ago</small></div></div>
        <div class="activity-item"><span class="avatar avatar-orange">JR</span><div><p>Jordan Reed refunded order #10477</p><small>1 hr ago</small></div></div>
        <div class="activity-item"><span class="avatar avatar-green">SP</span><div><p>Sam Patel joined the operations team</p><small>3 hrs ago</small></div></div>
        """, "text/html; charset=utf-8");
});
app.MapAnvilPost<DashboardRefreshRequest>("/api/dashboard/activity", async (_, _) =>
{
    await Task.CompletedTask;
    return Results.Content($"""
        <div class="activity-item"><span class="avatar avatar-violet">ML</span><div><p>Maya Lewis placed order #10482</p><small>Updated at {DateTimeOffset.UtcNow:HH:mm:ss} UTC</small></div></div>
        <div class="activity-item"><span class="avatar avatar-blue">AK</span><div><p>Alex Kim updated the Espresso listing</p><small>34 min ago</small></div></div>
        <div class="activity-item"><span class="avatar avatar-orange">JR</span><div><p>Jordan Reed refunded order #10477</p><small>1 hr ago</small></div></div>
        <div class="activity-item"><span class="avatar avatar-green">SP</span><div><p>Sam Patel joined the operations team</p><small>3 hrs ago</small></div></div>
        """, "text/html; charset=utf-8");
});
app.MapAnvilShard<DashboardRefreshRequest, Anvil.Sample.Components.ServerShardResult>("/api/showcase/shard", async (_, _) =>
{
    await Task.CompletedTask;
    return new { GeneratedAt = DateTimeOffset.UtcNow.ToString("HH:mm:ss") };
}, requireAuthentication: false);
app.MapAnvilSse("/api/events", _ => Events());
app.MapAnvilPost<EchoRequest>("/api/echo", async (_, request) =>
{
    await Task.CompletedTask;
    return Results.Ok(request);
});
app.MapAnvilFormPost<EchoRequest>("/api/form-echo", async (_, request) =>
{
    await Task.CompletedTask;
    return Results.Ok(request);
});
app.MapAnvilPut<EchoRequest>("/api/echo", async (_, request) =>
{
    await Task.CompletedTask;
    return Results.Ok(request);
});
app.MapAnvilPatch<EchoRequest>("/api/echo", async (_, request) =>
{
    await Task.CompletedTask;
    return Results.Ok(request);
});
app.MapAnvilDelete("/api/echo", async _ =>
{
    await Task.CompletedTask;
    return Results.NoContent();
});
app.MapAnvilProcedure<EchoRequest, ProcedureResponse>("/api/procedure", async (_, request) =>
{
    await Task.CompletedTask;
    return new ProcedureResponse(request.Message.ToUpperInvariant());
});
app.MapAnvilOpenApi();
app.MapAnvilManifest();
app.MapAnvilReadiness();
app.MapAnvilSitemap("/sitemap.xml", async _ =>
{
    await Task.CompletedTask;
    return (IReadOnlyList<AnvilSitemapEntry>)[
        new("https://example.test/", DateTimeOffset.UtcNow, "daily", 1.0m),
        new("https://example.test/products", DateTimeOffset.UtcNow, "weekly", 0.8m),
        new("https://example.test/framework", DateTimeOffset.UtcNow, "monthly", 0.5m)
    ];
});

static async IAsyncEnumerable<AnvilSseEvent> Events()
{
    yield return new AnvilSseEvent("connected", "status", "1");
    await Task.Yield();
    yield return new AnvilSseEvent("ready", "status", "2");
}

static async Task<object?> InventoryFragmentParameters(RequestContext context)
{
    var search = context.Request.Query["search"].FirstOrDefault();
    if (HttpMethods.IsPost(context.Request.Method))
        search = (await context.ReadFormAsync())["search"].FirstOrDefault();

    var catalog = context.HttpContext.RequestServices.GetRequiredService<ProductCatalog>();
    return new { Products = catalog.Search(search).ToArray() };
}

app.Run();

public partial class Program;

public sealed record EchoRequest(
    [property: Required]
    [property: MinLength(1)]
    string Message);

public sealed record ProcedureResponse(string Result);

public sealed record DashboardRefreshRequest;

public sealed record InventorySearchRequest(string? Search);

public sealed record CreateProductRequest(
    [property: Required]
    [property: MinLength(2)]
    string Name,
    [property: Required]
    [property: MinLength(2)]
    string Description);

public sealed record WorkspaceSettingsRequest(
    [property: Required]
    string Name,
    string Timezone,
    bool WeeklySummaries);

public sealed record SignInRequest(
    [property: Required]
    [property: EmailAddress]
    string Email,
    [property: Required]
    string Password);

public sealed record ContactRequest(
    [property: Required]
    [property: EmailAddress]
    string To,
    [property: Required]
    string Message);

public sealed record EmptyFormRequest;
