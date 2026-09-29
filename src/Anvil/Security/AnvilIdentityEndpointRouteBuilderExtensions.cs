using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anvil;

public static class AnvilIdentityEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAnvilIdentityEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapAnvilIdentityEndpoints<ApplicationUser>();

    public static IEndpointRouteBuilder MapAnvilIdentityEndpoints<TUser>(this IEndpointRouteBuilder endpoints)
        where TUser : IdentityUser, new()
    {
        var options = endpoints.ServiceProvider.GetService<IOptions<AnvilIdentityOptions>>()?.Value ?? new();
        var identity = endpoints.MapGroup(string.Empty);

        identity.MapPost(options.LoginPath, async (AnvilLoginRequest request, SignInManager<TUser> signInManager) =>
        {
            var result = await signInManager.PasswordSignInAsync(request.UserName, request.Password, request.RememberMe, true);
            return result.Succeeded ? Results.Ok() : result.RequiresTwoFactor ? Results.Ok(new { requiresTwoFactor = true }) : result.IsLockedOut ? Results.StatusCode(StatusCodes.Status423Locked) : Results.Unauthorized();
        }).RequireAnvilAntiforgery();

        identity.MapPost(options.MfaLoginPath, async (AnvilMfaLoginRequest request, SignInManager<TUser> signInManager) =>
        {
            var result = await signInManager.TwoFactorAuthenticatorSignInAsync(request.Code, request.RememberMe, request.RememberMachine);
            return result.Succeeded ? Results.Ok() : Results.Unauthorized();
        }).RequireAnvilAntiforgery();

        identity.MapPost(options.MfaRecoveryLoginPath, async (AnvilMfaLoginRequest request, SignInManager<TUser> signInManager) =>
        {
            var result = await signInManager.TwoFactorRecoveryCodeSignInAsync(request.Code);
            return result.Succeeded ? Results.Ok() : Results.Unauthorized();
        }).RequireAnvilAntiforgery();

        identity.MapPost(options.LogoutPath, async (SignInManager<TUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Ok();
        }).RequireAuthorization().RequireAnvilAntiforgery();

        identity.MapPost(options.RegisterPath, async (AnvilRegisterRequest request, UserManager<TUser> userManager, IOptions<AnvilIdentityOptions> settings, IEnumerable<IAnvilIdentityMessageSender<TUser>> senders) =>
        {
            if (settings.Value.RegistrationMode != AnvilRegistrationMode.Public) return Results.NotFound();
            var user = new TUser { UserName = request.UserName, Email = request.Email };
            var result = await userManager.CreateAsync(user, request.Password);
            if (result.Succeeded && user.Email is not null)
            {
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                foreach (var sender in senders) await sender.SendEmailConfirmationAsync(user, token);
            }
            return result.Succeeded ? Results.Ok(new { userId = user.Id, emailConfirmed = user.EmailConfirmed }) : IdentityErrors(result);
        }).RequireAnvilAntiforgery();

        identity.MapPost(options.ConfirmEmailPath, async (AnvilConfirmEmailRequest request, UserManager<TUser> users) =>
        {
            var user = await users.FindByIdAsync(request.UserId);
            if (user is null) return Results.NotFound();
            var result = await users.ConfirmEmailAsync(user, request.Token);
            return result.Succeeded ? Results.Ok() : IdentityErrors(result);
        }).RequireAnvilAntiforgery();

        identity.MapPost(options.ForgotPasswordPath, async (AnvilForgotPasswordRequest request, UserManager<TUser> users, IEnumerable<IAnvilIdentityMessageSender<TUser>> senders) =>
        {
            var user = await users.FindByNameAsync(request.UserNameOrEmail) ?? await users.FindByEmailAsync(request.UserNameOrEmail);
            if (user is not null && await users.IsEmailConfirmedAsync(user))
            {
                // The token is intentionally handed to the application's mail boundary, never echoed by this endpoint.
                var token = await users.GeneratePasswordResetTokenAsync(user);
                foreach (var sender in senders) await sender.SendPasswordResetAsync(user, token);
            }
            return Results.Ok(new { sent = true });
        }).RequireAnvilAntiforgery();

        identity.MapPost(options.ResetPasswordPath, async (AnvilResetPasswordRequest request, UserManager<TUser> users) =>
        {
            var user = await users.FindByIdAsync(request.UserId);
            if (user is null) return Results.BadRequest();
            var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
            return result.Succeeded ? Results.Ok() : IdentityErrors(result);
        }).RequireAnvilAntiforgery();

        identity.MapGet(options.CurrentUserPath, (HttpContext context) => Results.Ok(new
        {
            id = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            name = context.User.Identity?.Name,
            authenticated = context.User.Identity?.IsAuthenticated == true
        })).RequireAuthorization();

        identity.MapGet(options.MfaSetupPath, async (HttpContext context, UserManager<TUser> users, IOptions<AnvilIdentityOptions> settings) =>
        {
            var user = await CurrentUser(context, users);
            if (user is null) return Results.Unauthorized();
            var key = await users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await users.ResetAuthenticatorKeyAsync(user);
                key = await users.GetAuthenticatorKeyAsync(user);
            }
            return Results.Ok(new AnvilMfaSetup(key!, AnvilTotp.CreateProvisioningUri("Anvil", user.Email ?? user.UserName ?? user.Id, key!)));
        }).RequireAuthorization();

        identity.MapPost(options.MfaVerifyPath, async (HttpContext context, AnvilMfaVerifyRequest request, UserManager<TUser> users) =>
        {
            var user = await CurrentUser(context, users);
            if (user is null) return Results.Unauthorized();
            var valid = await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.Code);
            if (!valid) return Results.BadRequest(new { error = "Invalid authenticator code." });
            var result = await users.SetTwoFactorEnabledAsync(user, true);
            return result.Succeeded ? Results.Ok() : IdentityErrors(result);
        }).RequireAuthorization().RequireAnvilAntiforgery();

        identity.MapPost(options.MfaRecoveryCodesPath, async (HttpContext context, UserManager<TUser> users) =>
        {
            var user = await CurrentUser(context, users);
            if (user is null) return Results.Unauthorized();
            var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            return codes is null ? Results.BadRequest() : Results.Ok(new { codes });
        }).RequireAuthorization().RequireAnvilAntiforgery();

        identity.MapGet($"{options.ExternalLoginPath}/{{provider}}", (string provider, string? returnUrl, IEnumerable<IAnvilExternalLoginProvider> providers) =>
        {
            var external = providers.FirstOrDefault(x => string.Equals(x.Name, provider, StringComparison.OrdinalIgnoreCase));
            return external is null ? Results.NotFound() : Results.Redirect(external.CreateChallenge(returnUrl ?? "/"));
        });

        identity.MapGet("/account/devices", async (HttpContext context, UserManager<TUser> users, IAnvilDeviceSessionStore sessions) =>
        {
            var userId = users.GetUserId(context.User);
            return userId is null ? Results.Unauthorized() : Results.Ok(await sessions.ListAsync(userId));
        }).RequireAuthorization();

        identity.MapDelete("/account/devices/{sessionId}", async (string sessionId, HttpContext context, UserManager<TUser> users, IAnvilDeviceSessionStore sessions) =>
        {
            var userId = users.GetUserId(context.User);
            if (userId is null) return Results.Unauthorized();
            await sessions.RevokeAsync(userId, sessionId);
            return Results.NoContent();
        }).RequireAuthorization().RequireAnvilAntiforgery();

        identity.MapDelete("/account/devices", async (HttpContext context, UserManager<TUser> users, IAnvilDeviceSessionStore sessions) =>
        {
            var userId = users.GetUserId(context.User);
            if (userId is null) return Results.Unauthorized();
            await sessions.RevokeAllAsync(userId);
            await users.UpdateSecurityStampAsync(await users.FindByIdAsync(userId) ?? throw new InvalidOperationException("Identity user disappeared."));
            return Results.NoContent();
        }).RequireAuthorization().RequireAnvilAntiforgery();

        return endpoints;
    }

    private static async Task<TUser?> CurrentUser<TUser>(HttpContext context, UserManager<TUser> users) where TUser : IdentityUser
    {
        var id = users.GetUserId(context.User);
        return id is null ? null : await users.FindByIdAsync(id);
    }

    private static IResult IdentityErrors(IdentityResult result) =>
        Results.ValidationProblem(result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));

    private static RouteHandlerBuilder RequireAnvilAntiforgery(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            await context.HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>()
                .ValidateRequestAsync(context.HttpContext);
            return await next(context);
        });
}
