using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anvil;

public sealed record AnvilStoredFile(string Id, string FileName, string ContentType, long Length, string OwnerId, DateTimeOffset CreatedAt);
public sealed class AnvilStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "storage");
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
}

public interface IAnvilStorage
{
    Task<AnvilStoredFile> SaveAsync(string ownerId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string ownerId, string id, CancellationToken cancellationToken = default);
    Task DeleteAsync(string ownerId, string id, CancellationToken cancellationToken = default);
}

public sealed class LocalAnvilStorage(IOptions<AnvilStorageOptions> options) : IAnvilStorage
{
    private readonly AnvilStorageOptions options = options.Value;

    public async Task<AnvilStoredFile> SaveAsync(string ownerId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        if (options.MaxBytes <= 0) throw new InvalidOperationException("Storage MaxBytes must be positive.");
        var id = Guid.NewGuid().ToString("N");
        var ownerPath = Path.Combine(options.RootPath, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ownerId))).ToLowerInvariant());
        Directory.CreateDirectory(ownerPath);
        var path = Path.Combine(ownerPath, id);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            length += read;
            if (length > options.MaxBytes) { output.Close(); File.Delete(path); throw new InvalidDataException("The upload exceeds the configured size limit."); }
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var metadata = new AnvilStoredFile(id, Path.GetFileName(fileName), contentType ?? "application/octet-stream", length, ownerId, DateTimeOffset.UtcNow);
        await File.WriteAllTextAsync(path + ".json", JsonSerializer.Serialize(metadata), cancellationToken);
        return metadata;
    }

    public async Task<Stream?> OpenReadAsync(string ownerId, string id, CancellationToken cancellationToken = default)
    {
        var path = GetPath(ownerId, id);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous) : null;
    }

    public Task DeleteAsync(string ownerId, string id, CancellationToken cancellationToken = default)
    {
        var path = GetPath(ownerId, id); if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".json")) File.Delete(path + ".json"); return Task.CompletedTask;
    }

    private string GetPath(string ownerId, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Invalid storage identifier.", nameof(id));
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ownerId))).ToLowerInvariant();
        return Path.Combine(options.RootPath, hash, id);
    }
}

public static class AnvilStorageServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilLocalStorage(this IServiceCollection services, Action<AnvilStorageOptions>? configure = null)
    {
        services.AddOptions<AnvilStorageOptions>(); if (configure is not null) services.Configure(configure);
        return services.AddSingleton<IAnvilStorage, LocalAnvilStorage>();
    }
}
