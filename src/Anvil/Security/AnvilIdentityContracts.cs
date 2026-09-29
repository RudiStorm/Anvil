using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Anvil;

public class ApplicationUser : IdentityUser
{
}

public class AnvilIdentityDbContext<TUser>(DbContextOptions options) : IdentityDbContext<TUser>(options)
    where TUser : IdentityUser
{
}

public enum AnvilRegistrationMode
{
    Public,
    InviteOnly,
    Disabled
}

public sealed class AnvilIdentityOptions
{
    public AnvilRegistrationMode RegistrationMode { get; set; } = AnvilRegistrationMode.Public;
    public string LoginPath { get; set; } = "/account/login";
    public string LogoutPath { get; set; } = "/account/logout";
    public string RegisterPath { get; set; } = "/account/register";
    public string CurrentUserPath { get; set; } = "/account/me";
    public string ConfirmEmailPath { get; set; } = "/account/confirm-email";
    public string ForgotPasswordPath { get; set; } = "/account/forgot-password";
    public string ResetPasswordPath { get; set; } = "/account/reset-password";
    public string MfaSetupPath { get; set; } = "/account/mfa/setup";
    public string MfaVerifyPath { get; set; } = "/account/mfa/verify";
    public string MfaLoginPath { get; set; } = "/account/mfa/login";
    public string MfaRecoveryLoginPath { get; set; } = "/account/mfa/recovery-login";
    public string MfaRecoveryCodesPath { get; set; } = "/account/mfa/recovery-codes";
    public string ExternalLoginPath { get; set; } = "/account/external";
    public bool RequireConfirmedEmail { get; set; }
}

public sealed record AnvilLoginRequest(string UserName, string Password, bool RememberMe = false);
public sealed record AnvilRegisterRequest(string UserName, string Email, string Password);
public sealed record AnvilConfirmEmailRequest(string UserId, string Token);
public sealed record AnvilForgotPasswordRequest(string UserNameOrEmail);
public sealed record AnvilResetPasswordRequest(string UserId, string Token, string NewPassword);
public sealed record AnvilMfaVerifyRequest(string Code);
public sealed record AnvilMfaLoginRequest(string Code, bool RememberMe = false, bool RememberMachine = false);
public interface IAnvilIdentityMessageSender<TUser> where TUser : IdentityUser
{
    ValueTask SendEmailConfirmationAsync(TUser user, string token, CancellationToken cancellationToken = default);
    ValueTask SendPasswordResetAsync(TUser user, string token, CancellationToken cancellationToken = default);
}

public static class AnvilPermissions
{
    public const string CustomersView = "customers.view";
    public const string CustomersCreate = "customers.create";
    public const string CustomersUpdate = "customers.update";
    public const string CustomersDelete = "customers.delete";
}

public interface IAnvilPasskeyService
{
    ValueTask<string> CreateRegistrationOptionsAsync(string userId, CancellationToken cancellationToken = default);
    ValueTask<string> CreateAuthenticationOptionsAsync(CancellationToken cancellationToken = default);
    ValueTask<AnvilExternalIdentity?> VerifyAsync(string response, CancellationToken cancellationToken = default);
}

public interface IAnvilMfaService
{
    ValueTask<string> CreateChallengeAsync(string userId, CancellationToken cancellationToken = default);
    ValueTask<bool> VerifyChallengeAsync(string userId, string challenge, string code, CancellationToken cancellationToken = default);
}

public sealed record AnvilPasswordResetToken(string UserId, string Token, DateTimeOffset ExpiresAt);

public interface IAnvilPasswordResetService
{
    ValueTask<AnvilPasswordResetToken> IssueAsync(string userId, TimeSpan lifetime, CancellationToken cancellationToken = default);
    ValueTask<string?> ConsumeAsync(string token, CancellationToken cancellationToken = default);
}

public sealed record AnvilMfaSetup(string Secret, string ProvisioningUri);
public sealed record AnvilDeviceSession(string Id, string UserId, string? Name, DateTimeOffset CreatedAt, DateTimeOffset LastAccessedAt);

public interface IAnvilDeviceSessionStore
{
    ValueTask AddAsync(AnvilDeviceSession session, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<AnvilDeviceSession>> ListAsync(string userId, CancellationToken cancellationToken = default);
    ValueTask RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
    ValueTask RevokeAllAsync(string userId, string? exceptSessionId = null, CancellationToken cancellationToken = default);
}

public interface IAnvilExternalLoginProvider
{
    string Name { get; }
    string CreateChallenge(string returnUrl = "/");
}
