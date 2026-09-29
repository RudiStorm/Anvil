using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Anvil;

public sealed class AnvilSessionManager(
    RequestContext requestContext,
    IAnvilSessionStore store,
    IOptions<AnvilSessionOptions> options,
    TimeProvider timeProvider)
{
    public async ValueTask<AnvilSessionEntry?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var token = requestContext.GetCookie(options.Value.CookieName);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = HashToken(token);
        var entry = await store.GetAsync(tokenHash, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (entry.ExpiresAt <= now || entry.CreatedAt + options.Value.AbsoluteTimeout <= now)
        {
            await store.RemoveAsync(tokenHash, cancellationToken);
            requestContext.DeleteCookie(options.Value.CookieName);
            return null;
        }

        var refreshed = entry with
        {
            LastAccessedAt = now,
            ExpiresAt = now + options.Value.IdleTimeout
        };
        await store.SetAsync(refreshed, cancellationToken);
        requestContext.SetCookie(
            options.Value.CookieName,
            token,
            CreateSessionCookieOptions(options.Value.IdleTimeout));
        return refreshed;
    }

    public async ValueTask<AnvilSessionEntry> StartAsync(
        IReadOnlyDictionary<string, string>? values = null,
        CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var token = CreateToken();
        var entry = new AnvilSessionEntry(
            HashToken(token),
            now,
            now,
            now + options.Value.IdleTimeout,
            values ?? new Dictionary<string, string>(StringComparer.Ordinal));

        await store.SetAsync(entry, cancellationToken);
        requestContext.SetCookie(
            options.Value.CookieName,
            token,
            CreateSessionCookieOptions(options.Value.IdleTimeout));
        return entry;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        var token = requestContext.GetCookie(options.Value.CookieName);
        if (!string.IsNullOrWhiteSpace(token))
        {
            await store.RemoveAsync(HashToken(token), cancellationToken);
        }

        requestContext.DeleteCookie(options.Value.CookieName);
    }

    public async ValueTask<AnvilSessionEntry?> RotateAsync(
        CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(cancellationToken);
        if (current is null)
        {
            return null;
        }

        return await StartAsync(current.Values, cancellationToken);
    }

    private static string CreateToken() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static Microsoft.AspNetCore.Http.CookieOptions CreateSessionCookieOptions(TimeSpan lifetime)
    {
        var options = AnvilCookieExtensions.CreateDefaultOptions();
        options.Expires = DateTimeOffset.UtcNow + lifetime;
        return options;
    }
}
