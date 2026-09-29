# Persistence Guidance

Anvil intentionally does not hide an ORM. Keep the context, entities, migrations,
and persistence services in the application and inject those services into Razor
components and request handlers. `AddAnvilPersistence` only supplies safe
registration and configuration plumbing; provider packages remain application
dependencies.

SQLite is the generated application's default. The generated project includes
`Microsoft.EntityFrameworkCore.Sqlite`, an application-owned `AppDbContext`, and
this registration:

```csharp
builder.Services.AddAnvilPersistence<AppDbContext>(
    builder.Configuration,
    options => options.Provider = AnvilDatabaseProvider.Sqlite,
    (options, connectionString) => options.UseSqlite(connectionString));
```

Generated applications can select a provider explicitly with
`anvil new MyApp --database <sqlite|sqlserver|postgresql|mysql>`. The generated
project references only that provider package and uses a matching named API:

```csharp
builder.Services.AddAnvilSqlServerPersistence<AppDbContext>(
    builder.Configuration,
    (options, connectionString) => options.UseSqlServer(connectionString));
```

The equivalent APIs are `AddAnvilSqlitePersistence`,
`AddAnvilPostgreSqlPersistence`, and `AddAnvilMySqlPersistence`. They keep the
provider package in the application while recording the explicit provider in
`AnvilPersistenceOptions` for diagnostics.

Select other providers explicitly and add their EF Core package to the
application. Do not add all providers to Anvil or select a provider from an
untrusted configuration value.

```csharp
// SQL Server: Microsoft.EntityFrameworkCore.SqlServer
options => options.UseSqlServer(connectionString)

// PostgreSQL: Npgsql.EntityFrameworkCore.PostgreSQL
options => options.UseNpgsql(connectionString)

// MySQL: Pomelo.EntityFrameworkCore.MySql
options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
```

`ConnectionStrings:DefaultConnection` is required. The registration fails at
startup when it is missing, which also makes EF design-time failures clear.
Keep connection strings in user secrets or deployment configuration, not in
source control.

## Migrations

Migration authoring is explicit and reviewable:

```sh
dotnet ef migrations add InitialCreate --project MyApp.csproj
anvil migrate status
anvil migrate --dry-run
anvil migrate
```

Commit migration files and review them like application code. `anvil migrate`
only applies an existing `Migrations` directory; it never creates a migration.
Run it as a deployment step after the application has been built, and do not
use `EnsureCreated` for a relational production database.

Load data in the request handler or component, validate mutation models on the
server, and keep authorization checks next to the mutation. Do not expose a
database context directly to browser-visible state.

The sample uses an in-memory catalog only to keep the reference application
self-contained. Replace it with an application-owned context and repository for
production. The EF InMemory provider is suitable for isolated unit tests, not
for relational behavior or production data.
