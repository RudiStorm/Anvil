namespace Anvil;

public sealed record AnvilSessionEntry(
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastAccessedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> Values);
