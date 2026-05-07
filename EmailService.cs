using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
namespace JsbaiBackend.Services;
public interface IEmailService {
    Task SendAuthorConfirmationAsync(string toEmail, string toName, string refId, string title, string articleType, string affiliation);
    Task SendEditorNotificationAsync(string refId, string title, string articleType, string authorName, string authorEmail, string affiliation, string? manuscriptUrl);
    Task SendCustomEmailAsync(string toEmail, string subject, string body);
}
public class EmailService : IEmailService {
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;
    public EmailService(IConfiguration config, ILogger<EmailService> logger) { _config = config; _logger = logger; }
    public async Task SendAuthorConfirmationAsync(string toEmail, string toName, string refId, string title, string articleType, string affiliation) {
        await SendEmailAsync(toEmail, $"Manuscript Received — {refId} — JSBAI",
            $"Dear {toName},\n\nThank you for submitting to JSBAI.\n\nReference ID: {refId}\nTitle: {title}\nType: {articleType}\nAffiliation: {affiliation}\nSubmitted: {DateTime.UtcNow:dd MMM yyyy}\n\nKind regards,\nEditorial Office — JSBAI");
    }
    public async Task SendEditorNotificationAsync(string refId, string title, string articleType, string authorName, string authorEmail, string affiliation, string? manuscriptUrl) {
        var editorEmail = _config["Email:EditorEmail"] ?? "";
        await SendEmailAsync(editorEmail, $"[NEW SUBMISSION] {refId} — {articleType}",
            $"New submission received.\n\nRef ID: {refId}\nTitle: {title}\nType: {articleType}\nAuthor: {authorName} <{authorEmail}>\nAffiliation: {affiliation}\nManuscript: {manuscriptUrl ?? "On server"}");
    }
    public async Task SendCustomEmailAsync(string toEmail, string subject, string body) {
        await SendEmailAsync(toEmail, subject, body);
    }
    private async Task SendEmailAsync(string toEmail, string subject, string body) {
        try {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("JSBAI Editorial Office", _config["Email:SenderEmail"]));
            msg.To.Add(new MailboxAddress("", toEmail));
            msg.Subject = subject;
            msg.Body = new TextPart("plain") { Text = body };
            using var client = new SmtpClient();
            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_config["Email:SenderEmail"], _config["Email:AppPassword"]);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
        } catch (Exception ex) { _logger.LogError(ex, "Email failed to {Email}", toEmail); }
    }
}
