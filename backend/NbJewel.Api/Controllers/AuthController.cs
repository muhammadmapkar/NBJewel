// Role: Provides CSRF tokens and cookie sessions; public registration creates customers without ERP grants.
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NbJewel.Api.Identity;

namespace NbJewel.Api.Controllers;

public sealed record LoginRequest([Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(1), MaxLength(128)] string Password);

public sealed record ConfirmEmailRequest([Required, MaxLength(256)] string UserId, [Required, MaxLength(4096)] string Token);
public sealed record ResendVerificationRequest([Required, EmailAddress, MaxLength(254)] string Email);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    IPermissionAdapter permissions, IdentityAvailability availability, IAntiforgery antiforgery, AccountVerification verification, SampleIdentityOptions sampleIdentity) : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var access = id is null ? null : await permissions.ResolveAsync(id, cancellationToken);
        return access is null ? Unauthorized() : Ok(access);
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(LoginRequest request, CancellationToken cancellationToken)
    {
        if (sampleIdentity.Enabled) return StatusCode(403, new { message = "Customer registration is unavailable in local sample staff mode." });
        if (!availability.Configured) return StatusCode(503, new { message = "Identity is not configured." });
        if (verification.Required && !verification.SenderConfigured) return VerificationUnavailable();
        var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), AccountType = "customer" };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { message = "Registration failed. Use a unique email and a password of at least 12 characters with upper case, lower case, a digit and a symbol." });
        if (verification.Required)
        {
            try { await verification.SendAsync(user.Email!, user.Id, await users.GenerateEmailConfirmationTokenAsync(user), cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { return VerificationUnavailable(); }
            return VerificationAccepted();
        }
        await signIn.SignInAsync(user, isPersistent: false);
        return Ok(await permissions.ResolveAsync(user.Id, cancellationToken));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (!availability.Configured) return StatusCode(503, new { message = "Identity is not configured." });
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) { verification.DummyWork(request.Password); return Unauthorized(new { message = "Invalid email or password." }); }
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized(new { message = "Invalid email or password." });
        if (verification.Required && !await users.IsEmailConfirmedAsync(user))
            return Unauthorized(new { message = "Invalid email or password." });
        await signIn.SignInAsync(user, isPersistent: false);
        return Ok(await permissions.ResolveAsync(user.Id, cancellationToken));
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
    {
        if (!availability.Configured) return StatusCode(503, new { message = "Identity is not configured." });
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null || !(await users.ConfirmEmailAsync(user, request.Token)).Succeeded)
            return BadRequest(new { message = "Invalid verification request." });
        return Ok(new { message = "Email verified." });
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        if (!availability.Configured) return StatusCode(503, new { message = "Identity is not configured." });
        if (!verification.SenderConfigured) return VerificationUnavailable();
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) verification.DummyWork(request.Email);
        else if (!await users.IsEmailConfirmedAsync(user))
        {
            try { await verification.SendAsync(user.Email!, user.Id, await users.GenerateEmailConfirmationTokenAsync(user), cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch { /* Keep this response generic for both unknown accounts and delivery failures. */ }
        }
        return VerificationAccepted();
    }

    private ObjectResult VerificationUnavailable() => StatusCode(503, new { message = "Email verification is unavailable." });
    private ObjectResult VerificationAccepted() => Accepted(new { verificationRequired = true, message = "Check your email to verify your account." });

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }
}
