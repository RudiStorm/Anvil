namespace Anvil;

public sealed class AnvilRoute<TValues>(string template)
{
    public string Template { get; } = template ?? throw new ArgumentNullException(nameof(template));

    public string Link(
        TValues values,
        object? queryValues = null,
        string? fragment = null)
    {
        return AnvilLink.Build(Template, values, queryValues, fragment);
    }

    public string FormAction(
        TValues values,
        object? queryValues = null,
        string? fragment = null)
    {
        return Link(values, queryValues, fragment);
    }

    public string AbsoluteLink(
        RequestContext context,
        TValues values,
        object? queryValues = null,
        string? fragment = null)
    {
        return context.AbsoluteLink(Link(values, queryValues, fragment));
    }

    public bool IsCurrent(RequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.IsCurrent(Template);
    }

    public bool IsCurrent(RequestContext context, TValues values)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.IsCurrent(Link(values));
    }
}
