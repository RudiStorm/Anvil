using Microsoft.AspNetCore.Http;

namespace Anvil;

public static class AnvilRedirectExtensions
{
    public static IResult SeeOther(string location) => Results.Redirect(location, permanent: false, preserveMethod: false);
    public static IResult TemporaryRedirect(string location) => Results.Redirect(location, permanent: false, preserveMethod: true);
}
