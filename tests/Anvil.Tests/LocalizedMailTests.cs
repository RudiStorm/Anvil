using Anvil;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace Anvil.Tests;

public sealed class LocalizedMailTests
{
    [Fact]
    public void Factory_renders_subject_and_body_from_current_localizer()
    {
        var factory = new AnvilLocalizedMailFactory(new TestLocalizerFactory());

        var message = factory.Create<WelcomeMail>(
            "from@example.test",
            "to@example.test",
            "subject",
            "html",
            "text",
            "Ada");

        Assert.Equal("Welcome, Ada", message.Subject);
        Assert.Equal("<p>Hello, Ada</p>", message.Html);
        Assert.Equal("Hello, Ada", message.Text);
    }

    private sealed class WelcomeMail;

    private sealed class TestLocalizerFactory : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) => new TestLocalizer();
        public IStringLocalizer Create(string baseName, string location) => new TestLocalizer();
    }

    private sealed class TestLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => this[name, []];

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var value = name switch
                {
                    "subject" => "Welcome, {0}",
                    "html" => "<p>Hello, {0}</p>",
                    "text" => "Hello, {0}",
                    _ => name
                };
                return new LocalizedString(name, string.Format(CultureInfo.InvariantCulture, value, arguments));
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
        public IStringLocalizer WithCulture(CultureInfo culture) => this;
    }
}
