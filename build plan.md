# Anvil Build Plan

Anvil is a .NET 10 server-rendered web framework inspired by Tokio's Topcoat.
It uses Razor and ASP.NET Core while reducing the ceremony normally required to
build a full-stack web application.

## Product Principles

- Server-rendered HTML is the default.
- Razor is the template language.
- ASP.NET Core hosting, dependency injection, configuration, and HTTP primitives are reused.
- No separate frontend application is required.
- No WebAssembly is required for normal applications.
- No Node.js build pipeline is required for normal applications.
- Internal URLs should be generated, not concatenated manually.
- The common path should require minimal configuration.
- Advanced features should be explicit rather than magical.
- Optional integrations must not complicate the core package.
- Framework abstractions must not hide database or security behavior.
- Application code should remain ordinary C# wherever possible.

## P0: Core Framework

### Project Experience

- `anvil new` creates a working application. [x]
- `anvil dev` starts the development server. [x]
- Development file watching. [x]
- Browser refresh during development. [x]
- Clear build and runtime errors. [x]
- Production publishing without a separate JavaScript application. [x]
- Framework integration tests for the complete request lifecycle. [x]
- A single obvious application entry point. [x]
- Sensible defaults with little or no configuration. [x]

### Rendering

- Razor component rendering. [x]
- Server-side rendering. [x]
- Async components. [x]
- Component parameters and child content. [x]
- Layouts. [x]
- Conditional rendering and normal C# control flow. [x]
- HTML escaping by default. [x]
- Explicit safe HTML support. [x]
- Conditional and boolean attributes. [x]
- Attribute forwarding. [x]
- CSS class composition. [x]
- Page titles and head content. [x]
- Component dependency injection. [x]
- Proper component error handling. [x]

### Routing

- Page routes. [x]
- API routes. [x]
- GET and POST routes. [x]
- PUT, PATCH, and DELETE routes. [x]
- Route parameters. [x]
- Typed route parameters. [x]
- Catch-all routes. [x]
- Query-string binding. [x]
- Form binding. [x]
- JSON binding. [x]
- Nested layouts. [x]
- Route groups. [x]
- Route-specific authorization and request logic. [x]
- Duplicate route detection. [x]
- Trailing-slash normalization. [x]
- Custom hosting inside an ASP.NET Core pipeline. [x]

### Request Context

- Request-scoped context. [x]
- Access to request and response data. [x]
- Route values, query values, headers, cookies, and client information. [x]
- Cancellation token propagation. [x]
- Normal ASP.NET Core dependency injection. [x]
- Application-scoped services. [x]
- Request-specific values. [x]
- Scoped values for nested component operations. [x]

### Forms and Content

- URL-encoded form binding. [x]
- Query binding. [x]
- JSON request binding. [x]
- Typed request models. [x]
- Optional request models. [x]
- Validation errors. [x]
- Request body size limits. [x]
- Raw body access when explicitly requested. [x]
- Multipart form uploads. [x]
- File upload handling. [x]
- Antiforgery protection for mutations. [x]
- Clear malformed-input responses. [x]

### Errors

- Custom 400, 401, 403, 404, and 500 pages. [x]
- Safe production error handling. [x]
- Developer-friendly development errors. [x]
- Error boundaries around components. [x]
- Proper HTTP status preservation. [x]
- Redirect helpers. [x]
- 303 POST/redirect/GET flow. [x]
- 307 redirects. [x]
- Missing-resource to 404 helpers. [x]

### Assets

- Static file serving. [x]
- Development asset serving. [x]
- Production asset serving. [x]
- Content-hashed asset URLs. [x]
- Cache headers. [x]
- Asset lookup from Razor files. [x]
- Missing asset diagnostics. [x]
- Asset manifest generation. [x]

## P1: Productive Framework

### Routing and Links

- Razor folder/component conventions. [x]
- Optional Razor route discovery through `MapAnvil<TApp>`. [x]
- Explicit route attributes for advanced cases. [x]
- Named route generation. [x]
- Reusable typed internal link definitions. [x]
- Strongly typed form actions. [x]
- Correct path and query encoding. [x]
- Current-route detection. [x]
- URL fragments. [x]
- Absolute URLs and configurable base URLs. [x]

### Context and Data Access

- Typed application context. [x]
- Typed request context. [x]
- Composable request guards. [x]
- Function-based authentication and authorization helpers. [x]
- Per-request memoization. [x]
- Concurrent deduplication of identical data requests. [x]
- Scoped context overrides. [x]

### Validation

- Model validation with DataAnnotations. [x]
- Form validation. [x]
- Field-level errors. [x]
- Validation summaries. [x]
- Server-side validation as the authority. [x]
- Optional client-side validation. [x]
- Validation integration with Anvil API and form routes. [x]

### Cookies

- Read cookies. [x]
- Add cookies. [x]
- Remove cookies. [x]
- Secure, HttpOnly, SameSite, path, and domain settings. [x]
- Correct cookie behavior on redirects and errors. [x]
- Protection against writes after streaming begins. [x]
- Signed cookies. [x]
- Encrypted cookies. [x]
- Typed JSON cookie stores. [x]
- Configurable cookie keys and prefixes. [x]

### Sessions

- Session abstraction with bring-your-own storage. [x]
- Login and logout lifecycle. [x]
- Session expiration. [x]
- Sliding expiration. [x]
- Session refresh. [x]
- Session rotation. [x]
- Session revocation. [x]
- Token hashing. [x]
- Secure default cookie settings. [x]
- Authentication helpers. [x]

Authentication should remain application-owned initially. Anvil should provide
secure session primitives without forcing a user model or identity provider.

### Streaming and Resilience

- Progressive HTML streaming. [x]
- Suspense and loading placeholders. [x]
- Error boundaries for delayed content. [x]
- Live regions that replace their own HTML. [x]
- Multiple emissions from a component. [x]
- Response flushing. [x]
- Client disconnect cancellation. [x]
- Backpressure handling. [x]

### Browser Runtime

- Small optional browser runtime. [x]
- Server-rendered initial HTML. [x]
- Event handlers without WebAssembly. [x]
- Local browser state. [x]
- Input bindings. [x]
- Explicit server calls for server-side work. [x]
- Partial HTML replacement. [x]
- Opt-in partial form submissions that replace only a targeted HTML region, preserve GET history, and fall back to navigation on failure. [x]
- Secure serialization of browser-visible state. [x]
- Strict separation between client-visible and server-only state. [x]

Anvil should not translate arbitrary C# into JavaScript. Browser behavior
should use explicit events, local state, server calls, and partial updates.

### Mail

- HTML email rendering with Razor-compatible message previews. [x]
- Plain-text fallback. [x]
- Attachments. [x]
- SMTP transport. [x]
- File transport for development. [x]
- In-memory transport for tests. [x]
- Typed mail messages. [x]
- Dependency injection. [x]
- Email previews. [x]

### Styling and UI

- Optional Tailwind integration hook. [x]
- Tailwind build integration through the `tailwindcss` executable. [x]
- No mandatory Node.js dependency. [x]
- Development and production CSS generation. [x]
- Font bundling. [x]
- Font-face generation. [x]
- Optional Fontsource integration. [x]
- Inline SVG icons. [x]
- Accessible icon labels. [x]
- Optional Iconify integration. [x]
- Starter UI components. [x]
- Components copied into the application. [x]
- Editable copied components. [x]
- Component variants and sizes. [x]
- Attribute forwarding and child content. [x]
- Dark mode conventions. [x]

UI code should belong to the application after installation and remain freely
editable.

## P2: Full-Stack Integrations

### Signals and Server Procedures

- Reactive signals. [x]
- Reactive text updates. [x]
- Reactive attribute updates. [x]
- Event expressions. [x]
- Async server procedures. [x]
- Server-rendered partial components. [x]
- Loading and error states. [x]
- Server shards that rerender on changed inputs. [x]
- Secure procedure input validation. [x]
- Explicit authorization inside procedures and shards. [x]
- WebSocket-assisted connected rendering. [x]
- Reconnection behavior. [x]

### SSE

- Server-sent event responses. [x]
- Event IDs and event data. [x]
- Keep-alive messages. [x]
- Last-event-ID support. [x]
- Resumable streams. [x]
- Cancellation. [x]
- Typed event helpers. [x]

### WebSockets

- WebSocket endpoints. [x]
- Authentication before upgrade. [x]
- Protocol selection. [x]
- Message send and receive helpers. [x]
- Message size limits. [x]
- Cancellation and close handling. [x]
- Safe connection lifecycle. [x]

### Multipart

- Multipart parsing. [x]
- Field iteration. [x]
- File streaming. [x]
- Text and byte field helpers. [x]
- Upload size limits. [x]
- Correct field disposal. [x]

### External Integrations

- HTMX request detection. [x]
- Razor-rendered HTMX fragment responses. [x]
- HTMX response headers. [x]
- Alpine AJAX request detection. [x]
- Alpine AJAX target handling. [x]
- Datastar signal parsing. [x]
- Datastar element patches. [x]
- Datastar signal patches. [x]
- Datastar SSE responses. [x]
- Static directory mounting. [x]
- ASP.NET Core request-delegate service integration. [x]

### Sitemap

- Sitemap generation. [x]
- URL metadata. [x]
- Last-modified timestamps. [x]
- Change frequency. [x]
- Priority. [x]
- Base URL support. [x]

## P3: Advanced Features

### CLI

- `anvil new`. [x]
- `anvil dev`. [x]
- `anvil build`. [x]
- `anvil fmt` where it adds value beyond Razor tooling. [x]
- `anvil asset`. [x]
- `anvil ui`. [x]
- `anvil check`. [x]
- `anvil routes`. [x]
- `anvil publish`. [x]

### Static Export

- Render selected routes to static HTML. [x]
- Copy referenced assets. [x]
- Support static route parameters. [x]
- Fail clearly for server-only routes. [x]
- Generate a deployable static directory. [x]

### OpenAPI

- API route discovery. [x]
- Request schema generation. [x]
- Response schema generation. [x]
- Authentication metadata. [x]
- OpenAPI document generation. [x]

### Localization

- Resource-based translations. [x]
- Current culture selection. [x]
- Number and date formatting. [x]
- Localized validation errors. [x]
- Localized email content. [x]

### Background Jobs

- Background task abstraction. [x]
- Application lifecycle integration. [x]
- Cancellation. [x]
- Retry policy. [x]
- Persistent job store contract for external providers. [x]

### Authentication Providers

- OAuth provider abstraction. [x]
- Passkey abstraction. [x]
- Multi-factor authentication abstraction. [x]
- Password reset. [x]
- Account-management examples. [x]

### Other Roadmap Features

- Client-side navigation. [x]
- Prefetching. [x]
- Image optimization and resizing hooks. [x]
- Markdown support. [x]
- Rate limiting and compression helpers. [x]
- Islands architecture boundary. [x]
- WebTransport abstraction. [x]
- Larger UI blocks and CRUD application templates. [x]

## C# Translation Of Topcoat Concepts

| Topcoat concept | Anvil approach |
| --- | --- |
| `view!` | Razor markup |
| `#[component]` | Razor component |
| `#[page]` | Razor `@page` or page convention |
| `#[route]` | ASP.NET Core endpoint or route attribute |
| `#[layout]` | Razor layout |
| `#[layer]` | ASP.NET Core middleware or endpoint filter |
| `Cx` | `RequestContext` plus normal DI |
| `app_context` | ASP.NET Core DI singleton/scoped services |
| `memoize` | Request-scoped memoization service |
| `href!` | Typed route/link generator |
| `signal` | Explicit browser signal abstraction |
| `procedure` | Typed server procedure endpoint |
| `shard` | Server-rendered partial component |
| `live!` | Streaming/live component abstraction |
| `asset!` | Asset manifest and Razor asset helper |
| `topcoat ui` | `anvil ui` component copier |

## Milestones

### Milestone 1: Usable Framework

- Razor server rendering.
- Pages and layouts.
- Routing and route parameters.
- Query, form, and JSON binding.
- Dependency injection.
- Request context.
- Errors and 404 pages.
- Static assets.
- Antiforgery defaults.
- `anvil new`.
- `anvil dev`.
- Production publishing.
- Complete sample application.

### Milestone 2: Productive Framework

- Typed links.
- Request memoization.
- Cookies.
- Sessions.
- Validation.
- Streaming.
- Suspense.
- Error boundaries.
- Local browser state.
- Server procedures.
- Partial server-rendered updates.
- Tailwind.
- Editable UI components.
- Mail.

### Milestone 3: Full-Stack Framework

- Signals.
- Shards.
- SSE.
- WebSockets.
- HTMX.
- Alpine AJAX.
- Datastar.
- Fonts.
- Icons.
- Multipart.
- Sitemaps.
- OpenAPI.
- Static export.

### Milestone 4: Advanced Features

- Client navigation.
- Prefetching.
- Image processing.
- Localization.
- Background jobs.
- Authentication providers.
- Islands.
- WebTransport.
- Larger UI blocks.

## P4: Production Hardening

The feature milestones above describe framework capability. The following
hardening work keeps those capabilities safe, discoverable, and verifiable in
real applications.

### Fragment and HTMX Hardening

- Configurable HTMX asset source with a pinned default. [x]
- Bundle the pinned HTMX runtime as an Anvil static asset. [x]
- Typed request binding for fragment POST endpoints. [x]
- OpenAPI metadata for fragment endpoints. [x]
- Shared Razor rendering between fragments and server shards. [x]
- Browser navigation and form contract verification keeps full pages and fragments separate. [x]

### Sample Application Completeness

- Working product creation and deletion flows. [x]
- Working workspace settings mutation. [x]
- Working account-management mutations. [x]
- Authentication, persistence, upload, and email integration examples. [x]

### Release Verification and Documentation

- Clean-process CI build and test verification for all projects. [x]
- CLI template and command smoke tests in CI. [x]
- Complete fragment, HTMX, shard, and signal documentation. [x]

## P5: Production Readiness

P5 moves Anvil from a complete feature prototype toward a framework that is
straightforward to publish, secure, upgrade, and validate in production.

### Packaging and Releases

- NuGet package metadata for Raukeld.Anvil and Raukeld.Anvil.Razor. [x]
- Package build validation and local package smoke tests. [x]
- Versioning and release documentation. [x]

### Application Guidance

- Database persistence guidance and sample boundary. [x]
- Upgrade and migration guidance. [x]
- Complete CRUD and CLI template guidance. [x]

### Quality and Security

- Browser interaction contract verification for navigation, forms, fragments, and modals. [x]
- Accessibility contract verification for sample workflows. [x]
- Response-size smoke tests for full pages and fragments. [x]
- Security review and dependency/license audit. [x]

## Definition Of Success

Anvil is successful when a developer can build a real CRUD application with
Razor pages, database access, forms, validation, authentication, sessions, file
uploads, search with partial updates, email, static assets, and production
deployment without creating:

- A separate API project.
- A separate frontend project.
- A JavaScript build pipeline.
- Manual route tables.
- Repetitive DTO plumbing for every page.
- Custom middleware for ordinary authentication checks.
- Hand-written internal URL concatenation.
- Framework-specific boilerplate before rendering the first page.

## Non-Goals

- Replacing ASP.NET Core's HTTP server.
- Replacing ASP.NET Core dependency injection.
- Creating a new ORM.
- Translating arbitrary C# to JavaScript.
- Requiring a custom template language.
- Hiding database operations behind framework magic.
- Forcing a specific authentication provider.
- Making every feature part of the core package.

## Dotisan Merge Plan

Anvil is intended to become the C# and Razor server-rendered counterpart to
Dotisan. Dotisan's current source and design documents are maintained at
https://github.com/RudiStorm/dotisan-code. This section tracks the work needed
to bring Dotisan's production application foundation into Anvil without making
Vue, Vite, or a separate frontend application mandatory.

### Merge Principles

- Keep ASP.NET Core, EF Core, Identity, configuration, middleware, OpenTelemetry, and HttpClient as the underlying standard technologies.
- Keep generated application code inspectable, editable, and runnable after Anvil is removed.
- Use Razor pages and components as the default UI instead of Vue pages and stores.
- Use Anvil partial forms, fragments, shards, signals, SSE, and WebSockets for progressive enhancement and targeted updates.
- Prefer compile-time or explicit endpoint discovery over runtime reflection.
- Keep persistence, domain behavior, authorization rules, and compliance policy application-owned.
- Do not claim a capability is production-ready while its default implementation is process-local, in-memory, or example-only.

### Current Dotisan Capabilities To Merge

- EF Core persistence for SQLite, SQL Server, PostgreSQL, and MySQL.
- Standard ASP.NET Core Identity with cookie authentication and antiforgery.
- Registration policies, email confirmation, password reset, TOTP MFA, recovery codes, session management, and external login boundaries.
- Role claims, named policies, code-defined permissions, and explicit endpoint authorization metadata.
- Durable EF-backed audit entries with actor, tenant, resource, action, changed fields, trace ID, and correlation ID.
- Optional shared-schema multi-tenancy with tenant filtering and cross-tenant tests.
- Durable Wolverine jobs, delayed messages, retries, recurring schedules, error handling, and transactional outbox integration.
- OpenTelemetry tracing and metrics with OTLP and Aspire Dashboard development support.
- Notifications, storage, imports, exports, caching, webhooks, and readiness labels for optional integrations.
- Canonical endpoint contracts that drive OpenAPI and generated artifacts.
- Resource, endpoint, and CRUD scaffolding.
- Production diagnostics, Docker/Compose generation, migration commands, and clean-machine release verification.

### Zero-Boilerplate Default

- `anvil new` defaults to the Identity profile. [x]
- Generated applications include EF Core, Identity, audit, tenancy, jobs, mail, observability, OpenAPI, health checks, and production configuration. [x]
- `anvil new` restores packages, restores the pinned EF tool, creates the initial migration, generates manifests, and builds before reporting success. [x]
- `--profile default` and `--no-restore` remain explicit opt-outs for minimal or offline generation. [x]
- A generated application can run without manually adding Identity, audit, persistence, or endpoint-registration boilerplate. [x]

### Phase 1: Endpoint Contract Foundation

- Replace the lightweight manual `AnvilApiRegistry` as the sole contract source with a canonical endpoint manifest. [x]
- Define endpoint IDs, feature names, methods, routes, request and response types, validation, authorization, permissions, tags, versions, idempotency, rate limits, and deprecation metadata. [x]
- Generate OpenAPI, route diagnostics, authorization metadata, and API documentation from the same manifest. [x]
- Add deterministic contract output and stale-contract verification. [x]
- Preserve explicit endpoint registration and avoid normal runtime assembly scanning. [x]

### Phase 2: Persistence Foundation

- Add generated EF Core application configuration while keeping EF Core outside the Anvil core abstraction boundary. [x]
- Support SQLite by default and SQL Server, PostgreSQL, and MySQL through explicit provider selection. [x]
- Add `anvil migrate`, migration status, and dry-run workflows. [x]
- Keep migration authoring explicit through `dotnet ef migrations add`. [x]
- Ensure production commands only apply reviewed, committed migrations. [x]
- Add database health checks and disposable integration-test database support. [x]

### Phase 3: Authentication

- Add optional standard ASP.NET Core Identity integration. [x]
- Add `ApplicationUser`, Identity EF stores, cookie authentication, login, logout, current-user, and registration policy endpoints. [x]
- Add public, invite-only, and disabled registration modes. [x]
- Add email confirmation, password reset, lockout, TOTP MFA, recovery codes, session/device management, and session revocation. [x]
- Add external login and OAuth provider extension points. [x]
- Keep Anvil's existing application-owned session primitives available for applications that do not select Identity. [x]
- Require antiforgery on cookie-authenticated state-changing endpoints. [x]
- Require persistent Data Protection keys and secure production cookie configuration. [x]

### Phase 4: Authorization and Permissions

- Replace session-value-only role checks with standard ASP.NET Core claims and policies for Identity applications. [x]
- Generate editable permission constants such as `customers.view`, `customers.create`, `customers.update`, and `customers.delete`. [x]
- Add named policy registration and endpoint permission metadata. [x]
- Protect Razor pages, forms, fragments, procedures, shards, APIs, and background operations consistently. [x]
- Add administrative permission management endpoints and Razor pages. [x]
- Verify anonymous access returns `401` and authenticated access without permission returns `403`. [x]

### Phase 5: Audit and Compliance

- Add an EF-backed `AuditEntry`, `IAuditWriter`, and application-owned audit writer. [x]
- Enable audit writes by default through standard ASP.NET Core configuration. [x]
- Capture actor, tenant, timestamp, resource, resource ID, operation, changed fields, trace ID, and correlation ID. [x]
- Audit authentication, authorization administration, CRUD mutations, exports, file access, configuration changes, job execution, and webhook replay. [x]
- Never record passwords, tokens, or raw credentials. [x]
- Add audit retention, export, access control, and tamper-evidence guidance. [x]
- Add compliance workflows for consent, privacy requests, data export, deletion/anonymization, retention, and legal holds where required by the application. [x]

### Phase 6: Tenancy

- Add an explicit `TenantContext` and tenant-resolution boundary. [x]
- Support shared database/shared schema tenancy with a required tenant identifier. [x]
- Reject header-only tenant identity in production. [x]
- Apply tenant filters to collection, read, create, update, delete, export, audit, and job operations. [x]
- Add cross-tenant isolation tests for every generated resource. [x]

### Phase 7: Durable Jobs and Scheduling

- Add an optional durable-provider integration for durable local queues, delayed delivery, retries, recurring jobs, and failure replay. [x]
- Add transactional outbox integration with EF Core. [x]
- Propagate actor and tenant context into jobs where appropriate. [x]
- Add `anvil jobs status` and `anvil schedule list`. [x]
- Keep Anvil's in-memory queue explicitly development/test-only. [x]
- Add idempotency, dead-letter, cancellation, and job-history guidance. [x]

### Phase 8: Integrations and Operations

- Add OpenTelemetry tracing, metrics, correlation IDs, OTLP configuration, and Aspire Dashboard development support. [x]
- Add authenticated notifications with persistence, pagination, retention, and a delivery boundary. [x]
- Add bounded storage uploads, ownership metadata, local storage, and provider-neutral external storage boundaries. [x]
- Add durable CSV/JSON imports and exports with tenant ownership and status tracking. [x]
- Add signed, durable, retryable webhooks with authorized replay. [x]
- Add cache namespacing and distributed-cache guidance. [x]
- Label optional integrations as production foundation, development adapter, or example-only. [x]

### Phase 9: Razor Application Scaffolding

- Add `anvil make resource`, `anvil make endpoint`, and `anvil make crud`. [x]
- Generate editable Razor list, detail, create, edit, and delete pages. [x]
- Generate search, pagination, sorting, loading, empty, error, validation, authorization, audit, and tenant-isolation behavior. [x]
- Generate partial table updates through `data-anvil-partial-form` and targeted fragment endpoints. [x]
- Generate typed route/link definitions and endpoint integration tests. [x]
- Generate a polished Identity starter experience with organized public, auth, and authenticated dashboard routes, automatic initial migration application, and explicit route authorization metadata. [x]
- Generate an editable shadcn-style Razor control catalog, native progressive-enhancement fallbacks, a public component showcase, and starter pages that demonstrate the generated controls without a Node build pipeline. [x]
- Do not generate Vue, Vite, Pinia, TanStack Query, Zod, or TypeScript for the default application. [x]

### Phase 10: CLI and Project Experience

- Add `anvil run`, `anvil generate`, `anvil generate --check`, `anvil migrate`, `anvil doctor`, `anvil mail`, `anvil jobs`, and `anvil schedule`. [x]
- Add canonical space-separated command forms while retaining documented compatibility aliases where necessary. [x]
- Add `anvil doctor --production` with PASS, WARNING, and BLOCKING results. [x]
- Check migrations, database configuration, Identity, Data Protection, HTTPS, Docker, health endpoints, mail, jobs, observability, and secrets configuration. [x]
- Keep project generation side-effect boundaries explicit and testable. [x]

### Phase 11: Deployment and Release Readiness

- Generate non-root production Dockerfiles and provider-specific Compose files. [x]
- Pin runtime and infrastructure image versions. [x]
- Support Mailpit for development and SMTP or application-owned providers for production. [x]
- Add persistent Data Protection key guidance, HTTPS/HSTS, CSP, trusted proxy configuration, health/readiness endpoints, structured logs, backups, and rollback guidance. [x]
- Add clean-machine generated-project tests, package smoke tests, migration checks, browser interaction tests, accessibility checks, and response-size checks. [x]

### Dotisan Merge Acceptance Gate

The merge is complete when the following workflow succeeds for a clean generated
application:

```text
anvil new MyApp --profile identity
anvil make resource Customer
anvil make crud Customer
anvil generate
anvil generate --check
dotnet ef migrations add InitialCreate --project src/MyApp
anvil migrate
dotnet build
dotnet test
anvil doctor --production
```

The generated application must provide authenticated Razor pages, policy-based
authorization, durable audit records, tenant-safe CRUD, server-side validation,
partial HTML updates, OpenAPI metadata, and production diagnostics without a
Vue frontend or a separate API project.
## P6: Anvil Store Sample

The Anvil Store is the first production-shaped marketplace sample. It distributes
free and open-source editable source archives for Anvil templates and providers.

- Store implementation plan and package contract documented in `agent-store-plan.md`. [x]
- Single-tenant `samples/Anvil.Store` application foundation with SQLite persistence and Identity. [x]
- Creator, moderator, administrator, and public storefront route boundaries. [x]
- Constrained ZIP package manifest and archive validation boundary. [x]
- Local quarantine, accepted, and published artifact storage boundary. [x]
- ClamAV malware scanning adapter with explicit development-only scanner. [x]
- Railway Dockerfile, non-root runtime entrypoint, persistent `/app/data` volume
  layout, and health-check deployment configuration. [x]
- Manual release review, approval, immutable artifact publication, and checksums. [x]
- Public template/provider catalog, listing details, search, creator profiles, and sitemap. [x]
- Authenticated download history, favorites, and eligible reviews. [ ]
- Complete administration, moderation reports, notifications, and operations UI. [ ]
- Full browser, accessibility, upload-security, authorization, and production verification suite. [ ]
- Public package API and CLI template consumption. [ ]

- Store sign-in, registration, logout, browser redirects, and authenticated account navigation. [x]
- Protected Store dashboard route with catalog summary and authenticated workspace links. [x]
- Public creator landing page with protected creator submission actions. [x]
- Copyable future CLI installation commands displayed on template and provider listings; CLI implementation remains deferred. [x]
- Any authenticated user can begin creator onboarding; first upload creates the creator profile and role. [x]
- First non-seeded registered account bootstrap and administrator dashboard navigation. [x]
- Administrator catalog and user management workflows. [x]
- Replace SQLite with a production relational provider before public launch. [ ]
