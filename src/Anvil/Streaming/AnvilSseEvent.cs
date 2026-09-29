using System.Text;

namespace Anvil;

public sealed record AnvilSseEvent(string Data, string? Event = null, string? Id = null)
{
    public static AnvilSseEvent KeepAlive() => new(string.Empty);

    public string Format()
    {
        ValidateMetadata(Event, nameof(Event));
        ValidateMetadata(Id, nameof(Id));
        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(Id)) builder.Append("id: ").Append(Id).Append('\n');
        if (!string.IsNullOrEmpty(Event)) builder.Append("event: ").Append(Event).Append('\n');
        foreach (var line in Data.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            builder.Append("data: ").Append(line).Append('\n');
        builder.Append('\n');
        return builder.ToString();
    }

    private static void ValidateMetadata(string? value, string name)
    {
        if (value?.Contains('\r') == true || value?.Contains('\n') == true)
            throw new ArgumentException("SSE metadata cannot contain CR or LF.", name);
    }
}
