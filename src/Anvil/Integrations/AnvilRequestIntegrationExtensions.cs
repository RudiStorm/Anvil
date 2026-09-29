using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Anvil;

public static class AnvilRequestIntegrationExtensions
{
    public static bool IsHtmxRequest(this RequestContext context) =>
        HeaderIsTrue(context, "HX-Request");

    public static string? HtmxTarget(this RequestContext context) =>
        context.Request.Headers["HX-Target"].FirstOrDefault();

    public static void SetHtmxTrigger(this RequestContext context, string name)
    {
        context.Response.Headers["HX-Trigger"] = name;
    }

    public static void SetHtmxRedirect(this RequestContext context, string location)
    {
        context.Response.Headers["HX-Redirect"] = location;
    }

    public static void SetHtmxReswap(this RequestContext context, string strategy)
    {
        context.Response.Headers["HX-Reswap"] = strategy;
    }

    public static bool IsAlpineAjaxRequest(this RequestContext context) =>
        HeaderIsTrue(context, "X-Alpine-Request");

    public static void SetAlpineRedirect(this RequestContext context, string location)
    {
        context.Response.Headers["X-Alpine-Redirect"] = location;
    }

    public static void SetAlpineTarget(this RequestContext context, string target)
    {
        context.Response.Headers["X-Alpine-Target"] = target;
    }

    public static IReadOnlyDictionary<string, JsonElement>? ReadDatastarSignals(
        this RequestContext context)
    {
        var value = context.Request.Headers["X-Datastar-Signals"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static Task WriteDatastarPatchElementsAsync(
        this RequestContext context,
        string selector,
        string html)
    {
        return WriteDatastarEventAsync(
            context,
            "datastar-patch-elements",
            ("selector", selector),
            ("elements", html));
    }

    public static Task WriteDatastarPatchSignalsAsync(
        this RequestContext context,
        string json)
    {
        return WriteDatastarEventAsync(context, "datastar-patch-signals", ("signals", json));
    }

    private static bool HeaderIsTrue(RequestContext context, string name) =>
        string.Equals(context.Request.Headers[name].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);

    private static async Task WriteDatastarEventAsync(
        RequestContext context,
        string eventName,
        params (string Name, string Value)[] fields)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.WriteAsync($"event: {eventName}\n", context.RequestAborted);
        foreach (var field in fields)
        {
            var value = field.Value.Replace("\r\n", "\n").Replace('\r', '\n');
            foreach (var line in value.Split('\n'))
                await context.Response.WriteAsync($"data: {field.Name} {line}\n", context.RequestAborted);
        }

        await context.Response.WriteAsync("\n", context.RequestAborted);
        await context.FlushAsync();
    }
}
