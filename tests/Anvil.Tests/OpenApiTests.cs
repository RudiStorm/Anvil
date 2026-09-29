using Anvil;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Anvil.Tests;

public sealed class OpenApiTests
{
    [Fact]
    public void Registry_builds_openapi_document_with_operations_and_schemas()
    {
        var registry = new AnvilApiRegistry();
        registry.Add<CreateRequest, ProductResponse>("POST", "/products", "createProduct", requiresAuthentication: true);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(registry.BuildDocument()));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/products").GetProperty("post");

        Assert.Equal("createProduct", operation.GetProperty("operationId").GetString());
        Assert.Equal("#/components/schemas/CreateRequest", operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("ref").GetString());
        Assert.Equal("cookie", document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("cookieAuth").GetProperty("in").GetString());
        Assert.True(operation.GetProperty("security")[0].GetProperty("cookieAuth").ValueKind == JsonValueKind.Array);
        Assert.True(document.RootElement.GetProperty("components").GetProperty("schemas").TryGetProperty("ProductResponse", out _));
    }

    [Fact]
    public void Registry_builds_a_deterministic_endpoint_manifest()
    {
        var registry = new AnvilApiRegistry();
        registry.Add(new AnvilApiDescription("POST", "/api/customers", "customers.create", RequestType: typeof(CreateRequest), Feature: "Customers", Permission: "customers.create", Tags: ["Customers"], Idempotent: false));

        var entry = Assert.Single(registry.BuildManifest());

        Assert.Equal("customers.create", entry.Id);
        Assert.Equal("Customers", entry.Feature);
        Assert.Equal("POST", entry.Method);
        Assert.Equal("customers.create", entry.Permission);
        Assert.Contains("CreateRequest", entry.Request, StringComparison.Ordinal);
        Assert.Equal("Customers", Assert.Single(entry.Tags));
    }

    [Fact]
    public void Registry_describes_html_fragment_responses()
    {
        var registry = new AnvilApiRegistry();
        registry.Add(new AnvilApiDescription(
            "GET",
            "/products/rows",
            ResponseContentType: "text/html"));

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(registry.BuildDocument()));
        var response = document.RootElement
            .GetProperty("paths")
            .GetProperty("/products/rows")
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200");

        Assert.Equal("string", response.GetProperty("content").GetProperty("text/html").GetProperty("schema").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Mutating_api_helpers_register_all_http_methods()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAnvil();
        builder.Services.AddAnvilOpenApi();
        var app = builder.Build();
        app.MapAnvilPut<CreateRequest>("/products/{id:int}", async (_, request) =>
        {
            await Task.CompletedTask;
            return Results.Ok(request);
        });
        app.MapAnvilPatch<CreateRequest>("/products/{id:int}", async (_, request) =>
        {
            await Task.CompletedTask;
            return Results.Ok(request);
        });
        app.MapAnvilDelete("/products/{id:int}", async _ =>
        {
            await Task.CompletedTask;
            return Results.NoContent();
        });

        await app.StartAsync();
        var registry = app.Services.GetRequiredService<AnvilApiRegistry>();
        var document = JsonSerializer.SerializeToDocument(registry.BuildDocument());
        var operations = document.RootElement.GetProperty("paths").GetProperty("/products/{id:int}");

        Assert.True(operations.TryGetProperty("put", out _));
        Assert.True(operations.TryGetProperty("patch", out _));
        Assert.True(operations.TryGetProperty("delete", out _));

        await app.StopAsync();
        await app.DisposeAsync();
    }

    private sealed record CreateRequest(string Name);
    private sealed record ProductResponse(int Id, string Name);
}
