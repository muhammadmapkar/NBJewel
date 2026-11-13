// Role: Configures email confirmation and performs dummy password verification for unknown accounts.
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
namespace NbJewel.Api.Identity;

public interface IPasswordWork { void VerifyDummy(string password); }
public sealed class PasswordWork : IPasswordWork
{
    private readonly IPasswordHasher<ApplicationUser> hasher;
    private readonly ApplicationUser dummy = new();
    private readonly string hash;
    public PasswordWork(IOptions<PasswordHasherOptions> options)
    {
        hasher = new PasswordHasher<ApplicationUser>(options);
        hash = hasher.HashPassword(dummy, Guid.NewGuid().ToString());
    }
    public void VerifyDummy(string password) => hasher.VerifyHashedPassword(dummy, hash, password);
}
public interface IAccountVerificationSender
{
    bool IsConfigured { get; }
    Task SendAsync(string email, string userId, string token, CancellationToken ct);
}
public sealed class AccountVerification(IConfiguration configuration, IWebHostEnvironment environment, IAccountVerificationSender sender, IPasswordWork passwordWork)
{
    public bool Required => configuration.GetValue<bool?>("AiSecurity:RequireEmailVerification") ?? !environment.IsDevelopment();
    public bool SenderConfigured => sender.IsConfigured;
    public Task SendAsync(string email, string userId, string token, CancellationToken ct) => sender.SendAsync(email, userId, token, ct);
    public void DummyWork(string password) => passwordWork.VerifyDummy(password);
}
public sealed class SmtpAccountVerificationSender(IConfiguration configuration, IWebHostEnvironment environment) : IAccountVerificationSender
{
    private string Host => configuration["SMTP_HOST"] ?? "";
    private string From => configuration["SMTP_FROM"] ?? "";
    private Uri? Origin => Uri.TryCreate(configuration["APP_PUBLIC_ORIGIN"], UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || (environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp)) &&
        string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) ? uri : null;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && MailAddress.TryCreate(From, out _) && Origin is not null;
    public async Task SendAsync(string email, string userId, string token, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Verification sender unavailable.");
        var port = int.TryParse(configuration["SMTP_PORT"], out var parsed) && parsed is > 0 and <= 65535 ? parsed : 587;
        using var smtp = new SmtpClient(Host, port) { EnableSsl = true, Timeout = 10000 };
        var username = configuration["SMTP_USERNAME"];
        if (!string.IsNullOrWhiteSpace(username)) smtp.Credentials = new NetworkCredential(username, configuration["SMTP_PASSWORD"]);
        var link = new Uri(Origin!, "verify-email").AbsoluteUri + "#userId=" + Uri.EscapeDataString(userId) + "&token=" + Uri.EscapeDataString(token);
        using var message = new MailMessage(From, email, "Verify your email", "Open this link to verify your email: " + link);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}
