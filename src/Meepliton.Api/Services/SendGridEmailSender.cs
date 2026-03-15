using Meepliton.Api.Identity;
using Microsoft.AspNetCore.Identity;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Meepliton.Api.Services;

/// <summary>
/// Sends transactional email via SendGrid. Activated when SENDGRID_API_KEY is present.
/// </summary>
public sealed class SendGridEmailSender(
    IConfiguration configuration,
    ILogger<SendGridEmailSender> logger) : IEmailSender<ApplicationUser>
{
    private readonly string _apiKey    = configuration["SENDGRID_API_KEY"]!;
    private readonly string _fromEmail = configuration["Email:FromAddress"] ?? "noreply@meepliton.com";
    private readonly string _fromName  = configuration["Email:FromName"]    ?? "Meepliton";

    public async Task SendConfirmationLinkAsync(
        ApplicationUser user, string email, string confirmationLink)
    {
        var subject = "Confirm your Meepliton account";
        var html = $"""
            <p>Hi {HtmlEncode(user.DisplayName)},</p>
            <p>Thanks for signing up! Click the link below to confirm your email address:</p>
            <p><a href="{confirmationLink}">Confirm my email</a></p>
            <p>If you did not create an account, you can ignore this email.</p>
            """;
        await SendAsync(email, subject, html);
    }

    public async Task SendPasswordResetLinkAsync(
        ApplicationUser user, string email, string resetLink)
    {
        var subject = "Reset your Meepliton password";
        var html = $"""
            <p>Hi {HtmlEncode(user.DisplayName)},</p>
            <p>Click the link below to reset your password. The link expires in 1 hour.</p>
            <p><a href="{resetLink}">Reset my password</a></p>
            <p>If you did not request a password reset, you can ignore this email.</p>
            """;
        await SendAsync(email, subject, html);
    }

    public async Task SendPasswordResetCodeAsync(
        ApplicationUser user, string email, string resetCode)
    {
        var subject = "Reset your Meepliton password";
        var html = $"""
            <p>Hi {HtmlEncode(user.DisplayName)},</p>
            <p>Your password reset code is: <strong>{resetCode}</strong></p>
            <p>If you did not request a password reset, you can ignore this email.</p>
            """;
        await SendAsync(email, subject, html);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlContent)
    {
        var client  = new SendGridClient(_apiKey);
        var from    = new EmailAddress(_fromEmail, _fromName);
        var to      = new EmailAddress(toEmail);
        var msg     = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: null, htmlContent);

        var response = await client.SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync();
            logger.LogError("SendGrid returned {StatusCode}: {Body}", (int)response.StatusCode, body);
        }
    }

    private static string HtmlEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
