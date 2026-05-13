namespace LangArt.Api.Common.Email;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, string? plainTextBody = null, CancellationToken ct = default);
}

/// <summary>
/// Bound to <see cref="SmtpOptions"/> via the configuration system.
/// </summary>
public class SmtpOptions
{
    public string Host { get; set; } = "smtp4dev";
    public int Port { get; set; } = 25;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@langartlms.com";
    public string FromName { get; set; } = "LangArt LMS";
    public bool UseSsl { get; set; } = false;
}
