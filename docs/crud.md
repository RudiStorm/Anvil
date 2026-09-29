# CRUD Guidance

Use a Razor page for the full resource view and a focused Razor component for
replaceable regions:

```csharp
app.MapAnvilFragmentGet<ProductRows>(
    "/products/rows",
    context => LoadProductParametersAsync(context));
```

Use typed fragment POST endpoints for mutations that should update a region,
and use normal POST/redirect/GET for mutations that change the current page or
require a durable navigation result. Validate and authorize every mutation on
the server.

The sample inventory page demonstrates both patterns: HTMX updates the table
rows while the server owns product validation and mutation behavior.
