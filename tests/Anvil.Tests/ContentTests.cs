using Anvil;

namespace Anvil.Tests;

public sealed class ContentTests
{
    [Fact]
    public void Markdown_renderer_escapes_markup_and_renders_headings()
    {
        var html = AnvilMarkdownRenderer.Render("# Title\n\nHello <script>alert(1)</script>");

        Assert.Contains("<h1>Title</h1>", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
