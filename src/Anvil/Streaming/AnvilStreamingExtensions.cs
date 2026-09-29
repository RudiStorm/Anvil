namespace Anvil;

public static class AnvilStreamingExtensions
{
    public static Task FlushAsync(
        this RequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Response.Body.FlushAsync(
            cancellationToken == default ? context.RequestAborted : cancellationToken);
    }

    public static bool IsClientConnected(this RequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return !context.RequestAborted.IsCancellationRequested;
    }
}
