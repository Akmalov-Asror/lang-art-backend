using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace LangArt.Api.Common.Email;

/// <summary>
/// Default email sender. Tries to deliver via the configured SMTP host (in docker compose
/// that's the bundled smtp4dev container at <c>smtp4dev:25</c>). If SMTP connection fails
/// — typical for native <c>dotnet run</c> outside the compose network — it logs the email
/// to the console instead, so password-reset flows still work end-to-end in dev.
/// </summary>
public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;
    private readonly IOptions<SmtpOptions> _opts;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger, IOptions<SmtpOptions> opts)
    {
        _logger = logger;
        _opts = opts;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, string? plainTextBody = null, CancellationToken ct = default)
    {
        var opt = _opts.Value;

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(opt.FromName, opt.FromAddress));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = plainTextBody ?? StripTags(htmlBody),
        };
        msg.Body = builder.ToMessageBody();

        try
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(opt.Host, opt.Port, opt.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None, ct);
            if (!string.IsNullOrEmpty(opt.Username))
            {
                await smtp.AuthenticateAsync(opt.Username, opt.Password ?? "", ct);
            }
            await smtp.SendAsync(msg, ct);
            await smtp.DisconnectAsync(true, ct);
            _logger.LogInformation("Email delivered: {Subject} to {To} (via {Host}:{Port})", subject, to, opt.Host, opt.Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP unavailable — logging email to console instead.");
            _logger.LogInformation(
                "[CONSOLE EMAIL]\n  TO:      {To}\n  SUBJECT: {Subject}\n  BODY:\n{Body}",
                to,
                subject,
                plainTextBody ?? StripTags(htmlBody));
        }
    }

    private static string StripTags(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", "").Trim();
}
