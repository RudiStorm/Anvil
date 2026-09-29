using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class MergePlanTests
{
    [Fact]
    public void Endpoint_manifest_is_normalized_sorted_and_rejects_duplicate_ids()
    {
        var manifest = new AnvilEndpointManifest();
        manifest.Add(new AnvilEndpointManifestEntry("z", "feature", null, "post", "/z", null, null, false, null, null, ["z", "a"], false, false, null));
        manifest.Add(new AnvilEndpointManifestEntry("a", "feature", null, "get", "/a", null, null, false, null, null, [], false, true, null));

        Assert.Equal(["a", "z"], manifest.Build().Select(entry => entry.Id));
        Assert.Equal(["a", "z"], manifest.Build()[1].Tags);

        manifest.Add(new AnvilEndpointManifestEntry("a", "feature", null, "get", "/duplicate", null, null, false, null, null, [], false, false, null));
        var error = Assert.Throws<InvalidOperationException>(() => manifest.Build());
        Assert.Contains("Duplicate endpoint ID 'a'", error.Message);
    }

    [Fact]
    public async Task Durable_store_persists_jobs_and_moves_failed_jobs_to_dead_letter()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anvil-jobs", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new AnvilBackgroundOptions
        {
            StorePath = Path.Combine(directory, "jobs.json"),
            MaxAttempts = 1,
            RetryDelay = TimeSpan.Zero
        });
        var job = new AnvilBackgroundJob("job-1", "sync", "{}", 0, DateTimeOffset.UtcNow);

        try
        {
            var store = new AnvilJsonBackgroundJobStore(options);
            await store.EnqueueAsync(job);
            Assert.Equal(job, await store.DequeueAsync());
            await store.FailAsync(job, new InvalidOperationException("failed"));

            var items = await store.ListAsync();
            var item = Assert.Single(items);
            Assert.Equal(AnvilBackgroundJobStatus.DeadLetter, item.Status);
            Assert.Equal("failed", item.Error);
            Assert.True(File.Exists(options.Value.StorePath));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Schedule_registry_replaces_by_name_and_lists_deterministically()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anvil-schedules", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new AnvilBackgroundOptions { StorePath = Path.Combine(directory, "jobs.json") });
        try
        {
            var registry = new AnvilJsonScheduleRegistry(options);
            await registry.RegisterAsync(new AnvilSchedule("z", "job-z", "0 * * * *"));
            await registry.RegisterAsync(new AnvilSchedule("a", "job-a", "*/5 * * * *"));
            await registry.RegisterAsync(new AnvilSchedule("z", "job-new", "0 0 * * *"));

            var schedules = await registry.ListAsync();
            Assert.Equal(["a", "z"], schedules.Select(schedule => schedule.Name));
            Assert.Equal("job-new", schedules[1].JobName);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Durable_store_supports_delayed_retry_replay_and_persistent_history()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anvil-jobs", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new AnvilBackgroundOptions
        {
            StorePath = Path.Combine(directory, "jobs.json"), MaxAttempts = 2,
            RetryDelay = TimeSpan.FromHours(1)
        });
        try
        {
            var job = new AnvilBackgroundJob("delayed", "sync", "{}", 0, DateTimeOffset.UtcNow.AddHours(1));
            var store = new AnvilJsonBackgroundJobStore(options);
            await store.EnqueueAsync(job);
            Assert.Null(await store.DequeueAsync());

            var due = job with { AvailableAt = DateTimeOffset.UtcNow };
            await store.EnqueueAsync(due);
            Assert.Equal(due, await store.DequeueAsync());
            await store.FailAsync(due, new InvalidOperationException("temporary"));
            Assert.Equal(AnvilBackgroundJobStatus.Pending, Assert.Single(await store.ListAsync()).Status);
            Assert.Equal(2, (await store.HistoryAsync(job.Id)).Count);

            await store.ReplayAsync(job.Id);
            var replayed = Assert.Single(await store.ListAsync());
            Assert.Equal(0, replayed.Job.Attempts);
            Assert.Equal(AnvilBackgroundJobStatus.Pending, replayed.Status);

            var reopened = new AnvilJsonBackgroundJobStore(options);
            Assert.Equal(3, (await reopened.HistoryAsync(job.Id)).Count);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Schedule_registry_claims_each_due_schedule_once()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anvil-schedules", Guid.NewGuid().ToString("N"));
        var registry = new AnvilJsonScheduleRegistry(Options.Create(new AnvilBackgroundOptions { StorePath = Path.Combine(directory, "jobs.json") }));
        try
        {
            await registry.RegisterAsync(new AnvilSchedule("hourly", "sync", "0 * * * *", NextRunAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
            Assert.Single(await registry.ClaimDueAsync(DateTimeOffset.UtcNow));
            Assert.Empty(await registry.ClaimDueAsync(DateTimeOffset.UtcNow));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Ef_outbox_enqueues_only_after_the_business_save_boundary()
    {
        var options = new DbContextOptionsBuilder<TestPersistenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new TestPersistenceDbContext(options);
        var jobs = new InMemoryAnvilBackgroundJobStore();
        var outbox = new AnvilEfTransactionalOutbox<TestPersistenceDbContext>(db, jobs);
        var id = await outbox.EnqueueAsync("sync", "{}", tenantId: "tenant-1", actorId: "actor-1");
        Assert.Equal(id, Assert.Single(db.ChangeTracker.Entries<AnvilOutboxMessage>()).Entity.Id);
        Assert.Null(await jobs.DequeueAsync());

        await db.SaveChangesAsync();
        Assert.Equal(1, await outbox.DispatchAsync());
        var job = await jobs.DequeueAsync();
        Assert.NotNull(job);
        Assert.Equal("tenant-1", job.TenantId);
        Assert.Equal("actor-1", job.ActorId);
    }

    [Fact]
    public async Task Observability_rejects_unsafe_correlation_ids_and_emits_a_safe_header()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[AnvilTelemetry.CorrelationHeader] = "bad value\r\n";

        var correlationId = AnvilTelemetry.GetOrCreateCorrelationId(context);

        Assert.DoesNotContain(' ', correlationId);
        Assert.Equal(correlationId, context.Response.Headers[AnvilTelemetry.CorrelationHeader].ToString());
        await Task.CompletedTask;
    }

    [Fact]
    public void Production_configuration_registers_persistent_keys_and_health_checks()
    {
        var keys = Path.Combine(Path.GetTempPath(), "anvil-production", Guid.NewGuid().ToString("N"));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnvilProduction(options =>
        {
            options.DataProtectionKeysPath = keys;
            options.LivenessPath = "/live";
            options.ReadinessPath = "/ready";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AnvilProductionOptions>>().Value;
        Assert.Equal(keys, options.DataProtectionKeysPath);
        Assert.NotNull(provider.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>());
    }

    [Theory]
    [InlineData(AnvilDatabaseProvider.Sqlite, "Microsoft.EntityFrameworkCore.Sqlite", "UseSqlite")]
    [InlineData(AnvilDatabaseProvider.SqlServer, "Microsoft.EntityFrameworkCore.SqlServer", "UseSqlServer")]
    [InlineData(AnvilDatabaseProvider.PostgreSql, "Npgsql.EntityFrameworkCore.PostgreSQL", "UseNpgsql")]
    [InlineData(AnvilDatabaseProvider.MySql, "Pomelo.EntityFrameworkCore.MySql", "UseMySql")]
    public void Provider_metadata_is_explicit(AnvilDatabaseProvider provider, string package, string method)
    {
        Assert.Equal(package, provider.GetPackageId());
        Assert.Equal(method, provider.GetConfigurationMethod());
    }

    [Theory]
    [InlineData(AnvilDatabaseProvider.Sqlite)]
    [InlineData(AnvilDatabaseProvider.SqlServer)]
    [InlineData(AnvilDatabaseProvider.PostgreSql)]
    [InlineData(AnvilDatabaseProvider.MySql)]
    public void Named_provider_registration_stamps_the_selected_provider(AnvilDatabaseProvider provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "provider-tests" })
            .Build();
        var services = new ServiceCollection();
        Action<DbContextOptionsBuilder, string> callback = (builder, connectionString) => builder.UseInMemoryDatabase(connectionString);

        _ = provider switch
        {
            AnvilDatabaseProvider.Sqlite => services.AddAnvilSqlitePersistence<TestPersistenceDbContext>(configuration, callback),
            AnvilDatabaseProvider.SqlServer => services.AddAnvilSqlServerPersistence<TestPersistenceDbContext>(configuration, callback),
            AnvilDatabaseProvider.PostgreSql => services.AddAnvilPostgreSqlPersistence<TestPersistenceDbContext>(configuration, callback),
            AnvilDatabaseProvider.MySql => services.AddAnvilMySqlPersistence<TestPersistenceDbContext>(configuration, callback),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        using var serviceProvider = services.BuildServiceProvider();
        Assert.Equal(provider, serviceProvider.GetRequiredService<IOptions<AnvilPersistenceOptions>>().Value.Provider);
    }

    [Fact]
    public async Task Persistence_registration_reads_named_connection_and_preserves_provider_options()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Test"] = "persistence-tests"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddAnvilPersistence<TestPersistenceDbContext>(
            configuration,
            options =>
            {
                options.ConnectionStringName = "Test";
                options.Provider = AnvilDatabaseProvider.SqlServer;
                options.EnableDetailedErrors = true;
            },
            (builder, connectionString) => builder.UseInMemoryDatabase(connectionString));

        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AnvilPersistenceOptions>>().Value;
        Assert.Equal(AnvilDatabaseProvider.SqlServer, options.Provider);
        await using var context = provider.GetRequiredService<TestPersistenceDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", context.Database.ProviderName);
    }

    private sealed class TestPersistenceDbContext(DbContextOptions<TestPersistenceDbContext> options)
        : DbContext(options)
    {
        public DbSet<AnvilOutboxMessage> OutboxMessages => Set<AnvilOutboxMessage>();
    }
}
