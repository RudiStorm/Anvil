# Anvil

Anvil is a .NET 10 server-rendered web framework inspired by Tokio's Topcoat.
It uses ASP.NET Core hosting and Razor syntax while providing a focused framework
API for pages, layouts, request context, and application conventions.

## Status

Anvil is a server-rendered .NET 10 framework with Razor UI, typed endpoints,
partial HTML updates, Identity integration, authorization, audit/compliance
contracts, tenancy, durable jobs, operations adapters, production diagnostics,
and an editable reference application.

## Running the sample

```sh
dotnet run --project samples/Anvil.Sample
```

The sample is an admin dashboard demonstrating `/`, `/products`, `/team`, and
`/settings`, with `/products/{id}` showing an inventory detail view. The
`/framework` page covers rendering and browser behavior; `/identity` covers
Identity and MFA; `/operations` covers audit, tenancy, jobs, storage, webhooks,
and readiness; `/integrations` covers uploads, sessions, and mail.

See [`AnvilDocumentation.md`](AnvilDocumentation.md) for the complete framework
and agent guide. The focused version is also available at
[`docs/reference.md`](docs/reference.md).

## CLI

Build the CLI from the repository and use it to create or run an application:

```sh
dotnet run --project src/Anvil.Cli -- new MyApp
cd MyApp
dotnet run --project ../src/Anvil.Cli -- dev
```

From an application directory, `anvil publish` creates a Windows self-contained
single-file executable in `D:\dev-tools-path` by default. Pass `-o` or
`--output` to choose another destination.

To publish the Anvil CLI itself as a single executable:

```powershell
.\Publish-AnvilCli.ps1
```

The script publishes `anvil.exe` to `D:\dev-tools-path`. Override the output,
runtime, or configuration when needed:

```powershell
.\Publish-AnvilCli.ps1 -OutputDirectory D:\tools -Runtime win-x64 -Configuration Release
```

To create the framework packages used by package-mode generated applications:

```powershell
.\Publish-AnvilPackages.ps1
```

The package output contains `Anvil`, `Anvil.Razor`, and `Anvil.Cli` packages at
the aligned project version. Publish the first two to the configured NuGet feed
before using `anvil new` outside a source checkout.

For a local package feed, pass it during generation:

```powershell
anvil new Portal --package-source D:\dev-tools-path\packages
```

`anvil dev` delegates to `dotnet watch` and forwards additional arguments to
the application. Generated projects use source references when created from
this repository and package references when created elsewhere.

## Request context

Inject `RequestContext` when a handler or service needs the current request:

```csharp
public sealed class ApiHandler(RequestContext request)
{
    public ValueTask<Input?> ReadInputAsync() => request.ReadJsonAsync<Input>();
}
```

The helpers delegate to ASP.NET Core and use the request cancellation token by
default.

For reusable typed route definitions, keep route values in a dedicated record:

```csharp
public sealed record ProductRoute(int Id);
static readonly AnvilRoute<ProductRoute> ProductDetails = new("/products/{id:int}");

var href = ProductDetails.Link(new ProductRoute(42));
```

Links support route encoding, query values, fragments, and current-route checks.
Named ASP.NET Core endpoints can also be resolved with
`request.RouteLink("route-name")`.

Razor's folder and component conventions remain the default route organization,
and `MapAnvil<TApp>` discovers component routes through ASP.NET Core. Use
`[AnvilRoute("/health", Name = "health")]` for explicit route metadata; API
registration remains explicit so route behavior stays predictable.

## Cookies and sessions

`RequestContext` exposes simple cookie helpers with HttpOnly and SameSite=Lax
defaults:

```csharp
request.SetCookie("theme", "dark");
var theme = request.GetCookie("theme");
request.DeleteCookie("theme");
```

Sessions are registered by `AddAnvil()` and use an in-memory store by default:

```csharp
var session = await sessions.StartAsync(new Dictionary<string, string>
{
    ["user_id"] = user.Id.ToString()
});

var current = await sessions.GetAsync();
await sessions.StopAsync();
```

Inject `AnvilSessionManager` where session state is needed. Replace the
`IAnvilSessionStore` registration with a persistent implementation for
production deployments. Session cookies contain random tokens while the store
keeps only SHA-256 token hashes.

For smaller values, Data Protection-backed cookies are available without
writing custom cryptography:

```csharp
request.SetSignedCookie("return_to", "/account");
request.SetEncryptedCookie("preferences", "private-value");
request.SetJsonCookie("settings", new UserSettings("dark"));
```

Invalid or tampered protected cookies return `null` rather than becoming an
application exception.

## Context, memoization, and authorization

Use normal DI for application services and `RequestContext` for request-scoped
values:

```csharp
var catalog = request.GetApplicationService<ProductCatalog>();
request.SetValue(new RequestState("active"));
```

Expensive work can be deduplicated within one request:

```csharp
var products = await request.MemoizeAsync("products", async cancellationToken =>
    await catalog.LoadAsync(cancellationToken));
```

Session-backed guards are explicit and composable:

```csharp
var session = await request.RequireAuthenticatedAsync();
await request.RequireRoleAsync("admin");
```

Missing authentication produces HTTP 401 and failed role checks produce HTTP
403 through `UseAnvilErrors()`.

## Streaming

Razor components can opt into ASP.NET Core streaming with
`@attribute [StreamRendering]`. Anvil also exposes request-aware helpers:

```csharp
await request.FlushAsync();
if (!request.IsClientConnected())
{
    return;
}
```

The sample includes `/streaming`, which renders a loading state before delayed
content completes.

Use `AnvilErrorBoundary` around components that need a safe fallback:

```razor
<AnvilErrorBoundary FallbackMessage="Unable to load this section.">
    <SlowComponent />
</AnvilErrorBoundary>
```

Exception details are not rendered by the default fallback. Live multi-emission
regions require the browser transport layer and are planned separately.

## Browser runtime

`AnvilRuntime` is an optional zero-build browser runtime. It currently
supports local toggles and same-origin partial HTML replacement:

```razor
<AnvilRuntime />

<button data-anvil-toggle="#details" aria-expanded="false">Details</button>
<div id="details" hidden>Additional content</div>

<button data-anvil-get="/products" data-anvil-target="#results">
    Refresh results
</button>
```

The runtime does not translate C# or serialize server state automatically.
Server-visible data must be returned explicitly by the requested endpoint.

For explicit public state, `AnvilClientState` accepts an `AnvilPublicState<T>`
wrapper and emits escaped JSON in a marked
`application/json` script element. Never pass secrets to it; anything emitted
there is intentionally readable by the browser.

Client state is deliberately opt-in. Anvil never serializes dependency-injected
services, request context, cookies, sessions, or component fields automatically.

Local signals use explicit data attributes:

```html
<input data-anvil-signal="query" value="coffee">
<output data-anvil-signal-bind="query"></output>
```

Typed server procedures use normal POST endpoints with automatic validation:

```csharp
app.MapAnvilProcedure<SearchRequest, SearchResult>(
    "/api/search",
    async (_, request) => await SearchAsync(request));
```

WebSocket clients can use `AnvilWebSocket.connect(url, handlers)` from the
runtime. It reconnects with bounded exponential backoff and can be stopped
explicitly.

## Integrations

Anvil exposes small helpers for common server integrations:

- `IsHtmxRequest()`, HTMX trigger, redirect, and swap headers.
- `MapAnvilFragmentGet<TComponent>()` and `MapAnvilFragmentPost<TComponent>()`
  for Razor-rendered HTML fragments.
- Alpine AJAX request, redirect, and target headers.
- Datastar signal parsing and element patch events.
- `MapAnvilSitemap()` for XML sitemap responses.
- `UseAnvilStaticDirectory()` for mounted public directories.
- `AddAnvilFileMail()` and `AddAnvilSmtpMail()` for mail delivery.

SSE streams expose `RequestContext.LastEventId`, support event IDs, and can emit
`AnvilSseEvent.KeepAlive()` frames. Every event stream observes the request
cancellation token and flushes after each event.

The Tailwind, font, and icon helpers are intentionally opt-in and do not add a
Node.js build requirement to the core framework.

Razor fragments are ordinary components rendered without a document wrapper:

```csharp
app.MapAnvilFragmentGet<InventoryRows>(
    "/inventory/rows",
    context => Task.FromResult<object?>(new
    {
        Products = catalog.Search(context.Request.Query["search"]).ToArray()
    }));
```

Use them with HTMX by targeting the element that owns the fragment:

```razor
<form hx-get="/inventory/rows" hx-target="#inventory-results" hx-swap="innerHTML">
    <input name="search" />
</form>
<tbody id="inventory-results">
    <InventoryRows Products="@products" />
</tbody>
```

`AnvilRuntime` bundles and pins HTMX `4.0.0` as a static Anvil asset by default.
Applications can provide a different `HtmxSource` and an `HtmxIntegrity` value
when the runtime is rendered if they need a custom host or deployment policy.

For form-backed fragments, use the typed POST overload. It binds and validates
the request before rendering the component:

```csharp
app.MapAnvilFragmentPost<SearchRequest, SearchRows>(
    "/inventory/rows",
    (context, request) => Task.FromResult<object?>(new
    {
        Products = catalog.Search(request.Query).ToArray()
    }));
```

`MapAnvilShard<TRequest, TComponent>()` uses the same renderer when a shard
needs a JSON request body. Components rendered through either API receive the
normal scoped services and `RequestContext`, but no document layout.

The sample application also demonstrates the related full-stack paths at
`/integrations`: session sign-in, multipart uploads, and in-memory email
delivery. These endpoints are intentionally application examples; production
applications should provide their own identity and persistence policies.

Static export is available through `AnvilStaticExporter`, OpenAPI metadata
through `AnvilApiRegistry`, and background work through
`IAnvilBackgroundTaskQueue`. These are intentionally opt-in services so a
small application does not pay for unused infrastructure.

Production guidance is available in `docs/persistence.md`,
`docs/upgrading.md`, `docs/crud.md`, and `docs/releasing.md`.

The CLI also supports editable starter components:

```sh
anvil ui list
anvil ui add button
```

Razor helpers also include safe Markdown rendering, responsive lazy images, and
island boundaries. These helpers do not execute Markdown or image content as
trusted markup without first escaping or explicitly defining the source.

## API routes

Anvil provides thin typed helpers over ASP.NET Core minimal APIs:

```csharp
app.MapAnvilGet("/api/health", async _ =>
{
    await Task.CompletedTask;
    return Results.Ok(new { status = "ok" });
});

app.MapAnvilPost<CreateRequest>("/api/items", async (_, request) =>
{
    await Task.CompletedTask;
    return Results.Ok(request);
});
```

Request models are bound by ASP.NET Core's normal JSON binding behavior.
Models passed to `MapAnvilPost<TRequest>` are validated with DataAnnotations
before the handler runs. Invalid models receive a standard HTTP 400 validation
problem response.

The same validation behavior is available for `MapAnvilPut<TRequest>` and
`MapAnvilPatch<TRequest>`. `MapAnvilDelete` provides the corresponding
typed DELETE route helper.

For HTML forms, use `MapAnvilFormPost<TRequest>`. It uses ASP.NET Core's
native `[FromForm]` binding and keeps antiforgery protection enabled by
default:

```csharp
app.MapAnvilFormPost<ContactForm>("/contact", async (_, form) =>
{
    await SaveContactAsync(form);
    return Results.Redirect("/contact/sent");
});
```

Call `app.UseAnvilErrors()` before endpoint mapping to get safe exception
responses and problem-details responses for API errors. Browser requests that
advertise `text/html` receive a minimal HTML error response instead.

## License

MIT
