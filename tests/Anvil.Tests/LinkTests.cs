using Anvil;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class LinkTests
{
    [Fact]
    public void Link_builder_encodes_route_and_query_values()
    {
        var link = AnvilLink.Build(
            "/products/{id:int}",
            new { id = "coffee beans" },
            new { search = "dark roast" },
            "details & reviews");

        Assert.Equal("/products/coffee%20beans?search=dark%20roast#details%20%26%20reviews", link);
    }

    [Fact]
    public void Link_builder_supports_catch_all_values()
    {
        var link = AnvilLink.Build("/files/{*path}", new { path = "docs/read me.txt" });

        Assert.Equal("/files/docs/read%20me.txt", link);
    }

    [Fact]
    public void Current_route_check_ignores_trailing_slashes_and_query_values()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/products/1/";
        context.Request.QueryString = new QueryString("?tab=details");

        Assert.True(AnvilLink.IsCurrent(context, "/products/1?tab=reviews"));
        Assert.False(AnvilLink.IsCurrent(context, "/products/2"));
    }

    [Fact]
    public void Typed_route_definition_builds_a_link()
    {
        var route = new AnvilRoute<ProductValues>("/products/{id:int}");

        Assert.Equal("/products/42", route.Link(new ProductValues(42)));
    }

    [Fact]
    public async Task Absolute_links_use_the_configured_base_url()
    {
        var services = new ServiceCollection();
        services.AddAnvilRouting(options => options.BaseUrl = new Uri("https://example.test/app/"));
        services.AddScoped<RequestContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var requestContext = scope.ServiceProvider.GetRequiredService<RequestContext>();
        requestContext.Initialize(context);

        Assert.Equal("https://example.test/app/products", requestContext.AbsoluteLink("/products"));
    }

    [Fact]
    public void Form_action_is_the_same_encoded_url_as_a_link()
    {
        var route = new AnvilRoute<ProductValues>("/products/{id:int}");

        Assert.Equal(route.Link(new ProductValues(42)), route.FormAction(new ProductValues(42)));
    }

    [Fact]
    public void Route_attribute_keeps_explicit_route_metadata()
    {
        var attribute = new AnvilRouteAttribute("/health") { Name = "health" };

        Assert.Equal("/health", attribute.Template);
        Assert.Equal("health", attribute.Name);
    }

    private sealed record ProductValues(int Id);
}
