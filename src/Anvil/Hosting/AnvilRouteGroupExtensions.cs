using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Anvil;

public static class AnvilRouteGroupExtensions
{
    public static RouteGroupBuilder MapAnvilGroup(this IEndpointRouteBuilder endpoints, string prefix)
    {
        return endpoints.MapGroup(prefix);
    }
}
