using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using System.Net;

namespace Anvil;

public sealed class RequestContext
{
    private HttpContext? httpContext;

    public HttpContext HttpContext =>
        httpContext ?? throw new InvalidOperationException("The Anvil request context is not available.");

    public HttpRequest Request => HttpContext.Request;

    public HttpResponse Response => HttpContext.Response;

    public RouteValueDictionary RouteValues => Request.RouteValues;

    public IPAddress? ClientIp => HttpContext.Connection.RemoteIpAddress;

    public CancellationToken RequestAborted => HttpContext.RequestAborted;

    public string? LastEventId => Request.Headers["Last-Event-ID"].FirstOrDefault();

    public Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = default)
    {
        return Request.ReadFormAsync(cancellationToken == default ? RequestAborted : cancellationToken);
    }

    public async Task<IReadOnlyList<AnvilMultipartFile>> ReadMultipartFilesAsync(
        CancellationToken cancellationToken = default,
        long maxBytes = 10 * 1024 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        if (Request.ContentLength > maxBytes)
            throw new BadHttpRequestException("The multipart request exceeds the configured size limit.");
        var form = await Request.ReadFormAsync(new FormOptions
        {
            MultipartBodyLengthLimit = maxBytes
        }, cancellationToken == default ? RequestAborted : cancellationToken);
        return form.Files
            .Select(file => new AnvilMultipartFile(
                file.Name,
                file.FileName,
                file.ContentType,
                file.Length,
                file))
            .ToArray();
    }

    public string? ReadFormValue(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return Request.HasFormContentType ? Request.Form[name].FirstOrDefault() : null;
    }

    public string ReadRequiredFormValue(string name)
    {
        return ReadFormValue(name)
            ?? throw new BadHttpRequestException($"Missing form field: {name}");
    }

    public ValueTask<T?> ReadJsonAsync<T>(
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Request.ReadFromJsonAsync<T>(options, cancellationToken == default ? RequestAborted : cancellationToken);
    }

    public Task WriteJsonAsync<T>(
        T value,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Response.WriteAsJsonAsync(value, options, cancellationToken == default ? RequestAborted : cancellationToken);
    }

    public async ValueTask<byte[]> ReadBodyAsync(long maximumBytes = 2 * 1024 * 1024)
    {
        if (Request.ContentLength > maximumBytes)
            throw new BadHttpRequestException("The request body exceeds the configured limit.");
        await using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, RequestAborted);
        if (buffer.Length > maximumBytes)
            throw new BadHttpRequestException("The request body exceeds the configured limit.");
        return buffer.ToArray();
    }

    internal void Initialize(HttpContext context) => httpContext = context;
}
