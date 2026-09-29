using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Anvil;

public static class AnvilTotp
{
    public static string CreateSecret(int bytes = 20) =>
        Base32Encode(RandomNumberGenerator.GetBytes(bytes));

    public static string CreateProvisioningUri(string issuer, string account, string secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";

    public static string ComputeCode(string base32Secret, long unixTimeSeconds)
    {
        var key = Base32Decode(base32Secret);
        var counter = BitConverter.GetBytes(unixTimeSeconds / 30);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 15;
        var value = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16) |
                    (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Base32Encode(byte[] bytes)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new System.Text.StringBuilder((bytes.Length * 8 + 4) / 5);
        for (var bit = 0; bit < bytes.Length * 8; bit += 5)
        {
            var value = 0;
            for (var offset = 0; offset < 5; offset++)
                value = (value << 1) | (bit + offset < bytes.Length * 8 ? (bytes[(bit + offset) / 8] >> (7 - ((bit + offset) % 8)) & 1) : 0);
            result.Append(alphabet[value]);
        }
        return result.ToString();
    }

    private static byte[] Base32Decode(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = new List<byte>();
        foreach (var character in value.TrimEnd('=').ToUpperInvariant())
        {
            var index = alphabet.IndexOf(character);
            if (index < 0) throw new FormatException("The TOTP secret is not valid Base32.");
            for (var bit = 4; bit >= 0; bit--) bits.Add((byte)((index >> bit) & 1));
        }
        var result = new byte[bits.Count / 8];
        for (var i = 0; i < result.Length; i++)
            for (var bit = 0; bit < 8; bit++) result[i] = (byte)((result[i] << 1) | bits[i * 8 + bit]);
        return result;
    }
}

public sealed class InMemoryAnvilDeviceSessionStore : IAnvilDeviceSessionStore
{
    private readonly ConcurrentDictionary<string, AnvilDeviceSession> sessions = new(StringComparer.Ordinal);

    public ValueTask AddAsync(AnvilDeviceSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        sessions[session.Id] = session;
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<AnvilDeviceSession>> ListAsync(string userId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<AnvilDeviceSession>>(sessions.Values.Where(x => x.UserId == userId).OrderByDescending(x => x.LastAccessedAt).ToArray());

    public ValueTask RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        if (sessions.TryGetValue(sessionId, out var session) && session.UserId == userId) sessions.TryRemove(sessionId, out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask RevokeAllAsync(string userId, string? exceptSessionId = null, CancellationToken cancellationToken = default)
    {
        foreach (var session in sessions.Values.Where(x => x.UserId == userId && x.Id != exceptSessionId)) sessions.TryRemove(session.Id, out _);
        return ValueTask.CompletedTask;
    }
}
