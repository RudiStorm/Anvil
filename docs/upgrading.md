# Upgrading Anvil

Anvil and Anvil.Razor should be upgraded together at the same version. Before
upgrading:

1. Run the existing test suite and record the package versions.
2. Update both package references or project references together.
3. Review changed endpoint and browser-runtime behavior.
4. Run `dotnet build`, `dotnet test`, and the sample application smoke checks.

The HTMX runtime is bundled by Anvil.Razor. Applications that override
`AnvilRuntime.HtmxSource` should review that override when upgrading HTMX.

Avoid changing persisted session or cookie formats without an explicit
migration. Session stores and application-owned database schemas remain the
responsibility of the application.
