using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Services;

namespace RcBunny.Controllers;

/// <summary>
/// Rodeo: marketing, the same screens as ebunny's Rodeo. This part: the dashboard, email and text campaigns (builder,
/// live audience count, send now or on a schedule, results) and click heat maps. Emails, PDFs and surveys are in the
/// RodeoEmails, RodeoPdfs and RodeoSurveys controllers.
/// </summary>
[ApiController]
[Route("api/rodeo")]
[RequirePermission("rodeo")]
public class RodeoController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private string Website => config["Website:Url"] ?? "http://localhost:5100";
    private async Task<Permissions.SignedIn> Me() => (await Permissions.CurrentAsync(HttpContext))!;

    // ---- Dashboard ----------------------------------------------------------------------------------------------

    [HttpGet("dashboard")]
    public async Task<object> Dashboard()
    {
        var sent = db.ContactLogs.Where(l => l.CampaignId != null);
        var emails = await sent.CountAsync(l => l.Channel == "Email");
        var since90 = DateTime.UtcNow.Date.AddDays(-90);
        var since30 = DateTime.UtcNow.AddDays(-30);
        var msids = await db.Msids.ToDictionaryAsync(m => m.Code, m => m.Name);
        var surveys = await Surveys.SummariesAsync(db);
        return new
        {
            optedIn = await db.Customers.CountAsync(c => c.MarketingOptIn), customers = await db.Customers.CountAsync(),
            campaigns = await db.Campaigns.CountAsync(c => c.Status == "sent"), messages = await sent.CountAsync(),
            openRate = emails == 0 ? 0 : (int)Math.Round(100.0 * await sent.CountAsync(l => l.Channel == "Email" && l.OpenedAt != null) / emails),
            clickRate = emails == 0 ? 0 : (int)Math.Round(100.0 * await sent.CountAsync(l => l.Channel == "Email" && l.ClickedAt != null) / emails),
            responses = await db.SurveyResponses.CountAsync(), responses30 = await db.SurveyResponses.CountAsync(r => r.CreatedAt >= since30),
            byChannel = (await db.Customers.Where(c => c.CreatedAt >= since90).GroupBy(c => c.Msid).Select(g => new { msid = g.Key, n = g.Count() }).ToListAsync())
                .OrderByDescending(x => x.n).Select(x => new { x.msid, name = x.msid != null && msids.TryGetValue(x.msid, out var n) ? n : "Unknown MSID", x.n }),
            recent = await db.Campaigns.OrderByDescending(c => c.CreatedAt).Take(5).Select(c => new { c.Id, c.Name, c.Channel, c.Status }).ToListAsync(),
            surveys = surveys.OrderByDescending(s => s.LastAt).Take(10),
            latestResponses = await Surveys.LatestAsync(db, 10),
        };
    }

    // ---- Campaigns ----------------------------------------------------------------------------------------------

    /// <summary>Campaigns in three tables: Scheduled (next first), Drafts, Sent (with results).</summary>
    [HttpGet("campaigns")]
    public async Task<object> Campaigns()
    {
        var all = await db.Campaigns.Include(c => c.Template).OrderByDescending(c => c.CreatedAt).ToListAsync();
        var counts = await db.ContactLogs.Where(l => l.CampaignId != null).GroupBy(l => l.CampaignId!.Value)
            .Select(g => new { g.Key, n = g.Count(), opened = g.Count(l => l.OpenedAt != null), clicked = g.Count(l => l.ClickedAt != null) }).ToDictionaryAsync(x => x.Key);
        var since30 = DateTime.UtcNow.AddDays(-30);
        async Task<object> Row(Campaign c) => new
        {
            c.Id, c.Name, c.Description, goal = c.Goal, goalLabel = c.GoalLabel, c.Product, c.Channel, template = c.Template?.Name, c.ScheduledAt, c.SentAt, c.UpdatedAt,
            audience = c.Status == "sent" ? 0 : await Services.Campaigns.AudienceQuery(db, c).CountAsync(),
            messages = counts.GetValueOrDefault(c.Id)?.n ?? 0, opened = counts.GetValueOrDefault(c.Id)?.opened ?? 0, clicked = counts.GetValueOrDefault(c.Id)?.clicked ?? 0,
            converted = c.Status == "sent" ? await Services.Campaigns.Converted(db, c).CountAsync() : 0,
        };
        var rows = new Dictionary<int, object>();
        foreach (var c in all) rows[c.Id] = await Row(c);
        return new
        {
            scheduled = all.Where(c => c.Status == "scheduled").OrderBy(c => c.ScheduledAt).Select(c => rows[c.Id]),
            drafts = all.Where(c => c.Status == "draft").Select(c => rows[c.Id]),
            sent = all.Where(c => c.Status == "sent").OrderByDescending(c => c.SentAt).Select(c => rows[c.Id]),
            sent30 = all.Where(c => c.Status == "sent" && c.SentAt >= since30).Sum(c => counts.GetValueOrDefault(c.Id)?.n ?? 0),
            window = Rodeo.ConversionWindow,
        };
    }

    private async Task<object> Options() => new
    {
        goals = Rodeo.Goals.Select(g => new { key = g.Key, g.Value.Label, g.Value.Converts }), products = Corral.Products, statuses = Corral.Statuses.Select(s => s.Status),
        markets = await db.Markets.OrderBy(m => m.Short).Select(m => new { m.Id, m.Short }).ToListAsync(),
        templates = await db.EmailTemplates.Include(t => t.Category).OrderBy(t => t.Name).Select(t => new { t.Id, t.Name, t.Subject, category = t.Category!.Name }).ToListAsync(),
        channels = await db.Msids.Where(m => m.Active).OrderBy(m => m.Code).Select(m => new { msid = m.Code, m.Name }).ToListAsync(),
        promos = (await db.PromoCodes.OrderBy(p => p.Code).ToListAsync()).Where(Live).Select(p => new { p.Code, p.Credit }),
    };

    private static bool Live(PromoCode p)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return p.Active && (p.StartsOn == null || p.StartsOn <= today) && (p.EndsOn == null || p.EndsOn >= today);
    }

    [HttpGet("campaigns/new")]
    public async Task<object> NewCampaign() => new
    {
        campaign = new { name = "", goal = "renewal", channel = "Email", capDays = 7, audience = JsonSerializer.Deserialize<JsonElement>("""{"statuses":["Good - On Flow"]}"""), status = "draft" },
        options = await Options(),
    };

    /// <summary>A campaign's page: the builder while it's a draft or scheduled; results once sent.</summary>
    [HttpGet("campaigns/{id:int}")]
    public async Task<IActionResult> Campaign(int id, int page = 1)
    {
        if (await db.Campaigns.Include(c => c.Template).FirstOrDefaultAsync(c => c.Id == id) is not { } c) return NotFound();
        var a = Services.Campaigns.Read(c.Audience);
        var row = new
        {
            c.Id, c.Name, c.Goal, goalLabel = c.GoalLabel, c.Description, c.Product, c.Channel, c.EmailTemplateId, template = c.Template?.Name, c.Message, c.Msid, c.PromoCode,
            c.CapDays, audience = JsonSerializer.Deserialize<JsonElement>(c.Audience), c.Status, c.ScheduledAt, c.SentAt, c.Skipped,
        };
        if (c.Status == "sent")
        {
            var messages = db.ContactLogs.Include(l => l.Customer).Where(l => l.CampaignId == id);
            var converted = await Services.Campaigns.Converted(db, c).Select(x => x.Id).ToListAsync();
            var opens = await messages.Where(l => l.OpenedAt != null).Select(l => l.OpenedAt!.Value).ToListAsync();
            var markets = a.MarketIds is { Count: > 0 } ids ? await db.Markets.Where(m => ids.Contains(m.Id)).Select(m => m.Short).ToListAsync() : [];
            return Ok(new
            {
                campaign = row, results = await Services.Campaigns.ResultsAsync(db, c), converts = Rodeo.Goals.GetValueOrDefault(c.Goal).Converts, window = Rodeo.ConversionWindow,
                daily = opens.GroupBy(o => o.ToLocalTime().Date).OrderBy(g => g.Key).Select(g => new { day = g.Key, n = g.Count() }), markets,
                total = await messages.CountAsync(), page,
                recipients = (await messages.OrderByDescending(l => l.Id).Skip((Math.Max(page, 1) - 1) * 50).Take(50).ToListAsync()).Select(l => new
                {
                    l.Id, customer = l.Customer == null ? null : new { l.Customer.Id, l.Customer.Account, l.Customer.Name }, status = l.DroppedAt != null ? "bounced" : l.Status,
                    l.OpenedAt, l.ClickedAt, converted = l.CustomerId != null && converted.Contains(l.CustomerId.Value),
                }),
            });
        }
        var sample = await Services.Campaigns.AudienceQuery(db, c).Include(x => x.Plan).OrderBy(x => x.Id).FirstOrDefaultAsync();
        object? preview = null;
        if (sample != null)
        {
            if (c.Channel == "Email" && c.Template != null)
            {
                var (h, f) = await PartsAsync(c.Template);
                preview = new { subject = WebUtilityDecode(Rodeo.FillIns(c.Template.Subject, sample, sample.Plan?.Name, Website)), html = Rodeo.Render(c.Template, h, f, sample, sample.Plan?.Name, Website) };
            }
            else if (c.Channel == "SMS") preview = new { text = Services.Campaigns.TextFor(c, sample) };
        }
        return Ok(new
        {
            campaign = row, options = await Options(), count = await Services.Campaigns.AudienceQuery(db, c).CountAsync(),
            excluded = await Services.Campaigns.ExclusionsAsync(db, c.Channel, a, c.CapDays), sample = sample?.FirstName, preview,
        });
    }

    private static string WebUtilityDecode(string s) => System.Net.WebUtility.HtmlDecode(s);

    /// <summary>The header and footer a template uses: its own picks, or the defaults.</summary>
    private async Task<(EmailPart?, EmailPart?)> PartsAsync(EmailTemplate t) => (
        (t.EmailHeaderId is { } h ? await db.EmailParts.FindAsync(h) : null) ?? await db.EmailParts.FirstOrDefaultAsync(p => p.Kind == "header" && p.IsDefault),
        (t.EmailFooterId is { } f ? await db.EmailParts.FindAsync(f) : null) ?? await db.EmailParts.FirstOrDefaultAsync(p => p.Kind == "footer" && p.IsDefault));

    public record CampaignInput(string? Name, string? Goal, string? Description, string? Product, string? Channel, int? EmailTemplateId, string? Message,
        string? Msid, string? PromoCode, int? CapDays, Campaigns.Audience? Audience);

    private async Task<string?> Check(CampaignInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 120) return "Give the campaign a name (up to 120 characters).";
        if (i.Goal == null || !Rodeo.Goals.ContainsKey(i.Goal)) return "Pick a goal.";
        if (i.Goal == "product" && (i.Product == null || !Corral.Products.Contains(i.Product))) return "Pick the product this campaign promotes.";
        if (i.Channel is not ("Email" or "SMS")) return "Pick email or text.";
        if (i.Channel == "Email" && (i.EmailTemplateId == null || !await db.EmailTemplates.AnyAsync(t => t.Id == i.EmailTemplateId))) return "Pick an email template.";
        if (i.Channel == "SMS" && string.IsNullOrWhiteSpace(i.Message)) return "Write the text message.";
        if (i.Message is { Length: > 320 }) return "Keep the text to 320 characters.";
        if (!string.IsNullOrEmpty(i.Msid) && !await db.Msids.AnyAsync(m => m.Code == i.Msid)) return "That MSID doesn't exist.";
        if (!string.IsNullOrEmpty(i.PromoCode) && !await db.PromoCodes.AnyAsync(p => p.Code == i.PromoCode)) return "That promo code doesn't exist.";
        if (i.CapDays is not (>= 0 and <= 90)) return "The contact limit is 0 to 90 days.";
        if (i.Audience?.ContractEndsWithin is < 1 or > 365) return "Contract ends within is 1 to 365 days.";
        return null;
    }

    [HttpPost("campaigns")]
    public Task<IActionResult> AddCampaign(CampaignInput input) => SaveCampaign(null, input);

    [HttpPut("campaigns/{id:int}")]
    public Task<IActionResult> UpdateCampaign(int id, CampaignInput input) => SaveCampaign(id, input);

    private async Task<IActionResult> SaveCampaign(int? id, CampaignInput i)
    {
        var c = id == null ? new Campaign { CreatedById = (await Me()).Id } : await db.Campaigns.FindAsync(id);
        if (c == null) return NotFound();
        if (c.Status == "sent") return UnprocessableEntity(new { error = "A sent campaign can't be changed." });
        if (await Check(i) is { } error) return BadRequest(new { error });
        (c.Name, c.Goal, c.Description, c.Product, c.Channel) = (i.Name!.Trim(), i.Goal!, string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(), i.Goal == "product" ? i.Product : null, i.Channel!);
        (c.EmailTemplateId, c.Message, c.Msid, c.PromoCode, c.CapDays, c.UpdatedAt) = (i.EmailTemplateId, string.IsNullOrWhiteSpace(i.Message) ? null : i.Message, NullIfEmpty(i.Msid), NullIfEmpty(i.PromoCode), i.CapDays!.Value, DateTime.UtcNow);
        c.Audience = Services.Campaigns.Write(i.Audience ?? new());
        if (id == null) db.Campaigns.Add(c);
        await db.SaveChangesAsync();
        return Ok(new { c.Id, message = $"Campaign saved. {await Services.Campaigns.AudienceQuery(db, c).CountAsync():N0} customers match." });
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>Live audience count for the builder (no save): matches, exclusions and a few names.</summary>
    [HttpPost("campaigns/count")]
    public async Task<object> Count(CampaignInput i)
    {
        var a = i.Audience ?? new();
        var channel = i.Channel == "SMS" ? "SMS" : "Email";
        var q = Services.Campaigns.AudienceQuery(db, channel, a);
        return new
        {
            count = await q.CountAsync(), excluded = await Services.Campaigns.ExclusionsAsync(db, channel, a, i.CapDays ?? 0),
            sample = await q.OrderBy(x => x.Id).Take(5).Select(x => x.Name + " · " + x.Account).ToListAsync(),
        };
    }

    /// <summary>Send now (logged on each account; email and texting aren't connected yet).</summary>
    [HttpPost("campaigns/{id:int}/send")]
    public async Task<IActionResult> Send(int id)
    {
        if (await db.Campaigns.FindAsync(id) is not { } c) return NotFound();
        if (c.Status == "sent") return UnprocessableEntity(new { error = "This campaign was already sent." });
        var (sent, skipped) = await Services.Campaigns.SendAsync(db, c, Website);
        return Ok(new
        {
            message = $"{sent:N0} messages logged but not sent: {(c.Channel == "SMS" ? "Twilio" : "email")} is not connected yet."
                + (skipped > 0 ? $" {skipped} skipped (suppressed or contacted in the last {c.CapDays} days)." : ""),
        });
    }

    public record ScheduleInput(DateOnly? Date, string? Time);

    /// <summary>Send at a date and time: the campaigns job sends it then.</summary>
    [HttpPost("campaigns/{id:int}/schedule")]
    public async Task<IActionResult> Schedule(int id, ScheduleInput i)
    {
        if (await db.Campaigns.FindAsync(id) is not { } c) return NotFound();
        if (c.Status == "sent") return UnprocessableEntity(new { error = "This campaign was already sent." });
        if (i.Date == null || !TimeOnly.TryParse(i.Time, out var time)) return BadRequest(new { error = "Pick a date and time." });
        var at = i.Date.Value.ToDateTime(time, DateTimeKind.Local).ToUniversalTime();
        if (at <= DateTime.UtcNow.AddMinutes(5)) return BadRequest(new { error = "Pick a time at least a few minutes from now, or use Send Now." });
        (c.Status, c.ScheduledAt, c.UpdatedAt) = ("scheduled", at, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return Ok(new { message = $"Scheduled for {at.ToLocalTime():dddd, MMM d 'at' h:mm tt}." });
    }

    [HttpPost("campaigns/{id:int}/unschedule")]
    public async Task<IActionResult> Unschedule(int id)
    {
        if (await db.Campaigns.FindAsync(id) is not { Status: "scheduled" } c) return UnprocessableEntity(new { error = "This campaign isn't scheduled." });
        (c.Status, c.ScheduledAt, c.UpdatedAt) = ("draft", null, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return Ok(new { message = "Back to draft: it will not send until you schedule or send it." });
    }

    /// <summary>Start a new draft from any campaign (handy for resending a sent one to a new audience).</summary>
    [HttpPost("campaigns/{id:int}/duplicate")]
    public async Task<IActionResult> Duplicate(int id)
    {
        if (await db.Campaigns.FindAsync(id) is not { } c) return NotFound();
        var copy = new Campaign
        {
            Name = c.Name + " (copy)", Channel = c.Channel, Goal = c.Goal, Description = c.Description, Product = c.Product, EmailTemplateId = c.EmailTemplateId, Message = c.Message,
            Audience = c.Audience, Msid = c.Msid, PromoCode = c.PromoCode, CapDays = c.CapDays, CreatedById = (await Me()).Id,
        };
        db.Campaigns.Add(copy);
        await db.SaveChangesAsync();
        return Ok(new { copy.Id, message = "Copied. Change what you need, then send or schedule it." });
    }

    // ---- Click heat maps ----------------------------------------------------------------------------------------

    [HttpGet("templates/{id:int}/heatmap")]
    public async Task<IActionResult> TemplateHeatmap(int id)
    {
        if (await db.EmailTemplates.FindAsync(id) is not { } t) return NotFound();
        return Ok(await HeatmapAsync(t, db.LinkClicks.Where(l => l.EmailTemplateId == id), t.Name,
            db.ContactLogs.Where(l => l.Channel == "Email" && l.Template == t.Name), $"/rodeo/templates/{id}", null));
    }

    [HttpGet("campaigns/{id:int}/heatmap")]
    public async Task<IActionResult> CampaignHeatmap(int id)
    {
        if (await db.Campaigns.FindAsync(id) is not { } c) return NotFound();
        var t = c.EmailTemplateId is { } tid ? await db.EmailTemplates.FindAsync(tid) : null;
        return Ok(await HeatmapAsync(t, db.LinkClicks.Where(l => l.CampaignId == id), c.Name, db.ContactLogs.Where(l => l.CampaignId == id), $"/rodeo/campaigns/{id}", c.Channel == "SMS" ? c.Message : null));
    }

    private async Task<object> HeatmapAsync(EmailTemplate? t, IQueryable<LinkClick> clicks, string name, IQueryable<ContactLog> sent, string back, string? text)
    {
        var counts = await clicks.GroupBy(l => l.Link).Select(g => new { link = g.Key, n = g.Count(), people = g.Select(x => x.CustomerId).Distinct().Count() }).ToListAsync();
        var sample = await db.Customers.Include(c => c.Plan).Where(c => c.Email != null).OrderBy(c => c.Id).FirstOrDefaultAsync();
        List<(string Key, string Label, string Url)> links;
        string html;
        if (text != null)
        {
            links = [("body-1", "Link in the text", "")];
            html = "<div class=\"hm-email hm-sms\"><div class=\"hm-body\"><p>" + Regex.Replace(System.Net.WebUtility.HtmlEncode(text), @"(https?://\S+)", "<a data-link=\"body-1\" href=\"$1\">$1</a>") + "</p></div></div>";
        }
        else if (t != null)
        {
            var (h, f) = await PartsAsync(t);
            var sections = new[] { ("header", h?.Html ?? ""), ("body", t.Body), ("footer", f?.Html ?? "") };
            links = [.. sections.SelectMany(s => Rodeo.Links(s.Item1, s.Item2))];
            // The email as sent, each part filled in on its own so the link keys stay per part, with data-link="key" on each link
            html = "<div class=\"hm-email\">" + string.Concat(sections.Select(s =>
            {
                var i = 0;
                var filled = sample == null ? s.Item2 : Rodeo.FillIns(s.Item2, sample, sample.Plan?.Name, Website);
                return $"<div class=\"hm-{s.Item1}\">" + Regex.Replace(filled, @"<a\s", _ => $"<a data-link=\"{s.Item1}-{++i}\" ", RegexOptions.IgnoreCase) + "</div>";
            })) + "</div>";
        }
        else (links, html) = ([], "");
        var clickers = await clicks.Select(l => l.CustomerId).Distinct().CountAsync();
        return new
        {
            name, back, html, total = counts.Sum(c => c.n), max = Math.Max(1, counts.Select(c => c.n).DefaultIfEmpty().Max()),
            sent = await sent.CountAsync(), opened = await sent.CountAsync(l => l.OpenedAt != null), clickers,
            devices = await clicks.GroupBy(l => l.Device ?? "Unknown").Select(g => new { device = g.Key, n = g.Count() }).ToListAsync(),
            links = links.Select(l => new { key = l.Key, label = l.Label, url = l.Url, n = counts.FirstOrDefault(c => c.link == l.Key)?.n ?? 0, people = counts.FirstOrDefault(c => c.link == l.Key)?.people ?? 0 })
                .OrderByDescending(l => l.n),
        };
    }
}
