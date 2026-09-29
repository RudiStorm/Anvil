using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Anvil;

public sealed class AnvilFragmentRenderer(
    IServiceProvider services,
    ILoggerFactory loggerFactory)
{
    public async Task<string> RenderAsync<TComponent>(
        object? parameters = null,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent
    {
        await using var renderer = new HtmlRenderer(services, loggerFactory);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var component = await renderer.RenderComponentAsync<TComponent>(
                ToParameterView(parameters));
            return component.ToHtmlString();
        });
    }

    private static ParameterView ToParameterView(object? parameters)
    {
        if (parameters is null) return ParameterView.Empty;
        if (parameters is IReadOnlyDictionary<string, object?> dictionary)
            return ParameterView.FromDictionary(dictionary.ToDictionary());

        var values = parameters.GetType()
            .GetProperties()
            .Where(property => property.GetMethod is not null)
            .ToDictionary(property => property.Name, property => property.GetValue(parameters));

        return ParameterView.FromDictionary(values);
    }
}
