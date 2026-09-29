# Anvil Starter Experience Design

## Goal

Make the default `anvil new` Identity application feel like a polished,
immediately usable starter product: a modern public landing page, working
registration and login screens, a protected dashboard, and an initialized
database.

## Scope

This applies to the default Identity profile only. `anvil new --profile default`
remains a minimal unauthenticated application. The existing Anvil runtime,
Identity services, and endpoint contracts are reused; the change is primarily
in generated application templates and project initialization.

## First-run behavior

After `anvil new MyApp` completes successfully, the generated application has:

- A public `/` landing page with a strong hero, product value proposition,
  feature cards, and links to registration and login.
- A rendered `/account/register` page under `Components/Pages/Auth` that posts to the existing Identity
  registration endpoint, displays validation/errors, and includes antiforgery.
- A rendered `/account/login` page under `Components/Pages/Auth` that posts to the existing Identity login
  endpoint, displays validation/errors, and includes antiforgery.
- A protected `/dashboard` page under `Components/Pages/App` with a polished starter dashboard shell,
  navigation, summary cards, and an authenticated-user affordance.
- A sign-out action using the existing Identity logout endpoint.
- An initial EF migration created and applied during `anvil new` initialization.

The scaffold does not seed a default account or password. The first user is
created through registration. Failed restore, migration creation, migration
application, or build steps return a non-zero result and an actionable error.

## Visual direction

The generated UI should have a modern shadcn-inspired feel without adding a
frontend build pipeline or shadcn dependency:

- Neutral zinc/slate palette with a restrained accent color.
- System/modern sans typography, strong hierarchy, and generous spacing.
- Rounded cards and controls, subtle borders, soft shadows, and clear focus
  states.
- Responsive layout for mobile and desktop.
- CSS variables and component-like utility classes so applications can easily
  customize the theme.
- Dark-mode-ready color variables, without requiring a client-side theme
  runtime for the initial scaffold.

## Route and authorization behavior

- Generated route files are organized by access boundary:

  ```text
  Components/Pages/Public/Home.razor
  Components/Pages/Auth/Login.razor
  Components/Pages/Auth/Register.razor
  Components/Pages/App/Dashboard.razor
  ```

- Every generated route declares its access policy explicitly with Razor
  authorization metadata; folder names communicate structure but are not the
  security mechanism.
- `Public/Home.razor` declares anonymous access for `/`.
- `Auth/Login.razor` and `Auth/Register.razor` declare anonymous access for
  `/account/login` and `/account/register`.
- `App/Dashboard.razor` declares authorization for `/dashboard` and redirects
  anonymous users to login.
- Authenticated pages expose the current user name/email and a sign-out action.
- Existing API Identity endpoints remain available and continue to be the
  source of authentication behavior.

## Database initialization

The generated project continues to create `InitialCreate` with `dotnet ef`.
`anvil new` then runs `dotnet ef database update` against the configured
connection string before the generated project build. This is scaffold-time
initialization only; application startup must not automatically apply pending
migrations in production.

## Verification

Tests must cover:

- Generated Identity projects contain the landing, auth, and dashboard files.
- Generated Identity projects register Identity and expose the expected routes.
- Generated default-profile projects do not include Identity-only pages or
  migration initialization.
- The initialization command sequence includes migration creation followed by
  database update and build.
- The generated package-mode project uses the published Raukeld package IDs.
- The dashboard rejects anonymous access and is reachable after authentication.
