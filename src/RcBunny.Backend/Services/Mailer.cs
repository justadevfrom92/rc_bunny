using System.Net;
using System.Net.Mail;

namespace RcBunny.Services;

/// <summary>
/// Sends an HTML email through SMTP (Integrations:Smtp:Host, Port, User, Password, From in appsettings or the environment).
/// Until a host is set, nothing reaches an inbox: emails are written to storage/mail-log instead, like Laravel's log mailer.
/// </summary>
public class Mailer(IConfiguration config, IWebHostEnvironment env)
{
    public bool Delivers => !string.IsNullOrWhiteSpace(config["Integrations:Smtp:Host"]);
    public string FromAddress => config["Integrations:Smtp:From"] ?? "hello@energytexas.example";
    public string FromName => config["Integrations:Smtp:FromName"] ?? "Energy Texas";

    public async Task SendAsync(string to, string? name, string subject, string html, Attachment? attachment = null)
    {
        if (!Delivers)
        {
            var dir = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage", "mail-log"));
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}"[..28]);
            await File.WriteAllTextAsync(file + ".html", $"<!-- To: {WebUtility.HtmlEncode(name ?? "")} <{to}> -->\n<!-- Subject: {WebUtility.HtmlEncode(subject)} -->\n"
                + (attachment != null ? $"<!-- Attached: {attachment.Name} -->\n" : "") + html);
            if (attachment != null) { await using var f = File.Create(file + "-" + attachment.Name); attachment.ContentStream.Position = 0; await attachment.ContentStream.CopyToAsync(f); }
            return;
        }
        using var client = new SmtpClient(config["Integrations:Smtp:Host"], int.TryParse(config["Integrations:Smtp:Port"], out var port) ? port : 587) { EnableSsl = config["Integrations:Smtp:Ssl"] != "false" };
        if (config["Integrations:Smtp:User"] is { Length: > 0 } user) client.Credentials = new NetworkCredential(user, config["Integrations:Smtp:Password"]);
        using var message = new MailMessage(new MailAddress(FromAddress, FromName), new MailAddress(to, name)) { Subject = subject, Body = html, IsBodyHtml = true };
        if (attachment != null) { attachment.ContentStream.Position = 0; message.Attachments.Add(attachment); }
        await client.SendMailAsync(message);
    }
}
