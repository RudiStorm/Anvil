using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.Json;

namespace Anvil;

public static class AnvilCookieExtensions
{
    public static string? GetCookie(this RequestContext context, string name)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Request.Cookies[CookieName(context, name)];
    }

    public static void SetCookie(
        this RequestContext context,
        string name,
        string value,
        CookieOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(name);
        EnsureWritable(context);

        var cookieOptions = options ?? CreateDefaultOptions();
        cookieOptions.Secure |= context.Request.IsHttps;
        context.Response.Cookies.Append(CookieName(context, name), value, cookieOptions);
    }

    public static void DeleteCookie(
        this RequestContext context,
        string name,
        CookieOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(name);
        EnsureWritable(context);

        var cookieOptions = options ?? CreateDefaultOptions();
        cookieOptions.Secure |= context.Request.IsHttps;
        context.Response.Cookies.Delete(CookieName(context, name), cookieOptions);
    }

    public static void SetSignedCookie(
        this RequestContext context,
        string name,
        string value,
        CookieOptions? options = null)
    {
        context.SetCookie(name, CreateProtector(context, name, "signed").Protect(value), options);
    }

    public static string? GetSignedCookie(this RequestContext context, string name)
    {
        return UnprotectCookie(context, name, "signed");
    }

    public static void SetEncryptedCookie(
        this RequestContext context,
        string name,
        string value,
        CookieOptions? options = null)
    {
        context.SetCookie(name, CreateProtector(context, name, "encrypted").Protect(value), options);
    }

    public static string? GetEncryptedCookie(this RequestContext context, string name)
    {
        return UnprotectCookie(context, name, "encrypted");
    }

    public static void SetJsonCookie<T>(
        this RequestContext context,
        string name,
        T value,
        CookieOptions? options = null)
    {
        context.SetEncryptedCookie(name, JsonSerializer.Serialize(value), options);
    }

    public static T? GetJsonCookie<T>(this RequestContext context, string name)
    {
        var value = context.GetEncryptedCookie(name);
        if (value is null)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(value);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static CookieOptions CreateDefaultOptions() => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = false,
        IsEssential = true,
        Path = "/",
    };

    private static IDataProtector CreateProtector(
        RequestContext context,
        string name,
        string purpose)
    {
        var provider = context.HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>();
        return provider.CreateProtector("Anvil", "Cookie", purpose, CookieName(context, name));
    }

    private static string? UnprotectCookie(
        RequestContext context,
        string name,
        string purpose)
    {
        var value = context.GetCookie(name);
        if (value is null)
        {
            return null;
        }

        try
        {
            return CreateProtector(context, name, purpose).Unprotect(value);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static string CookieName(RequestContext context, string name)
    {
        var services = context.HttpContext.RequestServices;
        var prefix = services is null
            ? string.Empty
            : services.GetService<IOptions<AnvilCookieOptions>>()?.Value.NamePrefix ?? string.Empty;
        return prefix + name;
    }

    private static void EnsureWritable(RequestContext context)
    {
        if (context.Response.HasStarted)
        {
            throw new InvalidOperationException("Cookies cannot be changed after the response has started.");
        }
    }
}
