using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// Ranger → Web Messages: what people sent from the website's Contact Us page. Each message has its own page with
/// the account it matches, a reply box (the reply is emailed and logged on the account), and Mark Handled / Reopen.
/// </summary>
[ApiController]
[Route("api/messages")]
[RequirePermission("ranger")]
public class MessagesController(AppDbContext db, IConfiguration config) : ControllerBase
{
    [HttpGet]
    public async Task<object> List(string? topic)
    {
        topic = topic != null && ContactMessage.Topics.ContainsKey(topic) ? topic : null;
        var q = db.ContactMessages.Where(m => topic == null || m.Topic == topic);
        return new
        {
            topic,
            topics = ContactMessage.Topics,
            counts = await db.ContactMessages.Where(m => m.HandledAt == null).GroupBy(m => m.Topic).Select(g => new { topic = g.Key, n = g.Count() }).ToDictionaryAsync(x => x.topic, x => x.n),
            open = await Rows(q.Where(m => m.HandledAt == null).OrderBy(m => m.CreatedAt)),
            handled = await Rows(q.Where(m => m.HandledAt != null).OrderByDescending(m => m.HandledAt).Take(25)),
        };
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var m = await db.ContactMessages.Include(x => x.HandledBy).Include(x => x.RepliedBy).Include(x => x.ResolvedBy).FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        var c = await Match(m);
        var email = m.Email.ToLower();
        var others = await db.ContactMessages.Where(x => x.Id != id && (x.Email.ToLower() == email || (c != null && x.CustomerId == c.Id)))
            .OrderByDescending(x => x.CreatedAt).Take(5).ToListAsync();
        return Ok(new
        {
            message = new
            {
                m.Id, m.Name, m.Email, m.Phone, m.Topic, topicLabel = m.TopicLabel, m.Message, m.Ip, m.CreatedAt, m.HandledAt, handledBy = m.HandledBy?.Name,
                m.Reply, m.RepliedAt, repliedBy = m.RepliedBy?.Name, m.Resolution, resolvedBy = m.ResolvedBy?.Name, m.ResolvedAt,
            },
            customer = c == null ? null : new
            {
                c.Id, c.Account, c.Name, c.Status, c.Balance, plan = await db.Plans.Where(p => p.Id == c.PlanId).Select(p => p.Name).FirstOrDefaultAsync(),
                byEmail = c.Email != null && c.Email.ToLower() == email,
            },
            others = others.Select(x => new { x.Id, topicLabel = x.TopicLabel, x.CreatedAt, x.HandledAt }),
        });
    }

    public record ReplyInput(string? Reply, bool Handled);

    /// <summary>Reply by email (logged on the matching account), and optionally mark it handled.</summary>
    [HttpPost("{id:int}/reply")]
    public async Task<IActionResult> Reply(int id, ReplyInput input)
    {
        var m = await db.ContactMessages.FindAsync(id);
        if (m is null) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Reply)) return BadRequest(new { error = "Write the reply first." });
        var me = (await Permissions.CurrentAsync(HttpContext))!;
        var c = await Match(m);
        (m.Reply, m.RepliedAt, m.RepliedById, m.CustomerId) = (input.Reply.Trim(), DateTime.UtcNow, me.Id, c?.Id);
        if (input.Handled) (m.HandledAt, m.HandledById) = (DateTime.UtcNow, me.Id);
        var ready = config.GetSection("Integrations:Salesforce").GetChildren().Any(s => !string.IsNullOrEmpty(s.Value));
        if (c != null)
            db.ContactLogs.Add(new ContactLog { CustomerId = c.Id, Channel = "Email", Template = "Web Message Reply", Body = m.Reply, Status = ready ? "queued" : "not sent", Who = me.Name });
        await db.SaveChangesAsync();
        return Ok(new { message = "Reply saved" + (ready ? $" and queued to {m.Email}." : " (email is not set up, so it was logged but not sent).") });
    }

    /// <summary>Mark handled, or reopen.</summary>
    [HttpPost("{id:int}/handled")]
    public async Task<IActionResult> Handled(int id)
    {
        var m = await db.ContactMessages.FindAsync(id);
        if (m is null) return NotFound();
        var me = (await Permissions.CurrentAsync(HttpContext))!;
        (m.HandledAt, m.HandledById) = m.HandledAt == null ? ((DateTime?)DateTime.UtcNow, (int?)me.Id) : (null, null);
        await db.SaveChangesAsync();
        return Ok(new { message = m.HandledAt != null ? "Marked as handled" : "Reopened" });
    }

    /// <summary>The account this message is probably about: the one it was matched to, else the same email, else the same phone number.</summary>
    private async Task<Customer?> Match(ContactMessage m)
    {
        if (m.CustomerId != null) return await db.Customers.FindAsync(m.CustomerId);
        var email = m.Email.ToLower();
        var byEmail = await db.Customers.FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == email);
        if (byEmail != null) return byEmail;
        var digits = new string((m.Phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 10) return null;
        var last10 = "%" + digits[^10..];
        return await db.Customers.FirstOrDefaultAsync(c => c.Phone != null && EF.Functions.Like(c.Phone.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", ""), last10));
    }

    private static async Task<List<Row>> Rows(IQueryable<ContactMessage> q) =>
        (await q.Include(m => m.HandledBy).ToListAsync())
            .Select(m => new Row(m.Id, m.Name, m.Email, m.Phone, m.TopicLabel, m.Message, m.CreatedAt, m.HandledAt, m.HandledBy?.Name, m.RepliedAt, m.Resolution)).ToList();

    public record Row(int Id, string Name, string Email, string? Phone, string Topic, string Message, DateTime CreatedAt, DateTime? HandledAt, string? HandledBy, DateTime? RepliedAt, string? Resolution);
}
