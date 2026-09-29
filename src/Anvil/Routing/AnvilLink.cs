using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Anvil;

public static partial class AnvilLink
{
    public static string Build(
        string routeTemplate,
        object? routeValues = null,
        object? queryValues = null,
        string? fragment = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(routeTemplate);

        var values = ReadValues(routeValues);
        var builder = new StringBuilder();
        var position = 0;
        foreach (var match in RouteParameter().Matches(routeTemplate).Cast<Match>())
        {
            builder.Append(routeTemplate, position, match.Index - position);
            var name = match.Groups["name"].Value;
            if (!values.Remove(name, out var value) || value is null)
            {
                throw new InvalidOperationException($"Missing route value: {name}");
            }

            var isCatchAll = match.Groups["catchAll"].Success;
            builder.Append(isCatchAll
                ? EncodeCatchAll(value.ToString() ?? string.Empty)
                : Uri.EscapeDataString(value.ToString() ?? string.Empty));
            position = match.Index + match.Length;
        }

        builder.Append(routeTemplate, position, routeTemplate.Length - position);
        var path = builder.ToString();
        var query = ReadValues(queryValues);
        if (query.Count > 0)
        {
            path = QueryHelpers.AddQueryString(path, query);
        }

        if (!string.IsNullOrEmpty(fragment))
        {
            path += $"#{Uri.EscapeDataString(fragment)}";
        }

        return path;
    }

    public static bool IsCurrent(HttpContext context, string path)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var current = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
        var target = new Uri(new Uri("http://anvil.local"), path).AbsolutePath.TrimEnd('/');
        return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
    }

    private static string EncodeCatchAll(string value)
    {
        return string.Join('/', value.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
    }

    private static Dictionary<string, string?> ReadValues(object? source)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return values;
        }

        if (source is IEnumerable<KeyValuePair<string, string?>> stringPairs)
        {
            foreach (var pair in stringPairs)
            {
                values[pair.Key] = pair.Value;
            }

            return values;
        }

        if (source is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                values[entry.Key.ToString()!] = entry.Value?.ToString();
            }

            return values;
        }

        foreach (var property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length == 0)
            {
                values[property.Name] = property.GetValue(source)?.ToString();
            }
        }

        return values;
    }

    [GeneratedRegex(@"\{(?<catchAll>\*)?(?<name>[A-Za-z_][A-Za-z0-9_]*)(?::[^}]+)?\}")]
    private static partial Regex RouteParameter();
}
