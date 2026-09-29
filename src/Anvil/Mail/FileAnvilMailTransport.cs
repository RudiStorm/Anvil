using Microsoft.Extensions.Options;
using System.Text;

namespace Anvil;

public sealed class FileAnvilMailTransport(IOptions<AnvilMailOptions> options) : IAnvilMailTransport
{
    public async ValueTask SendAsync(AnvilMailMessage message, CancellationToken cancellationToken = default)
    {
        var directory = options.Value.FileDirectory
            ?? throw new InvalidOperationException("Anvil mail FileDirectory is not configured.");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.eml");
        var text = BuildMessage(message);
        await File.WriteAllTextAsync(path, text, cancellationToken);
    }

    private static string BuildMessage(AnvilMailMessage message)
    {
        ValidateHeader(message.From, nameof(message.From));
        ValidateHeader(message.To, nameof(message.To));
        ValidateHeader(message.Subject, nameof(message.Subject));

        var mixedBoundary = $"anvil-mixed-{Guid.NewGuid():N}";
        var alternativeBoundary = $"anvil-alt-{Guid.NewGuid():N}";
        var builder = new StringBuilder()
            .Append("From: ").Append(message.From).Append("\r\n")
            .Append("To: ").Append(message.To).Append("\r\n")
            .Append("Subject: ").Append(EncodeHeader(message.Subject)).Append("\r\n")
            .Append("MIME-Version: 1.0\r\n")
            .Append("Content-Type: multipart/mixed; boundary=\"").Append(mixedBoundary).Append("\"\r\n\r\n")
            .Append("--").Append(mixedBoundary).Append("\r\n")
            .Append("Content-Type: multipart/alternative; boundary=\"").Append(alternativeBoundary).Append("\"\r\n\r\n");

        AppendTextPart(builder, alternativeBoundary, "text/plain", message.PlainText);
        AppendTextPart(builder, alternativeBoundary, "text/html", message.Html);
        builder.Append("--").Append(alternativeBoundary).Append("--\r\n");

        foreach (var attachment in message.Attachments)
        {
            ValidateHeader(attachment.FileName, nameof(attachment.FileName));
            ValidateHeader(attachment.ContentType, nameof(attachment.ContentType));
            builder.Append("--").Append(mixedBoundary).Append("\r\n")
                .Append("Content-Type: ").Append(attachment.ContentType).Append("; name*=UTF-8''")
                .Append(Uri.EscapeDataString(attachment.FileName)).Append("\r\n")
                .Append("Content-Disposition: attachment; filename*=UTF-8''")
                .Append(Uri.EscapeDataString(attachment.FileName)).Append("\r\n")
                .Append("Content-Transfer-Encoding: base64\r\n\r\n");
            AppendBase64(builder, attachment.Content);
        }

        return builder.Append("--").Append(mixedBoundary).Append("--\r\n").ToString();
    }

    private static void AppendTextPart(StringBuilder builder, string boundary, string contentType, string value)
    {
        builder.Append("--").Append(boundary).Append("\r\n")
            .Append("Content-Type: ").Append(contentType).Append("; charset=utf-8\r\n")
            .Append("Content-Transfer-Encoding: base64\r\n\r\n");
        AppendBase64(builder, Encoding.UTF8.GetBytes(value));
    }

    private static void AppendBase64(StringBuilder builder, byte[] bytes)
    {
        var encoded = Convert.ToBase64String(bytes);
        for (var index = 0; index < encoded.Length; index += 76)
            builder.Append(encoded, index, Math.Min(76, encoded.Length - index)).Append("\r\n");
    }

    private static string EncodeHeader(string value) =>
        value.All(static character => character is >= '\x20' and <= '\x7e')
            ? value
            : $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?=";

    private static void ValidateHeader(string value, string name)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\r') || value.Contains('\n'))
            throw new ArgumentException("Header values cannot contain CR or LF.", name);
    }
}
