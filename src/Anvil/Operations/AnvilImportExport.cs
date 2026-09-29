using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anvil;

public enum AnvilTransferStatus { Queued, Running, Completed, Failed }
public sealed record AnvilTransfer(string Id, string OwnerId, string Format, AnvilTransferStatus Status, string? Path, string? Error, DateTimeOffset CreatedAt);

public interface IAnvilTransferStore
{
    Task<AnvilTransfer> StartAsync(string ownerId, string format, CancellationToken cancellationToken = default);
    Task CompleteAsync(string id, string path, CancellationToken cancellationToken = default);
    Task FailAsync(string id, string error, CancellationToken cancellationToken = default);
    Task<AnvilTransfer?> GetAsync(string ownerId, string id, CancellationToken cancellationToken = default);
}

public sealed class FileAnvilTransferStore(IOptions<AnvilStorageOptions> options) : IAnvilTransferStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string Root => Path.Combine(options.Value.RootPath, "transfers");
    public async Task<AnvilTransfer> StartAsync(string ownerId, string format, CancellationToken cancellationToken = default)
    {
        if (format is not ("csv" or "json")) throw new ArgumentException("Format must be csv or json.", nameof(format));
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        var transfer = new AnvilTransfer(Guid.NewGuid().ToString("N"), ownerId, format, AnvilTransferStatus.Queued, null, null, DateTimeOffset.UtcNow);
        await PersistAsync(transfer, cancellationToken); return transfer;
    }
    public Task CompleteAsync(string id, string path, CancellationToken cancellationToken = default) => UpdateAsync(id, x => x with { Status = AnvilTransferStatus.Completed, Path = path }, cancellationToken);
    public Task FailAsync(string id, string error, CancellationToken cancellationToken = default) => UpdateAsync(id, x => x with { Status = AnvilTransferStatus.Failed, Error = error }, cancellationToken);
    public async Task<AnvilTransfer?> GetAsync(string ownerId, string id, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Root, id + ".json");
        var item = File.Exists(path) ? JsonSerializer.Deserialize<AnvilTransfer>(await File.ReadAllTextAsync(path, cancellationToken)) : null;
        return item?.OwnerId == ownerId ? item : null;
    }
    private async Task UpdateAsync(string id, Func<AnvilTransfer, AnvilTransfer> update, CancellationToken cancellationToken)
    { await gate.WaitAsync(cancellationToken); try { var x = await GetAsync(null, id, cancellationToken, ignoreOwner: true) ?? throw new KeyNotFoundException(id); await PersistAsync(update(x), cancellationToken); } finally { gate.Release(); } }
    private async Task PersistAsync(AnvilTransfer item, CancellationToken cancellationToken) { Directory.CreateDirectory(Root); var path = Path.Combine(Root, item.Id + ".json"); var temporary = path + ".tmp"; await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(item), cancellationToken); File.Move(temporary, path, true); }
    private async Task<AnvilTransfer?> GetAsync(string? itemOwner, string id, CancellationToken cancellationToken, bool ignoreOwner = false)
    { var path = Path.Combine(Root, id + ".json"); if (!File.Exists(path)) return null; var item = JsonSerializer.Deserialize<AnvilTransfer>(await File.ReadAllTextAsync(path, cancellationToken)); return item is not null && (ignoreOwner || item.OwnerId == itemOwner) ? item : null; }
}

public static class AnvilImportExportServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilImportExport(this IServiceCollection services) => services.AddSingleton<IAnvilTransferStore, FileAnvilTransferStore>();
}
