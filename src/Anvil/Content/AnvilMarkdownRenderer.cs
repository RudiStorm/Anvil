using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Anvil;

public static partial class AnvilMarkdownRenderer
{
    public static string Render(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var output = new StringBuilder();
        foreach (var line in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var escaped = WebUtility.HtmlEncode(line);
            var content = Inline().Replace(escaped, match => match.Groups["content"].Value);
            if (line.StartsWith("### ")) output.Append("<h3>").Append(content[4..]).Append("</h3>");
            else if (line.StartsWith("## ")) output.Append("<h2>").Append(content[3..]).Append("</h2>");
            else if (line.StartsWith("# ")) output.Append("<h1>").Append(content[2..]).Append("</h1>");
            else output.Append("<p>").Append(content).Append("</p>");
        }
        return output.ToString();
    }

    [GeneratedRegex(@"\*\*(?<content>[^*]+)\*\*")]
    private static partial Regex Inline();
}
