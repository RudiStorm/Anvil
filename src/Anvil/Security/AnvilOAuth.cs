using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Anvil;

public sealed class AnvilOAuthOptions
{
    public required string ProviderName { get; init; }
    public required string AuthorizationEndpoint { get; init; }
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public string? Scope { get; init; }
}

public sealed record AnvilOAuthState(string Value, string ReturnUrl, DateTimeOffset ExpiresAt);

public sealed record AnvilExternalIdentity(string Subject, string? Email, string? DisplayName);

public sealed class AnvilOAuthService(RequestContext requestContext)
{
    private const string StateCookie = "anvil.oauth.state";

    public string Begin(AnvilOAuthOptions options, string returnUrl = "/")
    {
        if (!Uri.TryCreate(returnUrl, UriKind.Relative, out _))
            throw new ArgumentException("OAuth return URLs must be relative.", nameof(returnUrl));

        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        requestContext.SetEncryptedCookie(StateCookie, System.Text.Json.JsonSerializer.Serialize(
            new AnvilOAuthState(state, returnUrl, DateTimeOffset.UtcNow.AddMinutes(10))));
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = options.Scope,
            ["state"] = state
        };
        return QueryHelpers.AddQueryString(options.AuthorizationEndpoint, query);
    }

    public string? ValidateCallback(string state)
    {
        var serialized = requestContext.GetEncryptedCookie(StateCookie);
        requestContext.DeleteCookie(StateCookie);
        if (serialized is null) return null;
        var expected = System.Text.Json.JsonSerializer.Deserialize<AnvilOAuthState>(serialized);
        return expected is not null
               && expected.ExpiresAt > DateTimeOffset.UtcNow
               && CryptographicOperations.FixedTimeEquals(
                   System.Text.Encoding.UTF8.GetBytes(expected.Value),
                   System.Text.Encoding.UTF8.GetBytes(state))
            ? expected.ReturnUrl
            : null;
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
