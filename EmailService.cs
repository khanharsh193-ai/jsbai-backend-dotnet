using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace JsbaiBackend.Services;

// ── Interface ─────────────────────────────────────────────────────────────────
// An interface is like a contract — it says "any email service MUST have these methods"
// This makes it easy to swap email providers later without changing the rest of the code
public interface IEmailService
{
    Task SendAuthorConfirmationAsync(string toEmail, string toName, string refId,
        string title, string articleType, string affiliation);

    Task SendEditorNotificationAsync(string refId, string title, string articleType,
        string authorName, string authorEmail, string affiliation,
        string? manuscriptUrl);
}

// ── Implementation ────────────────────────────────────────────────────────────
/// <summary>
/// Sends emails using Gmail SMTP via the MailKit library.
/// SMTP is the standard protocol for sending emails — like the postal system for email.
/// Gmail allows you to use it for free with an App Password.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAuthorConfirmationAsync(string toEmail, string toName,
        string refId, string title, string articleType, string affiliation)
    {
        var subject = $"Manuscript Received — {refId} — JSBAI";
        var body = $@"
Dear {toName},

Thank you for submitting your manuscript to the Journal of Sustainable 
Biosciences and Agricultural Innovation (JSBAI).

Your submission has been received and recorded with the following details:

  Reference ID  :  {refId}
  Manuscript    :  {title}
  Article Type  :  {articleType}
  Affiliation   :  {affiliation}
  Submitted On  :  {DateTime.UtcNow:dd MMMM yyyy, HH:mm} UTC

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Please retain this Reference ID for all future correspondence.

What happens next:
  1. Initial editorial screening (5–7 working days)
  2. Double-blind peer review (6–10 weeks)
  3. You will be notified at each stage at this email address

Kind regards,
Editorial Office — JSBAI
editor.jsbai@bioagriacad.in
";
        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendEditorNotificationAsync(string refId, string title,
        string articleType, string authorName, string authorEmail,
        string affiliation, string? manuscriptUrl)
    {
        var editorEmail = _config["Email:EditorEmail"] ?? "";
        var subject = $"[NEW SUBMISSION] {refId} — {articleType}";
        var body = $@"
New manuscript submission received at JSBAI.

━━━━━━━━━━━━━━━━━━
SUBMISSION DETAILS
━━━━━━━━━━━━━━━━━━

Reference ID  :  {refId}
Title         :  {title}
Article Type  :  {articleType}
Author        :  {authorName}
Author Email  :  {authorEmail}
Affiliation   :  {affiliation}
Manuscript    :  {manuscriptUrl ?? "Stored on server"}
Submitted     :  {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Login to the admin panel to review and update status.
";
        await SendEmailAsync(editorEmail, subject, body);
    }

    // ── Core send method ───────────────────────────────────────────────────
    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("JSBAI Editorial Office",
                _config["Email:SenderEmail"]));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();

            // Connect to Gmail's SMTP server
            // Port 587 = standard secure email sending port
            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);

            // Login with Gmail credentials from appsettings.json
            await client.AuthenticateAsync(
                _config["Email:SenderEmail"],
                _config["Email:AppPassword"]   // Gmail App Password (not your normal password)
            );

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // Log the error but don't crash the whole submission
            // The submission still gets saved even if email fails
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }
}
