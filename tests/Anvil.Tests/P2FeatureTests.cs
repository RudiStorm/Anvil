using System.Net.WebSockets;
using System.Globalization;
using System.Text;
using Anvil;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anvil.Tests;

public sealed class P2FeatureTests
{
    [Fact]
    public void Sse_events_format_ids_events_and_multiline_data()
    {
        var formatted = new AnvilSseEvent("first\nsecond", "status", "42").Format();

        Assert.Equal("id: 42\nevent: status\ndata: first\ndata: second\n\n", formatted);
        Assert.Equal("data: \n\n", AnvilSseEvent.KeepAlive().Format());
    }

    [Fact]
    public async Task Static_directory_mount_serves_files_under_the_requested_path()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anvil-static", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "hello.txt"), "hello");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseAnvilStaticDirectory(directory, "/mounted");

        try
        {
            await app.StartAsync();
            var response = await app.GetTestClient().GetAsync("/mounted/hello.txt");

            Assert.Equal(200, (int)response.StatusCode);
            Assert.Equal("hello", await response.Content.ReadAsStringAsync());
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Websocket_connection_sends_and_receives_text_messages()
    {
        var socket = new FakeWebSocket("hello");
        var connection = new AnvilWebSocketConnection(socket, new AnvilWebSocketOptions());

        Assert.Equal("hello", await connection.ReceiveTextAsync());
        await connection.SendTextAsync("world");

        Assert.Equal("world", socket.SentText);
    }

    [Fact]
    public async Task Multipart_parser_enforces_limits_without_content_length()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("this is too large"), "value");
        var body = await content.ReadAsByteArrayAsync();
        var context = new DefaultHttpContext();
        context.Request.ContentType = content.Headers.ContentType!.ToString();
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = null;
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        context.RequestServices = provider;
        var requestContext = provider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);

        await Assert.ThrowsAnyAsync<Exception>(() => requestContext.ReadMultipartFilesAsync(maxBytes: 4));
    }

    [Fact]
    public async Task File_mail_transport_writes_attachment_content_as_mime()
    {
        var directory = Path.Combine(Path.GetTempPath(), "anvil-mail", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var transport = new FileAnvilMailTransport(Options.Create(new AnvilMailOptions { FileDirectory = directory }));
            await transport.SendAsync(new AnvilMailMessage("from@example.test", "to@example.test", "Subject", "<p>Hello</p>")
            {
                Attachments = [new AnvilMailAttachment("hello.txt", "text/plain", Encoding.UTF8.GetBytes("attachment"))]
            });

            var file = Directory.GetFiles(directory).Single();
            var message = await File.ReadAllTextAsync(file);
            Assert.Contains("multipart/mixed", message);
            Assert.Contains("Content-Disposition: attachment; filename*=UTF-8''hello.txt", message);
            Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes("attachment")), message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Sse_endpoint_passes_last_event_id_to_the_stream()
    {
        string? lastEventId = null;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAnvil();
        var app = builder.Build();
        app.UseAnvil();
        app.MapAnvilSse("/events", (RequestContext _, string? id) => Events(id));

        try
        {
            await app.StartAsync();
            var request = new HttpRequestMessage(HttpMethod.Get, "/events");
            request.Headers.Add("Last-Event-ID", "7");
            var response = await app.GetTestClient().SendAsync(request);

            Assert.Equal(200, (int)response.StatusCode);
            Assert.Contains("id: 8", await response.Content.ReadAsStringAsync());
            Assert.Equal("7", lastEventId);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }

        async IAsyncEnumerable<AnvilSseEvent> Events(string? id)
        {
            lastEventId = id;
            await Task.Yield();
            yield return new AnvilSseEvent("resumed", "status", "8");
        }
    }

    [Fact]
    public void Integration_headers_require_a_true_value()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        var requestContext = provider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);

        context.Request.Headers["HX-Request"] = "false";
        context.Request.Headers["X-Alpine-Request"] = "1";
        Assert.False(requestContext.IsHtmxRequest());
        Assert.False(requestContext.IsAlpineAjaxRequest());
        context.Request.Headers["HX-Request"] = "true";
        context.Request.Headers["X-Alpine-Request"] = "true";
        Assert.True(requestContext.IsHtmxRequest());
        Assert.True(requestContext.IsAlpineAjaxRequest());
    }

    [Fact]
    public async Task Datastar_patch_helpers_preserve_multiline_data()
    {
        var services = new ServiceCollection();
        services.AddAnvil();
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();
        var requestContext = provider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);

        await requestContext.WriteDatastarPatchElementsAsync("#target", "<p>one\ntwo</p>");

        context.Response.Body.Position = 0;
        var output = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("data: elements <p>one\n", output);
        Assert.Contains("data: elements two</p>\n", output);
    }

    [Fact]
    public void Sse_metadata_rejects_line_injection()
    {
        Assert.Throws<ArgumentException>(() => new AnvilSseEvent("data", "status\r\nretry: 0").Format());
    }

    [Fact]
    public async Task Sitemap_priority_uses_invariant_decimal_formatting()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAnvil();
        var app = builder.Build();
        app.UseAnvil();
        app.MapAnvilSitemap("/sitemap.xml", _ => Task.FromResult<IReadOnlyList<AnvilSitemapEntry>>(
            [new AnvilSitemapEntry("https://example.test", Priority: 0.5m)]));

        try
        {
            await app.StartAsync();
            var xml = await app.GetTestClient().GetStringAsync("/sitemap.xml");
            Assert.Contains("<priority>0.5</priority>", xml);
            Assert.DoesNotContain("<priority>0,5</priority>", xml);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private sealed class FakeWebSocket(string incoming) : WebSocket
    {
        private readonly byte[] incomingBytes = Encoding.UTF8.GetBytes(incoming);

        public string? SentText { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;

        public override void Abort() { }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override void Dispose() { }

        public override Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken)
        {
            incomingBytes.CopyTo(buffer.Array!, buffer.Offset);
            return Task.FromResult(new WebSocketReceiveResult(
                incomingBytes.Length,
                WebSocketMessageType.Text,
                endOfMessage: true));
        }

        public override Task SendAsync(
            ArraySegment<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken)
        {
            SentText = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
            return Task.CompletedTask;
        }
    }
}
