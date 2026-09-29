using Anvil;

namespace Anvil.Tests;

public sealed class MailTests
{
    [Fact]
    public async Task In_memory_transport_preserves_attachments()
    {
        var transport = new InMemoryAnvilMailTransport();
        var message = new AnvilMailMessage("a@example.test", "b@example.test", "Test", "<p>Hi</p>")
        {
            Attachments = [new AnvilMailAttachment("hello.txt", "text/plain", "hello"u8.ToArray())]
        };

        await transport.SendAsync(message);

        Assert.Single(transport.Messages);
        Assert.Equal("hello.txt", transport.Messages[0].Attachments[0].FileName);
    }
}
