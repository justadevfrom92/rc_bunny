using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Services;

namespace RcBunny.Controllers;

/// <summary>
/// Rodeo → Emails (ebunny's EmailController, EmailPartController, TemplateTestController and the template screens of
/// MarketingController): how emails are doing by template, every email sent to customers, the templates and their test
/// sends, headers and footers, template categories and the suppression list.
/// </summary>
[ApiController]
[Route("api/rodeo")]
[RequirePermission("rodeo")]
public class RodeoEmailsController(AppDbContext db, IConfiguration config, Mailer mailer) : ControllerBase
{
    private string Website => config["Website:Url"] ?? "http://localhost:5100";
    private async Task<User> Me() => (await db.Users.FindAsync((await Permissions.CurrentAsync(HttpContext))!.Id))!;
    private IQueryable<ContactLog> Emails => db.ContactLogs.Where(l => l.Channel == "Email");

    public static readonly Dictionary<string, string> Stages = new()
    {
        ["queued"] = "Queued", ["sent"] = "Delivered", ["opened"] = "Opened", ["clicked"] = "Clicked", ["dropped"] = "Dropped", ["not sent"] = "Not sent", ["suppressed"] = "Suppressed",
    };

    private static string Stage(ContactLog l) => l.DroppedAt != null ? "dropped" : l.ClickedAt != null ? "clicked" : l.OpenedAt != null ? "opened" : l.SentAt != null ? "sent" : l.Status;

    private static object Row(ContactLog l) => new
    {
        l.Id, l.Template, l.CreatedAt, l.SentAt, l.OpenedAt, l.ClickedAt, stage = Stage(l),
        customer = l.Customer == null ? null : new { l.Customer.Id, l.Customer.Account, l.Customer.Name, l.Customer.Email },
    };

    // ---- Overview -----------------------------------------------------------------------------------------------

    [HttpGet("emails")]
    public async Task<object> Overview(int days = 30)
    {
        if (days is not (7 or 30 or 90 or 365)) days = 30;
        var since = DateTime.Today.AddDays(-(days - 1));
        var logs = await Emails.Where(l => l.CreatedAt >= since.ToUniversalTime())
            .Select(l => new { l.Template, l.CreatedAt, l.SentAt, l.OpenedAt, l.ClickedAt, l.DroppedAt, l.Status }).ToListAsync();
        var templates = await db.EmailTemplates.Include(t => t.Category).ToDictionaryAsync(t => t.Name);
        var rows = logs.GroupBy(l => l.Template).Select(g => new
        {
            template = g.Key, total = g.Count(), delivered = g.Count(l => l.SentAt != null), opened = g.Count(l => l.OpenedAt != null), clicked = g.Count(l => l.ClickedAt != null),
            dropped = g.Count(l => l.DroppedAt != null), unsent = g.Count(l => l.Status is "not sent" or "suppressed"), lastAt = g.Max(l => l.CreatedAt),
            category = templates.GetValueOrDefault(g.Key)?.Category is { } c ? new { c.Name, c.Color } : null, isTemplate = templates.ContainsKey(g.Key),
        }).OrderByDescending(r => r.total).ToList();
        double Rate(int n, int of) => of == 0 ? 0 : Math.Round(100.0 * n / of, 1);
        int total = rows.Sum(r => r.total), delivered = rows.Sum(r => r.delivered);

        // Emails per day (7 or 30 days), per week (90) or per month (365)
        var perDay = logs.GroupBy(l => l.CreatedAt.ToLocalTime().Date).ToDictionary(g => g.Key, g => g.Count());
        var chart = new List<object>();
        if (days > 90)
            for (var m = new DateTime(since.Year, since.Month, 1); m <= DateTime.Today; m = m.AddMonths(1))
                chart.Add(new { label = m.ToString("MMM"), tip = m.ToString("MMMM yyyy"), value = perDay.Where(kv => kv.Key.Year == m.Year && kv.Key.Month == m.Month).Sum(kv => kv.Value) });
        else
        {
            var step = days <= 30 ? 1 : 7;
            for (var d = since; d <= DateTime.Today; d = d.AddDays(step))
                chart.Add(new { label = step == 1 ? d.Day.ToString() : $"{d.Month}/{d.Day}", tip = step == 1 ? d.ToString("MMM d") : $"Week of {d:MMM d}", value = Enumerable.Range(0, step).Sum(i => perDay.GetValueOrDefault(d.AddDays(i))) });
        }
        var categories = await db.EmailCategories.OrderBy(c => c.Position).ToListAsync();
        var perCategory = await db.EmailTemplates.GroupBy(t => t.EmailCategoryId).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key ?? 0, x => x.n);
        return new
        {
            days, since = since.ToString("yyyy-MM-dd"), rows,
            stats = new { total, delivered, open = Rate(rows.Sum(r => r.opened), delivered), click = Rate(rows.Sum(r => r.clicked), delivered), drop = Rate(rows.Sum(r => r.dropped), total), unsent = rows.Sum(r => r.unsent) },
            chart, suppressed = await db.EmailSuppressions.CountAsync(),
            recent = (await Emails.Include(l => l.Customer).OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).Take(8).ToListAsync()).Select(Row),
            byCategory = categories.Select(c => new
            {
                c.Name, c.Color, templates = perCategory.GetValueOrDefault(c.Id),
                emails = rows.Where(r => templates.GetValueOrDefault(r.template)?.EmailCategoryId == c.Id).Sum(r => r.total),
            }),
        };
    }

    // ---- Sent emails --------------------------------------------------------------------------------------------

    [HttpGet("emails/sent")]
    public async Task<object> Sent(string? account, string? email, string? template, string? stage, int? campaign, string? user, DateOnly? start, DateOnly? end, int page = 1)
    {
        var q = Emails.Include(l => l.Customer).AsQueryable();
        if (!string.IsNullOrWhiteSpace(account)) q = q.Where(l => l.Customer!.Account.StartsWith(account.Trim()));
        if (!string.IsNullOrWhiteSpace(email)) q = q.Where(l => l.Customer!.Email!.Contains(email.Trim()));
        if (!string.IsNullOrEmpty(template)) q = q.Where(l => l.Template == template);
        if (campaign != null) q = q.Where(l => l.CampaignId == campaign);
        if (user == "system") q = q.Where(l => l.UserId == null);
        else if (int.TryParse(user, out var uid)) q = q.Where(l => l.UserId == uid);
        if (start is { } s) q = q.Where(l => l.CreatedAt >= s.ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        if (end is { } e) q = q.Where(l => l.CreatedAt < e.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        q = stage switch
        {
            null or "" => q,
            "dropped" => q.Where(l => l.DroppedAt != null),
            "clicked" => q.Where(l => l.ClickedAt != null && l.DroppedAt == null),
            "opened" => q.Where(l => l.OpenedAt != null && l.ClickedAt == null && l.DroppedAt == null),
            "sent" => q.Where(l => l.SentAt != null && l.OpenedAt == null && l.DroppedAt == null),
            _ => q.Where(l => l.SentAt == null && l.Status == stage),
        };
        var campaigns = await db.Campaigns.ToDictionaryAsync(c => c.Id, c => c.Name);
        var users = await db.Users.ToDictionaryAsync(u => u.Id, u => u.Name);
        var userIds = await Emails.Where(l => l.UserId != null).Select(l => l.UserId!.Value).Distinct().ToListAsync();
        return new
        {
            total = await q.CountAsync(), page,
            emails = (await q.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).Skip((Math.Max(page, 1) - 1) * 50).Take(50).ToListAsync()).Select(l => new
            {
                l.Id, l.Template, at = l.SentAt ?? l.CreatedAt, l.OpenedAt, l.ClickedAt, stage = Stage(l),
                customer = l.Customer == null ? null : new { l.Customer.Id, l.Customer.Account, l.Customer.Name, l.Customer.Email },
                by = l.CampaignId is { } cid ? $"Campaign: {campaigns.GetValueOrDefault(cid)}" : l.UserId is { } u ? users.GetValueOrDefault(u) : "System",
            }),
            templateNames = await Emails.Select(l => l.Template).Distinct().OrderBy(t => t).ToListAsync(),
            campaigns = await db.Campaigns.Where(c => c.Channel == "Email").OrderByDescending(c => c.Id).Select(c => new { c.Id, c.Name }).ToListAsync(),
            users = users.Where(u => userIds.Contains(u.Key)).OrderBy(u => u.Value).Select(u => new { id = u.Key, name = u.Value }),
            stages = Stages,
        };
    }

    /// <summary>The header and footer a template uses: its own picks, or the defaults.</summary>
    private async Task<(EmailPart?, EmailPart?)> PartsAsync(EmailTemplate t) => (
        (t.EmailHeaderId is { } h ? await db.EmailParts.FindAsync(h) : null) ?? await db.EmailParts.FirstOrDefaultAsync(p => p.Kind == "header" && p.IsDefault),
        (t.EmailFooterId is { } f ? await db.EmailParts.FindAsync(f) : null) ?? await db.EmailParts.FirstOrDefaultAsync(p => p.Kind == "footer" && p.IsDefault));

    private async Task<string> RenderAsync(EmailTemplate t, Customer c) { var (h, f) = await PartsAsync(t); return Rodeo.Render(t, h, f, c, c.Plan?.Name, Website); }
    private string Subject(EmailTemplate t, Customer c) => WebUtility.HtmlDecode(Rodeo.FillIns(t.Subject, c, c.Plan?.Name, Website));

    [HttpGet("emails/sent/{id:int}")]
    public async Task<IActionResult> Email(int id)
    {
        var e = await Emails.Include(l => l.Customer).ThenInclude(c => c!.Plan).FirstOrDefaultAsync(l => l.Id == id);
        if (e == null) return NotFound();
        var c = e.Customer;
        var name = Regex.Replace(e.Template, "^Test: ", "");
        var t = await db.EmailTemplates.FirstOrDefaultAsync(x => x.Name == name);
        // What the customer got: the stored body, or else the template filled in for them
        var html = e.Body is { Length: > 0 } b ? (b.Contains('<') ? b : WebUtility.HtmlEncode(b).Replace("\n", "<br>\n")) : t != null && c != null ? await RenderAsync(t, c) : null;
        var suppression = c?.Email is { } addr ? await db.EmailSuppressions.FirstOrDefaultAsync(s => s.Email == addr.ToLower()) : null;
        var campaign = e.CampaignId is { } cid ? await db.Campaigns.FindAsync(cid) : null;
        return Ok(new
        {
            email = new { e.Id, e.Template, e.Status, e.CreatedAt, e.SentAt, e.OpenedAt, e.ClickedAt, e.DroppedAt, stage = Stage(e) },
            customer = c == null ? null : new { c.Id, c.Account, c.Name, c.Email },
            template = t == null ? null : new { t.Id, t.Name }, subject = t != null && c != null ? Subject(t, c) : null, html,
            suppression = suppression == null ? null : new { suppression.Email, reason = Rodeo.SuppressionReasons.GetValueOrDefault(suppression.Reason, suppression.Reason), suppression.CreatedAt },
            sentBy = campaign != null ? null : e.UserId is { } u ? (await db.Users.FindAsync(u))?.Name : "System", campaign = campaign == null ? null : new { campaign.Id, campaign.Name },
            others = (await Emails.Where(l => l.CustomerId == e.CustomerId && l.Id != id && e.CustomerId != null).OrderByDescending(l => l.Id).Take(10).ToListAsync()).Select(Row),
        });
    }

    // ---- Templates ----------------------------------------------------------------------------------------------

    [HttpGet("templates")]
    public async Task<object> Templates(string? category)
    {
        var q = db.EmailTemplates.Include(t => t.Category).AsQueryable();
        if (category == "none") q = q.Where(t => t.EmailCategoryId == null);
        else if (int.TryParse(category, out var cid)) q = q.Where(t => t.EmailCategoryId == cid);
        var sent = await Emails.GroupBy(l => l.Template).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var campaigns = await db.Campaigns.Where(c => c.EmailTemplateId != null).GroupBy(c => c.EmailTemplateId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var tests = await db.EmailTestSends.GroupBy(t => t.EmailTemplateId).Select(g => new { g.Key, n = g.Count(), last = g.Max(t => t.CreatedAt) }).ToDictionaryAsync(x => x.Key);
        return new
        {
            templates = (await q.OrderBy(t => t.Name).ToListAsync()).Select(t => new
            {
                t.Id, t.Name, t.Subject, t.UpdatedAt, category = t.Category == null ? null : new { t.Category.Name, t.Category.Color },
                sent = sent.GetValueOrDefault(t.Name), campaigns = campaigns.GetValueOrDefault(t.Id), tests = tests.GetValueOrDefault(t.Id)?.n ?? 0, lastTest = tests.GetValueOrDefault(t.Id)?.last,
            }),
            categories = await db.EmailCategories.OrderBy(c => c.Position).ThenBy(c => c.Name).Select(c => new { c.Id, c.Name, c.Color }).ToListAsync(),
        };
    }

    private async Task<object> TemplateOptions() => new
    {
        categories = await db.EmailCategories.OrderBy(c => c.Position).ThenBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(),
        parts = await db.EmailParts.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name).Select(p => new { p.Id, p.Kind, p.Name, p.IsDefault, p.Html }).ToListAsync(),
    };

    [HttpGet("templates/new")]
    public async Task<object> NewTemplate(int? category) => new
    {
        template = new { name = "", subject = "", body = "<p>Hi {{first_name}},</p>", emailCategoryId = category, emailHeaderId = (int?)null, emailFooterId = (int?)null },
        options = await TemplateOptions(),
    };

    [HttpGet("templates/{id:int}")]
    public async Task<IActionResult> Template(int id)
    {
        if (await db.EmailTemplates.FindAsync(id) is not { } t) return NotFound();
        var sample = await db.Customers.OrderBy(c => c.Id).Select(c => new { c.FirstName, c.Account }).FirstOrDefaultAsync();
        return Ok(new { template = new { t.Id, t.Name, t.Subject, t.Body, t.EmailCategoryId, t.EmailHeaderId, t.EmailFooterId }, options = await TemplateOptions(), sample });
    }

    public record TemplateInput(string? Name, string? Subject, string? Body, int? EmailCategoryId, int? EmailHeaderId, int? EmailFooterId);

    [HttpPost("templates")]
    public Task<IActionResult> AddTemplate(TemplateInput i) => SaveTemplate(null, i);

    [HttpPut("templates/{id:int}")]
    public Task<IActionResult> UpdateTemplate(int id, TemplateInput i) => SaveTemplate(id, i);

    private async Task<IActionResult> SaveTemplate(int? id, TemplateInput i)
    {
        var t = id == null ? new EmailTemplate() : await db.EmailTemplates.FindAsync(id);
        if (t == null) return NotFound();
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 100) return BadRequest(new { error = "Give the template a name (up to 100 characters)." });
        if (await db.EmailTemplates.AnyAsync(x => x.Name == i.Name.Trim() && x.Id != t.Id)) return BadRequest(new { error = "Another template has that name." });
        if (string.IsNullOrWhiteSpace(i.Subject) || i.Subject.Length > 150) return BadRequest(new { error = "Write a subject (up to 150 characters)." });
        if (string.IsNullOrWhiteSpace(i.Body)) return BadRequest(new { error = "Write the email." });
        if (i.EmailHeaderId != null && !await db.EmailParts.AnyAsync(p => p.Id == i.EmailHeaderId && p.Kind == "header")) return BadRequest(new { error = "Pick a header." });
        if (i.EmailFooterId != null && !await db.EmailParts.AnyAsync(p => p.Id == i.EmailFooterId && p.Kind == "footer")) return BadRequest(new { error = "Pick a footer." });
        (t.Name, t.Subject, t.Body, t.EmailCategoryId, t.EmailHeaderId, t.EmailFooterId, t.UpdatedAt) = (i.Name.Trim(), i.Subject.Trim(), i.Body, i.EmailCategoryId, i.EmailHeaderId, i.EmailFooterId, DateTime.UtcNow);
        if (id == null) db.EmailTemplates.Add(t);
        await db.SaveChangesAsync();
        return Ok(new { t.Id, message = "Template saved" });
    }

    // ---- Template tests -----------------------------------------------------------------------------------------

    /// <summary>A test never goes to more people than this.</summary>
    public const int MaxTest = 25;
    public static readonly Dictionary<string, string> Modes = new()
    {
        ["me"] = "Just me", ["emails"] = "Email addresses", ["accounts"] = "Account numbers", ["bookmarks"] = "My bookmarks", ["team"] = "An admin team", ["category"] = "Customer category",
    };

    [HttpGet("templates/{id:int}/test")]
    public async Task<IActionResult> TestPage(int id, string? result, int page = 1)
    {
        if (await db.EmailTemplates.FindAsync(id) is not { } t) return NotFound();
        var me = await Me();
        var tests = db.EmailTestSends.Include(x => x.User).Where(x => x.EmailTemplateId == id && (result == null || x.Status == result));
        var users = await db.Users.Where(u => u.Active).GroupBy(u => u.RoleId).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        return Ok(new
        {
            template = new { t.Id, t.Name }, me = me.Email, max = MaxTest, delivers = mailer.Delivers, from = new { name = mailer.FromName, address = mailer.FromAddress },
            sample = await db.Customers.OrderBy(c => c.Id).Select(c => new { c.Account, c.Name }).FirstOrDefaultAsync(),
            roles = (await db.Roles.OrderBy(r => r.Name).ToListAsync()).Select(r => new { r.Id, r.Name, users = users.GetValueOrDefault(r.Id) }),
            bookmarks = await db.Bookmarks.Where(b => b.UserId == me.Id && b.Customer!.Email != null).Select(b => new { b.Customer!.Id, b.Customer.Name, b.Customer.Account, b.Customer.Email }).ToListAsync(),
            markets = await db.Markets.OrderBy(m => m.Name).Select(m => new { m.Id, m.Name }).ToListAsync(), statuses = Corral.Statuses.Select(s => s.Status),
            counts = await db.EmailTestSends.Where(x => x.EmailTemplateId == id).GroupBy(x => x.Status).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n),
            total = await tests.CountAsync(), page, modes = Modes,
            tests = (await tests.OrderByDescending(x => x.Id).Skip((Math.Max(page, 1) - 1) * 15).Take(15).ToListAsync()).Select(x => new
            {
                x.Id, x.CreatedAt, by = x.User?.Name, x.Mode, x.Target, x.Subject, x.Note, x.Status, again = x.Params != null,
                recipients = JsonSerializer.Deserialize<JsonElement>(x.Recipients),
            }),
        });
    }

    public record TestInput(string? Mode, string? Emails, string? Accounts, int? RoleId, List<string>? Status, string? Type, int? MarketId, int? Limit, string? Sample, string? Prefix, string? Note, bool? LogContact);

    private record Recipient(string Email, string? Name, Customer Customer, bool IsCustomer);

    private async Task<Customer> SampleAsync(string? account) =>
        await db.Customers.Include(c => c.Plan).FirstOrDefaultAsync(c => c.Account == account) ?? await db.Customers.Include(c => c.Plan).OrderBy(c => c.Id).FirstAsync();

    /// <summary>Who a test goes to (and what the "sent to" says), or an error.</summary>
    private async Task<(List<Recipient>? List, string? Target, string? Error)> RecipientsAsync(TestInput i)
    {
        if (i.Mode == null || !Modes.ContainsKey(i.Mode)) return (null, null, "Pick who gets it.");
        if (i.Sample is { Length: > 0 } sa && !await db.Customers.AnyAsync(c => c.Account == sa)) return (null, null, "No account with that number for the fill-ins.");
        var sample = await SampleAsync(i.Sample);
        var me = await Me();
        List<string> Split(string? s) => Regex.Split(s ?? "", @"[\s,;]+").Where(x => x != "").Distinct().ToList();
        Recipient Person(string email, string? name) => new(email, name, sample, false);
        Recipient Cust(Customer c) => new(c.Email!, c.Name, c, true);
        switch (i.Mode)
        {
            case "me": return ([Person(me.Email, me.Name)], me.Email, null);
            case "emails":
                var emails = Split(i.Emails);
                if (emails.Count == 0) return (null, null, "Type at least one email address.");
                var bad = emails.Where(e => !Regex.IsMatch(e, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")).ToList();
                if (bad.Count > 0) return (null, null, "Not an email address: " + string.Join(", ", bad));
                return (emails.Select(e => Person(e, null)).ToList(), string.Join(", ", emails), null);
            case "accounts":
                var accounts = Split(i.Accounts);
                if (accounts.Count == 0) return (null, null, "Type at least one account number.");
                var found = await db.Customers.Include(c => c.Plan).Where(c => accounts.Contains(c.Account)).ToListAsync();
                var missing = accounts.Except(found.Select(c => c.Account)).ToList();
                if (missing.Count > 0) return (null, null, "No account " + string.Join(", ", missing));
                return (found.Where(c => !string.IsNullOrEmpty(c.Email)).Select(Cust).ToList(), string.Join(", ", accounts), null);
            case "bookmarks":
                var marked = await db.Bookmarks.Where(b => b.UserId == me.Id && b.Customer!.Email != null).Select(b => b.Customer!).Include(c => c.Plan).ToListAsync();
                return (marked.Select(Cust).ToList(), $"Bookmarks of {me.Name}", null);
            case "team":
                if (i.RoleId == null || await db.Roles.FindAsync(i.RoleId) is not { } role) return (null, null, "Pick a team.");
                return ((await db.Users.Where(u => u.RoleId == role.Id && u.Active).OrderBy(u => u.Name).ToListAsync()).Select(u => Person(u.Email, u.Name)).ToList(), $"{role.Name} team", null);
            default:
                if (i.Limit is < 1 or > MaxTest) return (null, null, $"How many is 1 to {MaxTest}.");
                var statuses = (i.Status ?? []).Where(s => s != "").ToList();
                var q = db.Customers.Include(c => c.Plan).Where(c => c.Email != null);
                if (statuses.Count > 0) q = q.Where(c => statuses.Contains(c.Status));
                if (i.Type is { Length: > 0 } type) q = q.Where(c => c.Type == type);
                if (i.MarketId != null) q = q.Where(c => c.MarketId == i.MarketId);
                var market = i.MarketId != null ? (await db.Markets.FindAsync(i.MarketId))?.Name : null;
                var target = string.Join(" · ", new[] { statuses.Count > 0 ? string.Join(", ", statuses) : "Any status", i.Type is { Length: > 0 } ? i.Type : null, market }.Where(x => x != null));
                // Same people for Check Recipients and Send: a stable order, not random
                return ((await q.OrderBy(c => c.Id).Take(i.Limit ?? 5).ToListAsync()).Select(Cust).ToList(), target, null);
        }
    }

    /// <summary>"Check recipients": who a send would go to, without sending.</summary>
    [HttpPost("templates/{id:int}/test/recipients")]
    public async Task<IActionResult> TestRecipients(int id, TestInput i)
    {
        if (!await db.EmailTemplates.AnyAsync(t => t.Id == id)) return NotFound();
        var (list, target, error) = await RecipientsAsync(i);
        if (error != null) return UnprocessableEntity(new { error });
        return Ok(new
        {
            target, count = list!.Count, max = MaxTest,
            recipients = list.Take(100).Select(r => new { name = r.Name, email = r.Email, account = r.IsCustomer ? r.Customer.Account : null, fills = r.IsCustomer ? "their account" : $"sample {r.Customer.Account}" }),
        });
    }

    /// <summary>Subject and HTML exactly as sent: fill-ins for c, the test prefix, and a banner saying who sent the test.</summary>
    private async Task<(string Subject, string Html)> ComposeAsync(EmailTemplate t, Customer c, string prefix, string? note, User sender)
    {
        var banner = "<div style=\"background:#fff3d6;color:#8a5a00;padding:10px 14px;font:14px/1.4 Arial,sans-serif;border-bottom:1px solid #f0d58a\">"
            + $"<b>Test email</b> sent by {WebUtility.HtmlEncode(sender.Name)} from Rodeo{(string.IsNullOrWhiteSpace(note) ? "." : ": " + WebUtility.HtmlEncode(note))}</div>";
        return ($"{prefix} {Subject(t, c)}".Trim(), banner + await RenderAsync(t, c));
    }

    public record PreviewInput(string? Sample, string? Prefix, string? Note);

    /// <summary>The email as one account would get it, for the preview pane.</summary>
    [HttpPost("templates/{id:int}/test/preview")]
    public async Task<IActionResult> TestPreview(int id, PreviewInput i)
    {
        if (await db.EmailTemplates.FindAsync(id) is not { } t) return NotFound();
        var c = await SampleAsync(i.Sample);
        var (subject, html) = await ComposeAsync(t, c, i.Prefix ?? "[TEST]", i.Note, await Me());
        return Ok(new { subject, html, customer = new { c.Name, c.Account, found = string.IsNullOrEmpty(i.Sample) || c.Account == i.Sample } });
    }

    [HttpPost("templates/{id:int}/test")]
    public async Task<IActionResult> SendTest(int id, TestInput i) => await db.EmailTemplates.FindAsync(id) is { } t ? await PerformAsync(t, i) : NotFound();

    /// <summary>Send a past test again to the same people with the same options.</summary>
    [HttpPost("templates/{id:int}/test/{send:int}/again")]
    public async Task<IActionResult> Again(int id, int send)
    {
        var s = await db.EmailTestSends.FirstOrDefaultAsync(x => x.Id == send && x.EmailTemplateId == id);
        if (s?.Params == null || JsonSerializer.Deserialize<TestInput>(s.Params) is not { } p) return NotFound();
        return await PerformAsync((await db.EmailTemplates.FindAsync(id))!, p);
    }

    private async Task<IActionResult> PerformAsync(EmailTemplate t, TestInput i)
    {
        var (list, target, error) = await RecipientsAsync(i);
        if (error != null) return UnprocessableEntity(new { error });
        if (list!.Count == 0) return UnprocessableEntity(new { error = "Nobody with an email address matches that." });
        if (list.Count > MaxTest) return UnprocessableEntity(new { error = $"{list.Count} people match. A test goes to {MaxTest} at most; narrow it down." });
        var me = await Me();
        var prefix = i.Prefix ?? "[TEST]";
        var suppressed = (await db.EmailSuppressions.Select(s => s.Email).ToListAsync()).ToHashSet();
        var log = new List<Dictionary<string, string?>>();
        foreach (var r in list)
        {
            var entry = new Dictionary<string, string?> { ["email"] = r.Email, ["name"] = r.Name, ["account"] = r.IsCustomer ? r.Customer.Account : null };
            if (suppressed.Contains(r.Email.Trim().ToLowerInvariant())) { (entry["status"], entry["error"]) = ("suppressed", "On the suppression list"); log.Add(entry); continue; }
            var (subject, body) = await ComposeAsync(t, r.Customer, prefix, i.Note, me);
            try { await mailer.SendAsync(r.Email, r.Name, subject, body); entry["status"] = mailer.Delivers ? "sent" : "logged"; }
            catch (Exception e) { (entry["status"], entry["error"]) = ("failed", e.Message.Length > 200 ? e.Message[..200] : e.Message); }
            // Customers see the test on their account's Contact Log
            if (r.IsCustomer && i.LogContact != false)
                db.ContactLogs.Add(new ContactLog
                {
                    CustomerId = r.Customer.Id, Channel = "Email", Template = $"Test: {t.Name}", Body = body, Status = entry["status"] == "sent" ? "sent" : "not sent",
                    SentAt = entry["status"] == "sent" ? DateTime.UtcNow : null, UserId = me.Id, Who = me.Name,
                });
            if (!r.IsCustomer) entry["fills"] = r.Customer.Account;
            log.Add(entry);
        }
        var statuses = log.Select(l => l["status"]).Distinct().ToList();
        var overall = statuses.Count == 1 ? statuses[0]! : "partial";
        var send = new EmailTestSend
        {
            EmailTemplateId = t.Id, UserId = me.Id, Mode = i.Mode!, Target = target, Subject = $"{prefix} {t.Subject}", Note = i.Note, Status = overall,
            Recipients = JsonSerializer.Serialize(log.Select(l => l.Where(kv => kv.Value != null).ToDictionary())),
            Params = JsonSerializer.Serialize(i),
        };
        db.EmailTestSends.Add(send);
        await db.SaveChangesAsync();
        var n = log.Count;
        var people = n == 1 ? "recipient" : "recipients";
        var message = overall switch
        {
            "sent" => $"Test sent to {n} {people}.",
            "logged" => $"Test written to the mail log for {n} {people} — not delivered, because no SMTP host is set (Integrations:Smtp:Host). Set it with its Smtp settings to deliver.",
            "suppressed" => "Nothing sent: every recipient is on the suppression list (Rodeo → Emails → Suppression List).",
            "failed" => $"The test could not be sent: {log[0].GetValueOrDefault("error") ?? "mail error"}. Check the Smtp settings.",
            _ => "Test sent to some recipients; see Test History for the ones that failed.",
        };
        return Ok(new { ok = overall == "sent", message });
    }

    // ---- Headers & footers --------------------------------------------------------------------------------------

    [HttpGet("headers-footers")]
    public async Task<object> Parts()
    {
        var parts = await db.EmailParts.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name).ToListAsync();
        var headerUses = await db.EmailTemplates.Where(t => t.EmailHeaderId != null).GroupBy(t => t.EmailHeaderId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var footerUses = await db.EmailTemplates.Where(t => t.EmailFooterId != null).GroupBy(t => t.EmailFooterId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var usingDefault = new { header = await db.EmailTemplates.CountAsync(t => t.EmailHeaderId == null), footer = await db.EmailTemplates.CountAsync(t => t.EmailFooterId == null) };
        return Rodeo.PartKinds.ToDictionary(k => k.Key, k => parts.Where(p => p.Kind == k.Key).Select(p => new
        {
            p.Id, p.Name, p.IsDefault, p.UpdatedAt, empty = p.Html.Trim() == "",
            templates = (k.Key == "header" ? headerUses : footerUses).GetValueOrDefault(p.Id) + (p.IsDefault ? (k.Key == "header" ? usingDefault.header : usingDefault.footer) : 0),
        }));
    }

    [HttpGet("headers-footers/new")]
    public object NewPart(string? kind) => new { part = new { kind = kind is "header" or "footer" ? kind : "footer", name = "", html = "", isDefault = false }, templates = Array.Empty<object>() };

    [HttpGet("headers-footers/{id:int}")]
    public async Task<IActionResult> Part(int id)
    {
        if (await db.EmailParts.FindAsync(id) is not { } p) return NotFound();
        var q = p.Kind == "header" ? db.EmailTemplates.Where(t => t.EmailHeaderId == p.Id || (p.IsDefault && t.EmailHeaderId == null))
            : db.EmailTemplates.Where(t => t.EmailFooterId == p.Id || (p.IsDefault && t.EmailFooterId == null));
        return Ok(new
        {
            part = new { p.Id, p.Kind, p.Name, p.Html, p.IsDefault },
            templates = (await q.OrderBy(t => t.Name).ToListAsync()).Select(t => new { t.Id, t.Name, picked = (p.Kind == "header" ? t.EmailHeaderId : t.EmailFooterId) != null }),
        });
    }

    public record PartInput(string? Kind, string? Name, string? Html, bool IsDefault);

    [HttpPost("headers-footers")]
    public Task<IActionResult> AddPart(PartInput i) => SavePart(null, i);

    [HttpPut("headers-footers/{id:int}")]
    public Task<IActionResult> UpdatePart(int id, PartInput i) => SavePart(id, i);

    private async Task<IActionResult> SavePart(int? id, PartInput i)
    {
        var p = id == null ? new EmailPart { Kind = i.Kind ?? "" } : await db.EmailParts.FindAsync(id);
        if (p == null) return NotFound();
        if (!Rodeo.PartKinds.ContainsKey(p.Kind)) return BadRequest(new { error = "A header or a footer?" });
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 100) return BadRequest(new { error = "Give it a name (up to 100 characters)." });
        if (await db.EmailParts.AnyAsync(x => x.Kind == p.Kind && x.Name == i.Name.Trim() && x.Id != p.Id)) return BadRequest(new { error = $"Another {p.Kind} has that name." });
        (p.Name, p.Html, p.UpdatedAt) = (i.Name.Trim(), i.Html ?? "", DateTime.UtcNow);
        if (id == null) db.EmailParts.Add(p);
        await db.SaveChangesAsync();
        if (i.IsDefault && !p.IsDefault)
        {
            await db.EmailParts.Where(x => x.Kind == p.Kind && x.Id != p.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false));
            p.IsDefault = true;
            await db.SaveChangesAsync();
        }
        return Ok(new { p.Id, message = $"{Rodeo.PartKinds[p.Kind]} saved" });
    }

    /// <summary>Templates using it go back to the default. The default itself can't be deleted.</summary>
    [HttpDelete("headers-footers/{id:int}")]
    public async Task<IActionResult> DeletePart(int id)
    {
        if (await db.EmailParts.FindAsync(id) is not { } p) return NotFound();
        if (p.IsDefault) return UnprocessableEntity(new { error = "Pick another default first." });
        if (p.Kind == "header") await db.EmailTemplates.Where(t => t.EmailHeaderId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.EmailHeaderId, (int?)null));
        else await db.EmailTemplates.Where(t => t.EmailFooterId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.EmailFooterId, (int?)null));
        db.EmailParts.Remove(p);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{p.Name} deleted. Its templates use the default {p.Kind} now." });
    }

    // ---- Categories ---------------------------------------------------------------------------------------------

    [HttpGet("emails/categories")]
    public async Task<object> Categories()
    {
        var templates = await db.EmailTemplates.OrderBy(t => t.Name).Select(t => new { t.Id, t.Name, t.EmailCategoryId }).ToListAsync();
        return new
        {
            categories = (await db.EmailCategories.OrderBy(c => c.Position).ThenBy(c => c.Name).ToListAsync())
                .Select(c => new { c.Id, c.Name, c.Description, c.Color, c.Position, templates = templates.Where(t => t.EmailCategoryId == c.Id) }),
            uncategorized = templates.Where(t => t.EmailCategoryId == null),
        };
    }

    [HttpGet("emails/categories/form")]
    public async Task<IActionResult> CategoryForm(int? id)
    {
        var c = id == null ? new EmailCategory() : await db.EmailCategories.FindAsync(id);
        if (c == null) return NotFound();
        var cats = await db.EmailCategories.ToDictionaryAsync(x => x.Id, x => x.Name);
        return Ok(new
        {
            category = new { id = id, c.Name, c.Description, c.Color, position = id == null ? (int?)null : c.Position },
            templates = (await db.EmailTemplates.OrderBy(t => t.Name).ToListAsync()).Select(t => new { t.Id, t.Name, t.EmailCategoryId, category = t.EmailCategoryId is { } x ? cats.GetValueOrDefault(x) : null }),
        });
    }

    public record CategoryInput(string? Name, string? Description, string? Color, int? Position, List<int>? Templates);

    [HttpPost("emails/categories")]
    public Task<IActionResult> AddCategory(CategoryInput i) => SaveCategory(null, i);

    [HttpPut("emails/categories/{id:int}")]
    public Task<IActionResult> UpdateCategory(int id, CategoryInput i) => SaveCategory(id, i);

    private async Task<IActionResult> SaveCategory(int? id, CategoryInput i)
    {
        var c = id == null ? new EmailCategory() : await db.EmailCategories.FindAsync(id);
        if (c == null) return NotFound();
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 60) return BadRequest(new { error = "Give the category a name (up to 60 characters)." });
        if (await db.EmailCategories.AnyAsync(x => x.Name == i.Name.Trim() && x.Id != c.Id)) return BadRequest(new { error = "Another category has that name." });
        if (i.Color == null || !Regex.IsMatch(i.Color, "^#[0-9a-fA-F]{6}$")) return BadRequest(new { error = "Pick a color." });
        if (i.Position is < 0 or > 999) return BadRequest(new { error = "The order is 0 to 999." });
        (c.Name, c.Description, c.Color) = (i.Name.Trim(), string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(), i.Color);
        c.Position = i.Position ?? (id == null ? (await db.EmailCategories.MaxAsync(x => (int?)x.Position) ?? 0) + 1 : c.Position);
        if (id == null) db.EmailCategories.Add(c);
        await db.SaveChangesAsync();
        // The ticked templates are filed here; ones unticked here become uncategorized
        var ids = i.Templates ?? [];
        foreach (var t in await db.EmailTemplates.Where(t => t.EmailCategoryId == c.Id || ids.Contains(t.Id)).ToListAsync())
            t.EmailCategoryId = ids.Contains(t.Id) ? c.Id : null;
        await db.SaveChangesAsync();
        return Ok(new { message = "Category saved" });
    }

    [HttpDelete("emails/categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        if (await db.EmailCategories.FindAsync(id) is not { } c) return NotFound();
        await db.EmailTemplates.Where(t => t.EmailCategoryId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.EmailCategoryId, (int?)null));
        db.EmailCategories.Remove(c);
        await db.SaveChangesAsync();
        return Ok(new { message = "Category deleted; its templates are now uncategorized" });
    }

    // ---- Suppression list ---------------------------------------------------------------------------------------

    [HttpGet("emails/suppressions")]
    public async Task<object> Suppressions(string? q, string? reason, int page = 1)
    {
        var list = db.EmailSuppressions.Include(s => s.Customer).Include(s => s.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) list = list.Where(s => s.Email.Contains(q.Trim().ToLower()) || s.Customer!.Account.StartsWith(q.Trim()));
        if (!string.IsNullOrEmpty(reason)) list = list.Where(s => s.Reason == reason);
        return new
        {
            total = await list.CountAsync(), page, reasons = Rodeo.SuppressionReasons, filtered = !string.IsNullOrWhiteSpace(q) || !string.IsNullOrEmpty(reason),
            list = (await list.OrderByDescending(s => s.Id).Skip((Math.Max(page, 1) - 1) * 50).Take(50).ToListAsync()).Select(s => new
            {
                s.Id, s.Email, s.Reason, reasonLabel = Rodeo.SuppressionReasons.GetValueOrDefault(s.Reason, s.Reason), s.Note, s.CreatedAt, by = s.User?.Name,
                customer = s.Customer == null ? null : new { s.Customer.Account, s.Customer.Name },
            }),
        };
    }

    public record SuppressionInput(string? Email, string? Reason, string? Note);

    [HttpPost("emails/suppressions")]
    public async Task<IActionResult> AddSuppression(SuppressionInput i)
    {
        var email = (i.Email ?? "").Trim().ToLowerInvariant();
        if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$") || email.Length > 150) return BadRequest(new { error = "Type an email address." });
        if (i.Reason == null || !Rodeo.SuppressionReasons.ContainsKey(i.Reason)) return BadRequest(new { error = "Pick a reason." });
        if (await db.EmailSuppressions.AnyAsync(s => s.Email == email)) return BadRequest(new { error = "That address is already on the suppression list." });
        db.EmailSuppressions.Add(new EmailSuppression
        {
            Email = email, Reason = i.Reason, Note = string.IsNullOrWhiteSpace(i.Note) ? null : i.Note.Trim(), UserId = (await Me()).Id,
            CustomerId = await db.Customers.Where(c => c.Email != null && c.Email.ToLower() == email).Select(c => (int?)c.Id).FirstOrDefaultAsync(),
        });
        await db.SaveChangesAsync();
        return Ok(new { message = $"{email} added; no email will go to it." });
    }

    [HttpDelete("emails/suppressions/{id:int}")]
    public async Task<IActionResult> DeleteSuppression(int id)
    {
        if (await db.EmailSuppressions.FindAsync(id) is not { } s) return NotFound();
        db.EmailSuppressions.Remove(s);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{s.Email} removed; emails can go to it again." });
    }
}
