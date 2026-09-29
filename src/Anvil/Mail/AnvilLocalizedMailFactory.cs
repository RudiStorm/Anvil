using Microsoft.Extensions.Localization;

namespace Anvil;

public sealed class AnvilLocalizedMailFactory(IStringLocalizerFactory localizerFactory)
{
    public AnvilMailMessage Create<TResource>(
        string from,
        string to,
        string subjectKey,
        string htmlKey,
        string? textKey = null,
        params object[] arguments)
    {
        var localizer = localizerFactory.Create(typeof(TResource));
        var subject = localizer[subjectKey, arguments].Value;
        var html = localizer[htmlKey, arguments].Value;
        var text = textKey is null ? null : localizer[textKey, arguments].Value;
        return new AnvilMailMessage(from, to, subject, html, text);
    }
}
