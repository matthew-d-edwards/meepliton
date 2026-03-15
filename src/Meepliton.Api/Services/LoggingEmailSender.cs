using Meepliton.Api.Identity;
using Microsoft.AspNetCore.Identity;

namespace Meepliton.Api.Services;

/// <summary>
/// No-op email sender used when SENDGRID_API_KEY is not configured (local dev / CI).
/// Logs email content at Information level so developers can inspect links without a mail server.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        logger.LogInformation(
            "[DEV EMAIL] Confirmation link for {Email}: {Link}",
            email, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        logger.LogInformation(
            "[DEV EMAIL] Password reset link for {Email}: {Link}",
            email, resetLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        logger.LogInformation(
            "[DEV EMAIL] Password reset code for {Email}: {Code}",
            email, resetCode);
        return Task.CompletedTask;
    }
}
