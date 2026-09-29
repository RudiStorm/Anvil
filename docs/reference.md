# Anvil Framework Reference

Anvil is a .NET 10 framework for building server-rendered applications with
Razor and ASP.NET Core. It keeps the application an ordinary ASP.NET Core
project: dependency injection, configuration, middleware, EF Core, Identity,
health checks, and logging remain standard .NET APIs.

The reference application is `samples/Anvil.Sample`. Run it with:

```text
dotnet run --project samples/Anvil.Sample
```

The sample routes are:

| Route | Demonstrates |
| --- | --- |
| `/` | Admin dashboard, metrics, live fragments |
| `/products` | Razor CRUD table, search, partial form updates |
| `/products/{id}` | Typed route parameters and details |
| `/team` | Server-rendered application page |
| `/settings` | Validated form mutation |
| `/framework` | Framework capability tour |
| `/identity` | ASP.NET Core Identity endpoints and MFA links |
| `/operations` | Audit, tenancy, jobs, storage, webhooks, and readiness guidance |
| `/integrations` | Sessions, uploads, and mail |
| `/streaming` | Streaming and live regions |
| `/errors` | Component error boundaries |
| `/openapi.json` | Generated API metadata |
| `/anvil.contract.json` | Canonical Anvil endpoint manifest |
| `/sitemap.xml` | Sitemap metadata |
| `/health/ready` | Readiness checks |

## Application Setup

The smallest application uses the normal ASP.NET Core host:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAnvil();

var app = builder.Build();
app.UseAnvilErrors();
app.UseStaticFiles();
app.UseAnvil();
app.MapAnvil<App>();
app.Run();
```

`AddAnvil()` registers the request context, sessions, data protection, Razor
support, antiforgery defaults, and framework services. Advanced features are
opt-in through focused extensions.

## CLI Scaffolding

Create editable C# and Razor application source with the CLI:

```text
anvil make endpoint Health
anvil make:page Reports
anvil make:shard Revenue
anvil make resource Customer
anvil make crud Customer
anvil generate
anvil generate --check
```

`make endpoint` creates an endpoint scaffold under `Endpoints`. Add request,
response, validation, authorization, and handler logic there. `make:page` creates
a routed Razor page under `Components/Pages`. `make:shard` creates a targeted
Razor component under `Components/Shards` plus a matching server endpoint under
`Endpoints`, and adds the endpoint mapping to `Program.cs`.
`make resource`
creates the application model and resource boundary. `make crud` adds editable
Razor list, detail, form, and mutation pages. The generated files belong to the
application and do not introduce Vue or a separate frontend project.

## Razor Rendering

Razor components are the primary UI unit. They support normal C# control flow,
parameters, child content, dependency injection, layouts, page titles, head
content, async lifecycle methods, and error boundaries.

```razor
@page "/orders/{Id:int}"
@inject OrderService Orders

<PageTitle>Order @Id</PageTitle>

@if (order is null)
{
    <p>Order not found.</p>
}
else
{
    <h1>@order.Number</h1>
    <p>@order.Status</p>
}

@code {
    [Parameter] public int Id { get; set; }
    private Order? order;

    protected override async Task OnParametersSetAsync() =>
        order = await Orders.FindAsync(Id);
}
```

Razor escapes values by default. Use `AnvilMarkup.Trusted` only for content
that the application has explicitly sanitized and approved.

## RequestContext

Inject `RequestContext` into handlers and application services when request data
is needed:

```csharp
public sealed class CurrentRequest(RequestContext request)
{
    public string? Tenant => request.Request.Headers["X-Tenant-Id"];
    public CancellationToken Cancellation => request.RequestAborted;
}
```

It exposes the HTTP request and response, route values, query values, headers,
cookies, client address, cancellation, form binding, JSON binding, raw body
limits, response flushing, and request-scoped values.

Request-scoped memoization is available through `MemoizeAsync`. Values are
deduplicated for concurrent callers inside one request and are discarded when
the request ends.

## Routing and Links

Use ASP.NET Core route mapping for endpoints and Razor `@page` conventions for
pages. Internal links should use Anvil link helpers rather than string
concatenation.

```csharp
public sealed record ProductRoute(int Id);
static readonly AnvilRoute<ProductRoute> Product =
    new("/products/{id:int}");

var href = Product.Link(new ProductRoute(42));
```

Links support route encoding, query values, fragments, absolute URLs, named
routes, configurable base URLs, and current-route detection.

## API Endpoints

Anvil wraps Minimal APIs without hiding them:

```csharp
app.MapAnvilGet("/api/health", async _ =>
{
    await Task.CompletedTask;
    return Results.Ok(new { status = "ok" });
});

app.MapAnvilPost<CreateOrder>("/api/orders", async (_, request) =>
{
    var order = await orders.CreateAsync(request);
    return Results.Created($"/api/orders/{order.Id}", order);
});
```

Available helpers include GET, POST, form POST, PUT, PATCH, DELETE,
procedures, fragments, shards, SSE, WebSockets, route groups, and sitemap
mapping. Request models use ASP.NET Core binding and DataAnnotations validation.

The canonical endpoint manifest is available at `/anvil.contract.json` and is
the source for OpenAPI metadata, route diagnostics, authorization metadata, and
generated tooling.

## Forms and Partial Updates

Normal forms work without JavaScript. Opt into targeted replacement when a form
should update only one region:

```razor
<form method="get"
      action="/api/orders/rows"
      data-anvil-partial-form
      data-anvil-target="#orders">
    <input name="search" />
    <button type="submit">Search</button>
</form>

<tbody id="orders">
    <OrderRows Orders="orders" />
</tbody>
```

The browser runtime sends `X-Anvil-Partial`, replaces only the target, updates
GET history, and falls back to ordinary navigation if the fragment request
fails. Server-side validation remains authoritative.

For Razor-rendered fragments use:

```csharp
app.MapAnvilFragmentGet<OrderRows>(
    "/api/orders/rows",
    context => new { Search = context.Request.Query["search"].ToString() });
```

Use `MapAnvilFragmentPost<TRequest, TComponent>` for typed JSON or form-backed
fragment requests. Fragments intentionally return HTML without a document
wrapper.

## Browser Runtime

`<AnvilRuntime />` adds the optional zero-build runtime. It supports:

- local toggles;
- local signals and input bindings;
- prefetching;
- same-origin client navigation;
- partial GET/POST updates;
- partial form submissions;
- live regions;
- server shards;
- island hydration;
- WebSocket reconnection;
- browser-visible state markers.

The runtime never translates arbitrary C# into JavaScript and never serializes
services, cookies, sessions, or component fields automatically.

Use `AnvilClientState` with `AnvilPublicState<T>` only for deliberately public
data. Anything emitted into the document is readable by the browser.

## Streaming, SSE, and WebSockets

Use `[StreamRendering]` for progressive Razor rendering. `RequestContext`
provides cancellation and `FlushAsync` support.

SSE mapping supports event IDs, `Last-Event-ID`, keep-alives, cancellation, and
typed `AnvilSseEvent` values:

```csharp
app.MapAnvilSse("/events", _ => Events());
```

WebSocket endpoints support authentication before upgrade, protocol selection,
message size limits, cancellation, close handling, and bounded browser-runtime
reconnection.

## Authentication and Authorization

For applications requiring standard Identity, configure an EF Core context and
register:

```csharp
builder.Services.AddAnvilPersistence<AppDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddAnvilIdentity<ApplicationUser, AppDbContext>();
app.UseAuthentication();
app.UseAuthorization();
app.MapAnvilIdentityEndpoints<ApplicationUser>();
```

The Identity endpoint set includes registration policies, login, logout,
current user, email confirmation, password reset, TOTP setup and verification,
recovery codes, MFA login, device sessions, session revocation, and external
login challenge boundaries.

For code-defined permissions use standard ASP.NET Core policies and Anvil
permission helpers:

```csharp
builder.Services.AddAnvilAuthorization(options =>
    options.AddPolicy("orders.view", policy =>
        policy.RequirePermission("orders.view")));

app.MapGet("/admin/orders", () => Results.Ok())
    .RequirePermission("orders.view");
```

Unauthenticated protected requests return `401`; authenticated callers without
permission return `403`.

## Persistence and Migrations

Persistence stays application-owned. Provider helpers are available for SQLite,
SQL Server, PostgreSQL, and MySQL. Migration authoring remains normal EF Core:

```text
dotnet ef migrations add InitialCreate --project src/MyApp
anvil migrate
anvil migrate --dry-run
```

Production migration commands apply reviewed migrations only. Anvil does not
invent a repository or hide EF Core behavior.

## Tenancy

Register tenancy when the application uses shared-schema tenant isolation:

```csharp
builder.Services.AddAnvilTenancy(options =>
{
    options.Required = true;
    options.AllowDevelopmentHeader = true;
});
app.UseAnvilTenancy();
```

Claims are authoritative. Development headers are optional and must not be the
only tenant identity in production. Use `TenantContext`, tenant-owned entities,
EF query filters, and the tenant save interceptor for isolation.

## Audit and Compliance

`IAuditWriter` records actor, tenant, resource, operation, changed fields, trace
ID, and correlation ID. Sensitive keys such as passwords, tokens, credentials,
and secrets are filtered.

`AddAnvilCompliance` provides application-owned contracts for:

- consent records;
- privacy export requests;
- deletion and anonymization;
- retention policies;
- legal holds;
- access boundaries;
- data providers and erasure handlers.

Audit and privacy workflows must be backed by a durable application store for
production deployments.

## Background Jobs and Outbox

Anvil jobs support durable file-backed state, delayed availability, leases,
retry backoff, dead letters, replay, cancellation, history, schedules, actor
and tenant propagation, and an EF transactional outbox boundary.

Inspect them with:

```text
anvil jobs status
anvil schedule list
```

Use a distributed or provider-backed store for multi-instance production
deployments.

## Operations Integrations

The operations package provides explicit contracts and readiness labels for:

- file-backed notifications with pagination and retention;
- bounded owner-isolated storage;
- CSV/JSON import and export transfer status;
- signed durable webhooks with retries and replay;
- tenant-namespaced distributed cache;
- readiness and health checks.

Optional capabilities are not automatically production-ready. Provider
credentials, retention, monitoring, secret rotation, and operational ownership
remain application and deployment responsibilities.

## Mail, Localization, and Content

Mail supports typed messages, HTML and plain-text content, attachments, SMTP,
file transport, in-memory transport, previews, and localized message factories.

Localization uses standard resource-based `IStringLocalizer` and request
culture middleware:

```csharp
builder.Services.AddAnvilLocalization("en-US", "fr-FR");
app.UseAnvilLocalization();
```

Markdown, fonts, Fontsource, inline SVG icons, Iconify catalogs, Tailwind
hooks, and asset manifests are all opt-in application features.

## Production and Diagnostics

Use `AddAnvilProduction` for persistent Data Protection keys, HTTPS/HSTS,
security headers, CSP, trusted proxies, and health endpoints. Use:

```text
anvil doctor --production
anvil docker
anvil release --check
```

The production doctor reports PASS, WARNING, and BLOCKING checks for project
structure, migrations, provider configuration, Identity, mail, jobs,
observability, deployment files, health endpoints, and secrets boundaries.

## Testing

The recommended verification sequence is:

```text
dotnet build --configuration Release
dotnet test --configuration Release
anvil new MyApp --profile identity
anvil make resource Customer
anvil make crud Customer
anvil generate
anvil generate --check
anvil doctor --production
```

Test full documents, fragments, authentication status codes, antiforgery,
tenant isolation, audit records, job replay, operation ownership, accessibility
contracts, and response sizes. Use real browser automation in the application
when interaction behavior—not just HTTP output—must be verified.
