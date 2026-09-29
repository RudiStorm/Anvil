using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anvil;

public sealed record AnvilSchedule(
    string Name,
    string JobName,
    string Cron,
    bool Enabled = true,
    DateTimeOffset? NextRunAt = null);

public interface IAnvilScheduleRegistry
{
    ValueTask RegisterAsync(AnvilSchedule schedule, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<AnvilSchedule>> ListAsync(CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<AnvilSchedule>> ClaimDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}

public sealed class AnvilJsonScheduleRegistry(IOptions<AnvilBackgroundOptions> options) : IAnvilScheduleRegistry
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private string Path => System.IO.Path.ChangeExtension(options.Value.StorePath, ".schedules.json");

    public async ValueTask RegisterAsync(AnvilSchedule schedule, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schedule.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(schedule.JobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(schedule.Cron);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var schedules = await ReadAsync(cancellationToken);
            schedules.RemoveAll(item => item.Name == schedule.Name);
            schedules.Add(schedule);
            await WriteAsync(schedules, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<AnvilSchedule>> ListAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try { return (await ReadAsync(cancellationToken)).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray(); }
        finally { gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<AnvilSchedule>> ClaimDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var schedules = await ReadAsync(cancellationToken);
            var due = schedules.Where(item => item.Enabled && item.NextRunAt is not null && item.NextRunAt <= now).ToArray();
            foreach (var schedule in due)
            {
                var index = schedules.FindIndex(item => item.Name == schedule.Name);
                schedules[index] = schedule with { NextRunAt = null };
            }
            if (due.Length > 0) await WriteAsync(schedules, cancellationToken);
            return due;
        }
        finally { gate.Release(); }
    }

    private async Task<List<AnvilSchedule>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Path)) return [];
        await using var stream = File.OpenRead(Path);
        return await JsonSerializer.DeserializeAsync<List<AnvilSchedule>>(stream, jsonOptions, cancellationToken) ?? [];
    }

    private async Task WriteAsync(List<AnvilSchedule> schedules, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
        var temporary = Path + ".tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, schedules, jsonOptions, cancellationToken);
        File.Move(temporary, Path, true);
    }
}

public static class AnvilSchedulingServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilScheduling(this IServiceCollection services)
    {
        services.AddSingleton<AnvilJsonScheduleRegistry>();
        services.AddSingleton<IAnvilScheduleRegistry>(provider => provider.GetRequiredService<AnvilJsonScheduleRegistry>());
        return services;
    }
}
