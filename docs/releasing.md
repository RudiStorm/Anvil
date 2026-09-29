# Releasing Anvil

Anvil packages are versioned together. Update `Version` in both
`src/Anvil/Anvil.csproj` and `src/Anvil.Razor/Anvil.Razor.csproj`, then run:

```sh
dotnet restore Anvil.slnx
dotnet test Anvil.slnx --no-restore
dotnet pack src/Anvil/Anvil.csproj --no-build --output ./artifacts
dotnet pack src/Anvil.Razor/Anvil.Razor.csproj --no-build --output ./artifacts
```

Inspect both packages before publishing. `Anvil.Razor` should contain its
static web assets, including the bundled HTMX runtime.

Do not publish packages containing local application configuration, credentials,
or generated sample data.
