# Anvil Documentation

Start with [`AnvilDocumentation.md`](../AnvilDocumentation.md) for the complete
agent-friendly framework reference. [`reference.md`](reference.md) contains the
same public API material in the focused documentation site format.
and the sample route map.

Focused guides:

- [`reference.md`](reference.md): rendering, routing, request context, forms,
  fragments, browser runtime, Identity, authorization, tenancy, audit,
  compliance, jobs, operations, mail, localization, production, and testing.
- [`crud.md`](crud.md): application-owned CRUD boundaries and generated Razor
  workflow guidance.
- [`persistence.md`](persistence.md): EF Core registration and migration
  boundaries.
- [`upgrading.md`](upgrading.md): upgrade and compatibility guidance.
- [`releasing.md`](releasing.md): package, release, and acceptance checks.

The reference application in `samples/Anvil.Sample` is the executable example.
It intentionally demonstrates both progressive enhancement and ordinary
full-page fallback so applications can adopt features incrementally.
