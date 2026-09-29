namespace Anvil;

public sealed class InMemoryAnvilMailTransport : IAnvilMailTransport
{
    private readonly List<AnvilMailMessage> messages = [];
    public IReadOnlyList<AnvilMailMessage> Messages => messages;

    public ValueTask SendAsync(AnvilMailMessage message, CancellationToken cancellationToken = default)
    {
        messages.Add(message);
        return ValueTask.CompletedTask;
    }
}
