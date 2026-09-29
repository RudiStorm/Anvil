using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Anvil;

#pragma warning disable SYSLIB0005
public sealed class SmtpAnvilMailTransport(IOptions<AnvilMailOptions> options) : IAnvilMailTransport
{
    public async ValueTask SendAsync(AnvilMailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            throw new InvalidOperationException("Anvil mail SmtpHost is not configured.");
        }

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.SmtpEnableSsl,
            Credentials = string.IsNullOrEmpty(settings.SmtpUserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(settings.SmtpUserName, settings.SmtpPassword)
        };
        using var mail = new MailMessage(message.From, message.To, message.Subject, message.Html)
        {
            IsBodyHtml = true
        };
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.PlainText, null, "text/plain"));
        foreach (var attachment in message.Attachments)
        {
            mail.Attachments.Add(new Attachment(new MemoryStream(attachment.Content), attachment.FileName, attachment.ContentType));
        }
        await client.SendMailAsync(mail, cancellationToken);
    }
}
#pragma warning restore SYSLIB0005
