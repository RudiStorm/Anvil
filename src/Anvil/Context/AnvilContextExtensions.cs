using Microsoft.Extensions.DependencyInjection;

namespace Anvil;

public static class AnvilContextExtensions
{
    public static T GetApplicationService<T>(this RequestContext context)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.HttpContext.RequestServices.GetRequiredService<T>();
    }

    public static T? TryGetApplicationService<T>(this RequestContext context)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.HttpContext.RequestServices.GetService<T>();
    }

    public static void SetValue<T>(this RequestContext context, T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HttpContext.Items[typeof(T)] = value;
    }

    public static bool TryGetValue<T>(this RequestContext context, out T? value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.HttpContext.Items.TryGetValue(typeof(T), out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = null;
        return false;
    }

    public static IDisposable PushValue<T>(this RequestContext context, T value)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        var items = context.HttpContext.Items;
        var key = typeof(T);
        var hadPrevious = items.TryGetValue(key, out var previous);
        items[key] = value;
        return new ValueScope(items, key, hadPrevious, previous);
    }

    public static ValueTask<T> MemoizeAsync<T>(
        this RequestContext context,
        string key,
        Func<CancellationToken, ValueTask<T>> factory)
    {
        ArgumentNullException.ThrowIfNull(context);
        var memoizer = context.GetApplicationService<AnvilRequestMemoizer>();
        return memoizer.GetOrCreateAsync(key, factory, context.RequestAborted);
    }

    private sealed class ValueScope(
        IDictionary<object, object?> items,
        object key,
        bool hadPrevious,
        object? previous) : IDisposable
    {
        public void Dispose()
        {
            if (hadPrevious) items[key] = previous;
            else items.Remove(key);
        }
    }
}
