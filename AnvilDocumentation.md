# Anvil Documentation

Anvil is a .NET 10 server-rendered application framework built on ASP.NET Core
and Razor. It is designed as a C# and Razor counterpart to the server-first
ideas behind Topcoat and as the server-rendered evolution of the Dotisan
application platform.

This file is the consolidated reference for developers and coding agents. It is
the single document to consult before adding a feature, endpoint, page,
fragment, authentication flow, background job, or production integration.

## Core Principles

- Razor-rendered HTML is the default output.
- ASP.NET Core hosting, middleware, dependency injection, configuration, health checks, and HTTP primitives remain standard .NET.
- No Vue, Vite, Node.js build pipeline, or separate frontend application is required.
- Browser behavior is explicit: signals, events, server calls, fragments, shards, SSE, or WebSockets.
- Server validation and authorization are always authoritative.
- Persistence and domain rules remain application-owned.
- Generated code is editable source code, not an opaque runtime.
- Internal links use Anvil route helpers instead of string concatenation.
- Optional features are opt-in and must not make the core path complicated.

## Repository Layout

```text
src/Anvil/             Core framework, hosting, routing, security, operations
src/Anvil.Razor/       Razor components and optional browser runtime
src/Anvil.Cli/         anvil CLI and project scaffolding
samples/Anvil.Sample/  Complete admin dashboard/reference application
tests/Anvil.Tests/     Unit, integration, contract, CLI, and sample tests
docs/                  Focused guides
build plan.md          Feature and migration source of truth
```

The reference application is intentionally application-shaped. It demonstrates
features without pretending that in-memory sample data is production storage.

Run it with:

```powershell
dotnet run --project samples/Anvil.Sample
```

Important sample routes:

| Route | Purpose |
| --- | --- |
| `/` | Documentation homepage |
| `/overview` | Admin dashboard |
| `/products` | Inventory CRUD and partial search |
| `/products/{id}` | Typed route parameter detail page |
| `/team` | Server-rendered team page |
| `/settings` | Validated settings mutation |
| `/framework` | Feature showcase |
| `/identity` | Identity and MFA guide |
| `/operations` | Jobs, audit, tenancy, and operation guide |
| `/integrations` | Sessions, uploads, and mail |
| `/streaming` | Streaming and live-region example |
| `/errors` | Error boundary example |
| `/openapi.json` | Generated OpenAPI output |
| `/anvil.contract.json` | Canonical endpoint manifest |
| `/sitemap.xml` | Sitemap output |
| `/health/ready` | Readiness check |

## Create an Application

```powershell
dotnet tool install --global Raukeld.Anvil.Cli
anvil new Portal
cd Portal
anvil dev
```

The generated application is an ordinary ASP.NET Core web project:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAnvil();

var app = builder.Build();
app.UseAnvilErrors();
app.UseStaticFiles();
app.UseAnvil();
app.MapAnvil<Components.App>();
app.Run();
```

`anvil new` defaults to the identity profile, so the generated application has
Identity, EF Core, tenancy, audit, jobs, mail, observability, and production
boundaries immediately. Use `--profile default` for the deliberately minimal
profile. Use `--database sqlite|sqlserver|postgresql|mysql` to select the
persistence provider.

## CLI Reference

### Application Commands

```text
anvil new <Name> [--profile default|identity] [--database <provider>] [--package-source <directory>] [--no-restore]
anvil dev [--project <path>]
anvil run [--project <path>]
anvil build
anvil publish
anvil check
anvil fmt
anvil doctor [--production]
anvil generate [--check]
anvil migrate [--dry-run]
anvil docker
anvil release --check
anvil --version
```

`anvil doctor --production` is read-only and reports PASS, WARNING, or BLOCKING
conditions for migrations, Identity, providers, mail, jobs, deployment files,
health endpoints, observability, and production configuration.

`anvil check` runs a real `dotnet build` for the current project, including
restore when required. It is the fast generated-project compilation check;
`anvil generate --check` verifies generated route and endpoint metadata.

### Scaffolding Commands

```text
anvil make endpoint Health
anvil make:page Reports
anvil make:shard Revenue
anvil make resource Customer
anvil make crud Customer
anvil ui list
anvil ui add button
```

The colon forms are convenience aliases. The generated files belong to the
application and must be edited normally.

`make endpoint` creates `Endpoints/HealthEndpoints.cs` as an application-owned
endpoint base. Add request records, response records, validators,
authorization, and handler logic.

`make:page Reports` creates `Components/Pages/Reports.razor` with a route,
title, heading, and editable placeholder content.

`make:shard Revenue` creates `Components/Shards/RevenueShard.razor` with a
target ID, `data-anvil-shard` URL, loading marker, child content, and
`Endpoints/RevenueShardEndpoints.cs` with the matching
`/api/revenue/shard` server endpoint. The endpoint is added to `Program.cs`.

`make resource Customer` creates a tenant-owned model and resource boundary.
`make crud Customer` adds model, endpoint, list, edit, and details Razor files.

`anvil new` restores packages, restores the pinned EF tool, creates and applies
the initial EF migration, generates route/endpoint manifests, and builds the
project before reporting success. Identity projects include organized
`Components/Pages/Public`, `Components/Pages/Auth`, and `Components/Pages/App`
routes with explicit authorization metadata. Use `--no-restore` only for
offline file generation.

The scaffold also copies a complete editable shadcn-style Razor control catalog
to `Components/Controls`. Visit `/components` to see every generated control in
use. Each control is ordinary application-owned Razor source with theme tokens
in `wwwroot/app.css`; users can customize or remove controls independently.
Advanced interactions use semantic HTML fallbacks and the optional,
dependency-free `wwwroot/anvil-controls.js` helper. No Node toolchain is needed.

`anvil generate` creates deterministic route and endpoint manifests under
`obj/anvil`. `anvil generate --check` fails when generated metadata is stale.

When running from an installed CLI, the generated project uses `Raukeld.Anvil` and
`Raukeld.Anvil.Razor` package reference. Use `Publish-AnvilPackages.ps1` to create a
local package feed, or publish both packages to your NuGet feed. For a local
feed during development:

```powershell
.\Publish-AnvilPackages.ps1
anvil new Portal --package-source D:\dev-tools-path\packages
```

The generated project receives a `NuGet.config` with the selected local feed
and NuGet.org. Source-checkout generation continues to use project references.

### Persistence Commands

```text
anvil migrate status
anvil migrate --dry-run
anvil migrate
```

Migration authoring remains standard EF Core:

```text
dotnet ef migrations add InitialCreate --project src/MyApp
anvil migrate
```

Generated applications include `.config/dotnet-tools.json` with a pinned
`dotnet-ef` version. Run `dotnet tool restore` before authoring migrations so
the local tool matches the EF runtime instead of an older global tool.

### Operational Commands

```text
anvil jobs status
anvil schedule list
anvil mail status
anvil mail preview message.json
```

## Razor Pages and Components

Create a page under `Components/Pages`:

```razor
@page "/customers/{Id:int}"
@inject CustomerService Customers

<PageTitle>Customer @Id</PageTitle>

@if (customer is null)
{
    <h1>Customer not found</h1>
}
else
{
    <h1>@customer.Name</h1>
}

@code {
    [Parameter] public int Id { get; set; }
    private Customer? customer;

    protected override async Task OnParametersSetAsync()
        => customer = await Customers.FindAsync(Id);
}
```

Razor supports normal C# control flow, layouts, parameters, child content,
dependency injection, `PageTitle`, `HeadContent`, asynchronous lifecycle
methods, and `AnvilErrorBoundary`.

Values are HTML-escaped by default. Use `AnvilMarkup.Trusted` only for content
that the application has explicitly sanitized and approved.

## RequestContext

Inject `RequestContext` into handlers or services for request-scoped access:

```csharp
public sealed class CurrentRequest(RequestContext request)
{
    public string? Search => request.Request.Query["search"];
    public CancellationToken Cancellation => request.RequestAborted;
}
```

It exposes:

- `HttpContext`, `Request`, and `Response`
- route values, query values, headers, cookies, and client IP
- request cancellation
- JSON, form, raw body, and multipart helpers
- redirects and response status handling
- scoped values and application services
- request memoization through `MemoizeAsync`

Never place secrets, sessions, services, or database contexts in browser-visible
state.

## Routing and Links

Razor `@page` directives discover page routes. API endpoints use ASP.NET Core
route patterns. Explicit route metadata uses `[AnvilRoute]`.

Use typed routes for reusable links:

```csharp
public sealed record CustomerRoute(int Id);

static readonly AnvilRoute<CustomerRoute> Customer =
    new("/customers/{id:int}");

var href = Customer.Link(new CustomerRoute(42));
```

Links support route values, query values, fragments, absolute URLs, named
routes, base URLs, and current-route checks.

## API Endpoints

Anvil wraps Minimal APIs without replacing them:

```csharp
public sealed record CreateCustomerRequest(
    [property: Required] string Name,
    [property: EmailAddress] string Email);

app.MapAnvilPost<CreateCustomerRequest>(
    "/api/customers",
    async (RequestContext context, CreateCustomerRequest request) =>
    {
        var customer = await service.CreateAsync(request, context.RequestAborted);
        return Results.Created($"/api/customers/{customer.Id}", customer);
    },
    name: "customers.create");
```

Available endpoint helpers:

- `MapAnvilGet`
- `MapAnvilPost<TRequest>`
- `MapAnvilFormPost<TRequest>`
- `MapAnvilPut<TRequest>`
- `MapAnvilPatch<TRequest>`
- `MapAnvilDelete`
- `MapAnvilProcedure<TRequest,TResponse>`
- `MapAnvilFragmentGet<TComponent>`
- `MapAnvilFragmentPost<TRequest,TComponent>`
- `MapAnvilShard<TRequest,TComponent>`
- `MapAnvilSse`
- `MapAnvilWebSocket`
- `MapAnvilSitemap`

Request models use normal ASP.NET Core binding and DataAnnotations validation.
Invalid requests return standard validation problem details.

The endpoint manifest is registered with:

```csharp
builder.Services.AddAnvilOpenApi();
app.MapAnvilOpenApi();
app.MapAnvilManifest();
```

OpenAPI and the manifest are derived from the same endpoint metadata.

## Forms and Validation

Normal forms work without JavaScript:

```razor
<form method="post" action="/settings">
    <AntiforgeryToken />
    <input name="Name" required />
    <button type="submit">Save</button>
</form>
```

Use `MapAnvilFormPost<TRequest>` for typed URL-encoded forms:

```csharp
public sealed record UpdateSettings(
    [property: Required] string Name,
    string Timezone);

app.MapAnvilFormPost<UpdateSettings>("/settings", async (_, request) =>
{
    await settings.SaveAsync(request);
    return Results.Redirect("/settings");
});
```

The server validates every mutation. Client validation is optional enhancement,
never the authority.

## Fragments and Shards

Fragments return Razor HTML without a document wrapper:

```csharp
app.MapAnvilFragmentGet<CustomerRows>(
    "/api/customers/rows",
    context => new
    {
        Customers = service.Search(context.Request.Query["search"])
    });
```

Typed POST fragments bind and validate request models:

```csharp
app.MapAnvilFragmentPost<SearchRequest, CustomerRows>(
    "/api/customers/rows",
    (_, request) => Task.FromResult<object?>(
        new { Customers = service.Search(request.Query) }));
```

Shards are targetable server-rendered regions:

```razor
<AnvilShard Id="customer-results"
            Url="/api/customers/shard"
            SignalName="query">
    <p>Loading...</p>
</AnvilShard>
```

Use fragments for tables, modals, inline edits, and form targets. Use shards
when a region rerenders in response to changing browser signals.

## Browser Runtime

Render `<AnvilRuntime />` once in `App.razor`. It provides explicit, small
browser behavior:

```html
<button data-anvil-toggle="#details" aria-expanded="false">
    Show details
</button>
<div id="details" hidden>Details</div>

<input data-anvil-signal="query" value="coffee">
<output data-anvil-signal-bind="query"></output>
```

Opt into targeted form updates:

```html
<form method="get"
      action="/api/customers/rows"
      data-anvil-partial-form
      data-anvil-target="#customer-rows">
    <input name="search">
</form>
```

The runtime sends `X-Anvil-Partial`, replaces only the target, preserves GET
history, and falls back to normal navigation when a fragment fails.

The runtime does not translate arbitrary C# to JavaScript and does not serialize
server state automatically.

## Authentication and Authorization

Register standard ASP.NET Core Identity with EF Core:

```csharp
builder.Services.AddAnvilPersistence<AppDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddAnvilIdentity<ApplicationUser, AppDbContext>();

app.UseAuthentication();
app.UseAuthorization();
app.MapAnvilIdentityEndpoints<ApplicationUser>();
```

Supported Identity flows include:

- public, invite-only, and disabled registration;
- login and logout;
- current user;
- email confirmation;
- password reset;
- lockout;
- TOTP setup and MFA login;
- recovery codes;
- device sessions and revocation;
- external login challenge boundaries.

Use named permission policies:

```csharp
builder.Services.AddAnvilAuthorization(options =>
    options.AddPolicy("orders.view", policy =>
        policy.RequirePermission("orders.view")));

app.MapGet("/admin/orders", () => Results.Ok())
    .RequirePermission("orders.view");
```

Protected anonymous requests return `401`. Authenticated requests without the
required permission return `403`. Cookie-authenticated mutations require
antiforgery.

## Persistence, Migrations, and Tenancy

Anvil does not hide EF Core. Provider helpers exist for SQLite, SQL Server,
PostgreSQL, and MySQL:

```csharp
builder.Services.AddAnvilSqlitePersistence<AppDbContext>(
    builder.Configuration,
    (options, connection) => options.UseSqlite(connection));
```

Author and apply migrations explicitly:

```text
dotnet ef migrations add InitialCreate --project src/MyApp
anvil migrate status
anvil migrate --dry-run
anvil migrate
```

For shared-schema tenancy:

```csharp
builder.Services.AddAnvilTenancy(options =>
{
    options.Required = true;
    options.AllowDevelopmentHeader = true;
});
app.UseAnvilTenancy();
```

Claims are authoritative in production. Use tenant-owned entities, EF query
filters, and save interceptors for isolation.

## Audit and Compliance

`IAuditWriter` records actor, tenant, resource, operation, changed fields, trace
ID, and correlation ID. Passwords, tokens, credentials, and secrets are removed
from changed-field payloads.

`AddAnvilCompliance` provides contracts for:

- consent records;
- privacy export requests;
- deletion and anonymization;
- retention policies;
- legal holds;
- access boundaries;
- data providers and erasure handlers.

Use durable storage in production. Audit is persistence logging, not event
sourcing.

## Jobs and Operations

Register durable jobs and scheduling:

```csharp
builder.Services.AddAnvilBackgroundJobs();
builder.Services.AddAnvilScheduling();
builder.Services.AddAnvilTransactionalOutbox<AppDbContext>();
```

Jobs support delayed availability, leases, retries, backoff, dead letters,
replay, cancellation, history, schedules, actor propagation, and tenant
propagation. Inspect them with:

```text
anvil jobs status
anvil schedule list
```

Operations adapters include file-backed notifications, bounded storage,
CSV/JSON transfer status, signed webhooks, tenant-namespaced cache, readiness
labels, and health endpoints. Provider credentials, retention, monitoring, and
secret rotation remain application and deployment responsibilities.

## Streaming, Mail, Localization, and Assets

Use `[StreamRendering]`, `RequestContext.FlushAsync`, and cancellation for
progressive HTML. SSE supports IDs, `Last-Event-ID`, keep-alives, cancellation,
and typed events. WebSockets support authenticated upgrade, protocol selection,
message limits, and lifecycle handling.

Mail supports typed HTML/plain-text messages, attachments, SMTP, file transport,
in-memory tests, previews, and localized message factories.

Localization uses standard resources:

```csharp
builder.Services.AddAnvilLocalization("en-US", "fr-FR");
app.UseAnvilLocalization();
```

Assets support static files, content hashes, cache headers, manifests, Tailwind
hooks, fonts, Fontsource, SVG icons, Iconify catalogs, safe Markdown, image
helpers, and static export.

## Production

Use `AddAnvilProduction` for persistent Data Protection keys, HTTPS/HSTS,
security headers, CSP, trusted proxies, and health endpoints. Use:

```text
anvil doctor --production
anvil docker
anvil release --check
```

Production must use reviewed migrations, durable session and job stores,
persistent Data Protection keys, secure secrets, HTTPS, backups, rollback
procedures, structured logging, health checks, and observability.

## Testing and Agent Workflow

Recommended verification:

```text
dotnet build --configuration Release
dotnet test --configuration Release
anvil new MyApp --profile identity
anvil make endpoint Health
anvil make:page Reports
anvil make:shard Revenue
anvil make resource Customer
anvil make crud Customer
anvil generate
anvil generate --check
anvil doctor --production
```

When modifying Anvil:

1. Read `build plan.md` before starting.
2. Reuse standard ASP.NET Core APIs before introducing abstractions.
3. Keep endpoint, validation, authorization, and persistence behavior explicit.
4. Add focused tests for changed behavior.
5. Add or update sample coverage when a public capability changes.
6. Update documentation and build-plan status in the same change.
7. Run Release build and tests before reporting completion.

Do not claim a plan item is complete merely because a contract exists. Verify
the behavior through tests and, for browser behavior, a real browser or an
explicit browser contract test.
