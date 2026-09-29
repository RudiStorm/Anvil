using Microsoft.AspNetCore.Http;

namespace Anvil;

public static class AnvilResults
{
    public static IResult BadRequest(string? detail = null) => Results.Problem(statusCode: 400, title: "Bad request", detail: detail);
    public static IResult NotFound(string? detail = null) => Results.Problem(statusCode: 404, title: "Not found", detail: detail);
    public static IResult SeeOther(string location) => Results.Redirect(location, preserveMethod: false);
    public static IResult TemporaryRedirect(string location) => Results.Redirect(location, preserveMethod: true);
}
