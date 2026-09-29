using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Anvil.Tests;

public sealed class SampleApplicationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    private readonly WebApplicationFactory<Program> factory;

    public SampleApplicationTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
            builder.UseContentRoot(Path.Combine(FindRepositoryRoot(), "samples", "Anvil.Sample")));
        client = this.factory.CreateClient();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Anvil.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the Anvil solution root.");
    }

    [Fact]
    public async Task Dashboard_home_page_renders_server_side()
    {
        var response = await client.GetAsync("/overview");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Good morning, Jordan", body);
        Assert.Contains("Net revenue", body);
        Assert.Contains("server-rendered", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Route_parameter_renders_the_requested_product()
    {
        var response = await client.GetAsync("/products/1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Espresso", body);
    }

    [Fact]
    public async Task Query_parameter_filters_products()
    {
        var response = await client.GetAsync("/products?search=espresso");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Espresso", body);
        Assert.DoesNotContain("Pour-over", body);
    }

    [Fact]
    public async Task Razor_fragment_endpoint_renders_inventory_rows()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/inventory/table?search=espresso");
        request.Headers.Add("HX-Request", "true");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Espresso", body);
        Assert.DoesNotContain("Pour-over", body);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Direct_fragment_navigation_returns_the_full_products_page()
    {
        var response = await client.GetAsync("/api/inventory/table?search=house");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Inventory", body);
        Assert.Contains("name=\"search\"", body);
    }

    [Fact]
    public async Task Razor_fragment_post_endpoint_renders_form_values()
    {
        var antiforgery = factory.Services.GetRequiredService<IAntiforgery>();
        var tokenContext = new DefaultHttpContext
        {
            RequestServices = factory.Services
        };
        var tokens = antiforgery.GetAndStoreTokens(tokenContext);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/table")
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("search", "cold")])
        };
        request.Headers.Add("RequestVerificationToken", tokens.RequestToken!);
        request.Headers.Add("Cookie", tokenContext.Response.Headers.SetCookie.ToString().Split(';')[0]);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cold brew", body);
        Assert.DoesNotContain("Espresso", body);
    }

    [Fact]
    public async Task Inventory_search_uses_htmx_and_fragment_endpoint()
    {
        var page = await client.GetStringAsync("/products");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/inventory/table?search=espresso");
        request.Headers.Add("HX-Request", "true");
        using var fragmentResponse = await client.SendAsync(request);
        var fragment = await fragmentResponse.Content.ReadAsStringAsync();

        Assert.Contains("hx-get=\"/api/inventory/table\"", page);
        Assert.Contains("hx-target=\"#inventory-results\"", page);
        Assert.Contains("data-anvil-partial-form", page);
        Assert.Contains("Espresso", fragment);
        Assert.DoesNotContain("Pour-over", fragment);
        Assert.DoesNotContain("<html", fragment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sample_mutation_controls_are_wired_to_server_endpoints()
    {
        var products = await client.GetStringAsync("/products");
        var settings = await client.GetStringAsync("/settings");

        Assert.Contains("hx-post=\"/api/products\"", products);
        Assert.Contains("id=\"add-product-modal\"", products);
        Assert.Contains("data-anvil-toggle=\"#add-product-modal\"", products);
        Assert.Contains("hx-delete=\"/api/products/", products);
        Assert.Contains("hx-post=\"/api/settings\"", settings);
        Assert.Contains("id=\"settings-status\"", settings);
    }

    [Fact]
    public async Task Integrations_page_exposes_session_upload_and_mail_examples()
    {
        var page = await client.GetStringAsync("/integrations");

        Assert.Contains("/api/account/sign-in", page);
        Assert.Contains("/api/account/sign-out", page);
        Assert.Contains("/api/uploads", page);
        Assert.Contains("/api/contact", page);
        Assert.Contains("multipart/form-data", page);
    }

    [Fact]
    public async Task Admin_pages_render_team_and_settings_content()
    {
        var team = await client.GetStringAsync("/team");
        var settings = await client.GetStringAsync("/settings");

        Assert.Contains("Jordan Davis", team);
        Assert.Contains("Workspace details", settings);
    }

    [Fact]
    public async Task Identity_and_operations_reference_pages_render()
    {
        var identity = await client.GetStringAsync("/identity");
        var operations = await client.GetStringAsync("/operations");
        var readiness = await client.GetAsync("/health/ready");

        Assert.Contains("Identity lab", identity);
        Assert.Contains("MFA setup", identity);
        Assert.Contains("Operations", operations);
        Assert.Contains("Durable work", operations);
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
    }

    [Fact]
    public async Task Documentation_page_contains_the_core_learning_path()
    {
        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Getting started", body);
        Assert.Contains("Create an API endpoint", body);
        Assert.Contains("anvil make endpoint Health", body);
        Assert.Contains("anvil make:page Reports", body);
        Assert.Contains("anvil make:shard Revenue", body);
        Assert.Contains("Fragments and shards", body);
        Assert.Contains("Authentication and authorization", body);
        Assert.Contains("Production checklist", body);
        Assert.Contains("docs.css", body);
        Assert.True(body.IndexOf("href=\"#getting-started\"", StringComparison.Ordinal) < body.IndexOf("href=\"#scaffolding\"", StringComparison.Ordinal));
        Assert.True(body.IndexOf("href=\"#scaffolding\"", StringComparison.Ordinal) < body.IndexOf("href=\"#mental-model\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Framework_lab_exposes_the_showcase_components()
    {
        var response = await client.GetAsync("/framework");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Framework lab", body);
        Assert.Contains("data-anvil-signal=\"lab-query\"", body);
        Assert.Contains("data-anvil-shard=\"/api/showcase/shard\"", body);
        Assert.Contains("sample-framework-state", body);
        Assert.Contains("Server rendered.", body);
    }

    [Fact]
    public async Task Openapi_and_sitemap_are_mapped_in_the_sample()
    {
        var openApi = await client.GetStringAsync("/openapi.json");
        var sitemap = await client.GetStringAsync("/sitemap.xml");

        Assert.Contains("/api/health", openApi);
        Assert.Contains("/api/inventory/table", openApi);
        Assert.Contains("text/html", openApi);
        Assert.Contains("https://example.test/framework", sitemap);
    }

    [Fact]
    public async Task Showcase_shard_returns_only_fragment_html()
    {
        using var response = await client.PostAsJsonAsync("/api/showcase/shard", new { });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Server shard rendered", body);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dashboard_activity_is_a_replaceable_html_fragment()
    {
        var page = await client.GetStringAsync("/overview");
        var fragment = await client.GetAsync("/api/dashboard/activity");
        var fragmentBody = await fragment.Content.ReadAsStringAsync();

        Assert.Contains("data-anvil-live=\"/api/dashboard/activity\"", page);
        Assert.Equal(HttpStatusCode.OK, fragment.StatusCode);
        Assert.Equal("text/html", fragment.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Maya Lewis placed order", fragmentBody);
        Assert.DoesNotContain("<html", fragmentBody, StringComparison.OrdinalIgnoreCase);

        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/dashboard/activity")
        {
            Content = JsonContent.Create(new { })
        };
        var refreshed = await client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal("text/html", refreshed.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Static_assets_are_served()
    {
        var response = await client.GetAsync("/app.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Missing_route_returns_not_found()
    {
        var response = await client.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_returns_json()
    {
        var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", body?.Status);
    }

    [Fact]
    public async Task Route_groups_and_catch_all_parameters_work()
    {
        var group = await client.GetAsync("/group/ping");
        var catchAll = await client.GetAsync("/files/docs/readme.txt");

        Assert.Equal(HttpStatusCode.OK, group.StatusCode);
        Assert.Equal(HttpStatusCode.OK, catchAll.StatusCode);
        Assert.Contains("docs/readme.txt", await catchAll.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Named_route_link_generates_an_internal_path()
    {
        var response = await client.GetAsync("/api/link");
        var body = await response.Content.ReadFromJsonAsync<LinkResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/api/health", body?.Url);
    }

    [Fact]
    public async Task Streaming_page_renders_the_delayed_content()
    {
        var response = await client.GetAsync("/streaming");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Slow content arrived", body);
    }

    [Fact]
    public async Task Error_boundary_component_renders_safe_content()
    {
        var response = await client.GetAsync("/errors");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("protected by a Anvil error boundary", body);
    }

    [Fact]
    public async Task Browser_runtime_is_opt_in_and_served_as_a_static_asset()
    {
        var page = await client.GetStringAsync("/overview");
        var script = await client.GetAsync("/_content/Anvil.Razor/anvil.js");
        var htmx = await client.GetAsync("/_content/Anvil.Razor/htmx-4.0.0.min.js");

        Assert.Contains("_content/Anvil.Razor/anvil.js", page);
        Assert.Contains("_content/Anvil.Razor/htmx-4.0.0.min.js", page);
        Assert.Contains("hx-boost=\"true\"", page);
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal(HttpStatusCode.OK, htmx.StatusCode);
        Assert.Equal("text/javascript", htmx.Content.Headers.ContentType?.MediaType);
        Assert.Contains("version=\"4.0.0\"", await htmx.Content.ReadAsStringAsync());
        var scriptBody = await script.Content.ReadAsStringAsync();
        Assert.Contains("data-anvil-toggle", scriptBody);
        Assert.Contains("data-anvil-signal-attr", scriptBody);
        Assert.Contains("data-anvil-bind", scriptBody);
        Assert.Contains("data-anvil-partial-form", scriptBody);
        Assert.Contains("data-anvil-state", page);
    }

    [Fact]
    public async Task Browser_navigation_contract_keeps_full_pages_and_partial_targets_separate()
    {
        using var navigation = new HttpRequestMessage(HttpMethod.Get, "/products");
        navigation.Headers.Add("X-Anvil-Navigation", "true");
        using var navigationResponse = await client.SendAsync(navigation);
        var navigationBody = await navigationResponse.Content.ReadAsStringAsync();

        using var partial = new HttpRequestMessage(HttpMethod.Get, "/api/inventory/table?search=espresso");
        partial.Headers.Add("X-Anvil-Partial", "true");
        using var partialResponse = await client.SendAsync(partial);
        var partialBody = await partialResponse.Content.ReadAsStringAsync();

        Assert.Contains("<html", navigationBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Espresso", partialBody);
        Assert.DoesNotContain("<html", partialBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sample_interactions_expose_accessible_controls()
    {
        var products = await client.GetStringAsync("/products");
        var settings = await client.GetStringAsync("/settings");

        Assert.Contains("aria-expanded=\"false\"", products);
        Assert.Contains("role=\"dialog\"", products);
        Assert.Contains("aria-modal=\"true\"", products);
        Assert.Contains("aria-labelledby=\"add-product-title\"", products);
        Assert.Contains("for=\"inventory-search\"", products);
        Assert.Contains("aria-live=\"polite\"", settings);
    }

    [Fact]
    public async Task Fragment_responses_remain_smaller_than_full_page_responses()
    {
        var page = await client.GetStringAsync("/products");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/inventory/table?search=espresso");
        request.Headers.Add("HX-Request", "true");
        var fragment = await (await client.SendAsync(request)).Content.ReadAsStringAsync();

        Assert.True(fragment.Length < page.Length);
        Assert.True(fragment.Length < 20_000);
    }

    [Fact]
    public async Task Procedure_endpoint_returns_typed_json()
    {
        using var content = JsonContent.Create(new { message = "hello" });

        var response = await client.PostAsync("/api/procedure", content);
        var body = await response.Content.ReadFromJsonAsync<ProcedureResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("HELLO", body?.Result);
    }

    [Fact]
    public async Task Sse_endpoint_streams_events()
    {
        var response = await client.GetAsync("/api/events");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("event: status", body);
        Assert.Contains("data: ready", body);
    }

    [Fact]
    public async Task Cookie_writes_survive_redirects()
    {
        using var noRedirectClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/cookie-redirect");
        var response = await noRedirectClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("redirect-check=ok"));
    }

    [Fact]
    public async Task Cookie_writes_survive_handled_errors()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/cookie-error");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("error-check=ok"));
    }

    [Fact]
    public async Task Echo_endpoint_binds_json_and_returns_json()
    {
        using var content = JsonContent.Create(new { message = "hello" });

        var response = await client.PostAsync("/api/echo", content);
        var body = await response.Content.ReadFromJsonAsync<EchoResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("hello", body?.Message);
    }

    [Fact]
    public async Task Echo_endpoint_returns_validation_problem_for_invalid_json()
    {
        using var content = JsonContent.Create(new { message = string.Empty });

        var response = await client.PostAsync("/api/echo", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Message", body);
        Assert.Contains("required", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task Echo_endpoint_supports_update_verbs(string method)
    {
        using var content = JsonContent.Create(new { message = "updated" });
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/echo")
        {
            Content = content
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Echo_endpoint_supports_delete()
    {
        var response = await client.DeleteAsync("/api/echo");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Form_endpoint_binds_and_validates_form_data()
    {
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Message", "from form")
        ]);

        var antiforgery = factory.Services.GetRequiredService<IAntiforgery>();
        var tokenContext = new DefaultHttpContext
        {
            RequestServices = factory.Services
        };
        var tokens = antiforgery.GetAndStoreTokens(tokenContext);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/form-echo")
        {
            Content = content
        };
        request.Headers.Add("RequestVerificationToken", tokens.RequestToken!);
        request.Headers.Add("Cookie", tokenContext.Response.Headers.SetCookie.ToString().Split(';')[0]);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<EchoResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("from form", body?.Message);
    }

    private sealed record HealthResponse(string Status);

    private sealed record EchoResponse(string Message);

    private sealed record LinkResponse(string Url);

    private sealed record ProcedureResponse(string Result);
}
