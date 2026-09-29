using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public sealed record AnvilApiDescription(
    string Method,
    string Pattern,
    string? Name = null,
    string? Summary = null,
    Type? RequestType = null,
    Type? ResponseType = null,
    bool RequiresAuthentication = false,
    string? ResponseContentType = null,
    string? Feature = null,
    string? Permission = null,
    string? Version = null,
    IReadOnlyList<string>? Tags = null,
    bool Deprecated = false,
    bool Idempotent = false,
    string? RateLimitPolicy = null);

public sealed record AnvilEndpointManifestEntry(
    string Id,
    string? Feature,
    string? Name,
    string Method,
    string Route,
    string? Request,
    string? Response,
    bool RequiresAuthentication,
    string? Permission,
    string? Version,
    IReadOnlyList<string> Tags,
    bool Deprecated,
    bool Idempotent,
    string? RateLimitPolicy);

/// <summary>
/// The explicit, deterministic contract source for an Anvil application.
/// Applications can build this from generated code or register entries while
/// mapping endpoints; consumers should use this instead of discovering routes
/// by scanning assemblies.
/// </summary>
public sealed class AnvilEndpointManifest
{
    private readonly List<AnvilEndpointManifestEntry> entries = [];

    public IReadOnlyList<AnvilEndpointManifestEntry> Entries => entries;

    public void Add(AnvilEndpointManifestEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.Id))
            throw new ArgumentException("An endpoint ID is required.", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.Method) || string.IsNullOrWhiteSpace(entry.Route))
            throw new ArgumentException("Endpoint method and route are required.", nameof(entry));
        entries.Add(entry with
        {
            Method = entry.Method.ToUpperInvariant(),
            Tags = entry.Tags.Order(StringComparer.Ordinal).ToArray()
        });
    }

    public IReadOnlyList<AnvilEndpointManifestEntry> Build()
    {
        var duplicate = entries.GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate endpoint ID '{duplicate.Key}'.");

        return entries.OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .ThenBy(entry => entry.Method, StringComparer.Ordinal)
            .ThenBy(entry => entry.Route, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class AnvilApiRegistry
{
    private readonly List<AnvilApiDescription> descriptions = [];
    private readonly AnvilEndpointManifest manifest = new();
    public IReadOnlyList<AnvilApiDescription> Descriptions => descriptions;
    public AnvilEndpointManifest Contract => manifest;
    public void Add(AnvilApiDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        descriptions.Add(description);
        manifest.Add(ToManifestEntry(description));
    }

    public IReadOnlyList<AnvilEndpointManifestEntry> BuildManifest() => manifest.Build();

    private static AnvilEndpointManifestEntry ToManifestEntry(AnvilApiDescription description) => new(
            description.Name ?? BuildId(description),
            description.Feature,
            description.Name,
            description.Method.ToUpperInvariant(),
            description.Pattern,
            description.RequestType?.AssemblyQualifiedName,
            description.ResponseType?.AssemblyQualifiedName,
            description.RequiresAuthentication,
            description.Permission,
            description.Version,
            description.Tags ?? [],
            description.Deprecated,
            description.Idempotent,
            description.RateLimitPolicy);

    public void Add<TRequest, TResponse>(string method, string pattern, string? name = null, bool requiresAuthentication = false)
        => Add(new AnvilApiDescription(method, pattern, name, null, typeof(TRequest), typeof(TResponse), requiresAuthentication));

    public object BuildDocument()
    {
        var manifest = BuildManifest();
        var schemas = descriptions
            .SelectMany(description => new[] { description.RequestType, description.ResponseType })
            .Where(type => type is not null)
            .Distinct()
            .ToDictionary(type => type!.Name, type => AnvilOpenApiExtensions.Schema(type!));

        var paths = descriptions
            .GroupBy(description => description.Pattern)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(
                    description => description.Method.ToLowerInvariant(),
                    description => AnvilOpenApiExtensions.Operation(description)));

        return new
        {
            openapi = "3.0.3",
            info = new { title = "Anvil API", version = "1.0.0" },
            paths,
            components = new
            {
                schemas,
                securitySchemes = new Dictionary<string, object>
                {
                    ["cookieAuth"] = new { type = "apiKey", @in = "cookie", name = "anvil.session" }
                }
            },
            xAnvilManifest = manifest
        };
    }

    private static string BuildId(AnvilApiDescription description) =>
        $"{description.Method.ToLowerInvariant()}:{description.Pattern}";
}

public static class AnvilOpenApiExtensions
{
    internal static void RegisterEndpoint(
        this IEndpointRouteBuilder endpoints,
        string method,
        string pattern,
        string? name = null,
        Type? requestType = null,
        Type? responseType = null,
        bool requiresAuthentication = false,
        string? responseContentType = null,
        string? feature = null,
        string? permission = null,
        string? version = null,
        IReadOnlyList<string>? tags = null,
        bool deprecated = false,
        bool idempotent = false,
        string? rateLimitPolicy = null)
    {
        endpoints.ServiceProvider.GetService<AnvilApiRegistry>()?.Add(
            new AnvilApiDescription(method, pattern, name, null, requestType, responseType, requiresAuthentication, responseContentType, feature, permission, version, tags, deprecated, idempotent, rateLimitPolicy));
    }

    public static IServiceCollection AddAnvilOpenApi(this IServiceCollection services)
    {
        services.AddSingleton<AnvilApiRegistry>();
        return services;
    }

    public static RouteHandlerBuilder MapAnvilOpenApi(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/openapi.json")
    {
        return endpoints.MapGet(pattern, (AnvilApiRegistry registry) =>
            Results.Json(registry.BuildDocument()));
    }

    public static RouteHandlerBuilder MapAnvilManifest(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/anvil.contract.json")
    {
        return endpoints.MapGet(pattern, (AnvilApiRegistry registry) =>
            Results.Json(registry.BuildManifest()));
    }

    internal static object Operation(AnvilApiDescription description)
    {
        var operation = new Dictionary<string, object?>
        {
            ["responses"] = new Dictionary<string, object>
            {
                ["200"] = new
                {
                    description = "Successful response",
                    content = description.ResponseType is null && description.ResponseContentType is null
                        ? null
                        : new Dictionary<string, object>
                    {
                        [description.ResponseContentType ?? "application/json"] = new
                        {
                            schema = description.ResponseType is null
                                ? new { type = "string" }
                                : Reference(description.ResponseType)
                        }
                    }
                }
            }
        };

        if (description.Summary is not null) operation["summary"] = description.Summary;
        if (description.Name is not null) operation["operationId"] = description.Name;
        if (description.Tags is { Count: > 0 }) operation["tags"] = description.Tags;
        if (description.Deprecated) operation["deprecated"] = true;
        if (description.RequestType is not null)
        {
            operation["requestBody"] = new
            {
                required = true,
                content = new Dictionary<string, object>
                {
                    ["application/json"] = new { schema = Reference(description.RequestType) }
                }
            };
        }

        if (description.RequiresAuthentication)
            operation["security"] = new[] { new Dictionary<string, string[]> { ["cookieAuth"] = [] } };

        if (description.Permission is not null)
            operation["x-anvil-permission"] = description.Permission;
        if (description.Version is not null)
            operation["x-anvil-version"] = description.Version;
        if (description.Idempotent)
            operation["x-anvil-idempotent"] = true;
        if (description.RateLimitPolicy is not null)
            operation["x-anvil-rate-limit"] = description.RateLimitPolicy;

        return operation;
    }

    internal static object Schema(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsEnum) return new { type = "string", @enum = Enum.GetNames(type) };
        if (type == typeof(string) || type == typeof(Guid)) return new { type = "string" };
        if (type == typeof(bool)) return new { type = "boolean" };
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return new { type = "string", format = "date-time" };
        if (type == typeof(int) || type == typeof(long)) return new { type = "integer", format = type == typeof(int) ? "int32" : "int64" };
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return new { type = "number" };
        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            return new { type = "array", items = Schema(type.GetElementType() ?? type.GetGenericArguments()[0]) };

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name), property => Schema(property.PropertyType));
        return new { type = "object", properties };
    }

    private static object Reference(Type type) => new { @ref = $"#/components/schemas/{type.Name}" };
}
