using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Anvil;

public sealed class InMemoryAnvilPasswordResetService(TimeProvider? timeProvider = null) : IAnvilPasswordResetService
{
    private readonly ConcurrentDictionary<string, AnvilPasswordResetToken> tokens = new(StringComparer.Ordinal);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public ValueTask<AnvilPasswordResetToken> IssueAsync(
        string userId,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        if (lifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lifetime));

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var reset = new AnvilPasswordResetToken(userId, token, clock.GetUtcNow().Add(lifetime));
        tokens[token] = reset;
        return ValueTask.FromResult(reset);
    }

    public ValueTask<string?> ConsumeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token) || !tokens.TryRemove(token, out var reset))
            return ValueTask.FromResult<string?>(null);

        return ValueTask.FromResult<string?>(reset.ExpiresAt > clock.GetUtcNow() ? reset.UserId : null);
    }
}
