using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Services;

/// <summary>Rodeo campaigns (ebunny's Campaign model): who they go to, who's left out, sending, and the results funnel.</summary>
public static class Campaigns
{
    /// <summary>The audience filters, as saved on the campaign.</summary>
    public class Audience
    {
        [JsonPropertyName("statuses")] public List<string>? Statuses { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("market_ids")] public List<int>? MarketIds { get; set; }
        [JsonPropertyName("product")] public string? Product { get; set; }
        [JsonPropertyName("without_product")] public string? WithoutProduct { get; set; }
        [JsonPropertyName("autopay")] public string? Autopay { get; set; }
        [JsonPropertyName("balance")] public string? Balance { get; set; }
        [JsonPropertyName("contract_ends_within")] public int? ContractEndsWithin { get; set; }
    }

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    public static Audience Read(string? json) => string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<Audience>(json) ?? new();
    public static string Write(Audience a)
    {
        if (a.Statuses is { Count: 0 }) a.Statuses = null;
        if (a.MarketIds is { Count: 0 }) a.MarketIds = null;
        foreach (var p in new[] { nameof(a.Type), nameof(a.Product), nameof(a.WithoutProduct), nameof(a.Autopay), nameof(a.Balance) })
            if (typeof(Audience).GetProperty(p)!.GetValue(a) is "") typeof(Audience).GetProperty(p)!.SetValue(a, null);
        return JsonSerializer.Serialize(a, Json);
    }

    /// <summary>Customers the audience filters select. Marketing only goes to customers who opted in.</summary>
    public static IQueryable<Customer> AudienceQuery(AppDbContext db, string channel, Audience a, bool optedInOnly = true)
    {
        var q = db.Customers.AsQueryable();
        if (optedInOnly) q = q.Where(c => c.MarketingOptIn);
        if (channel == "SMS") q = q.Where(c => c.PhoneType == "Mobile");
        if (channel == "Email") q = q.Where(c => c.Email != null);
        if (a.Statuses is { Count: > 0 } s) q = q.Where(c => s.Contains(c.Status));
        if (a.Type is { Length: > 0 } t) q = q.Where(c => c.Type == t);
        if (a.MarketIds is { Count: > 0 } m) q = q.Where(c => c.MarketId != null && m.Contains(c.MarketId.Value));
        if (a.Autopay is "yes" or "no") { var on = a.Autopay == "yes"; q = q.Where(c => c.Autopay == on); }
        if (a.Balance == "owes") q = q.Where(c => c.Balance > 0);
        if (a.Balance == "paid") q = q.Where(c => c.Balance <= 0);
        if (a.Product is { Length: > 0 } p) q = q.Where(c => db.CustomerProducts.Any(x => x.CustomerId == c.Id && x.Product == p && x.RemovedAt == null));
        if (a.WithoutProduct is { Length: > 0 } w) q = q.Where(c => !db.CustomerProducts.Any(x => x.CustomerId == c.Id && x.Product == w && x.RemovedAt == null));
        if (a.ContractEndsWithin is { } days)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var until = today.AddDays(days);
            q = q.Where(c => db.PlanTerms.Any(x => x.CustomerId == c.Id && x.Status == "current" && x.ContractEnd >= today && x.ContractEnd <= until));
        }
        return q;
    }

    public static IQueryable<Customer> AudienceQuery(AppDbContext db, Campaign c, bool optedInOnly = true) => AudienceQuery(db, c.Channel, Read(c.Audience), optedInOnly);

    /// <summary>Who matches but is left out, and why: not opted in, on the suppression list, contacted recently.</summary>
    public static async Task<object> ExclusionsAsync(AppDbContext db, string channel, Audience a, int capDays)
    {
        var optedIn = AudienceQuery(db, channel, a);
        var since = DateTime.UtcNow.AddDays(-capDays);
        return new
        {
            notOptedIn = await AudienceQuery(db, channel, a, false).CountAsync() - await optedIn.CountAsync(),
            suppressed = channel == "Email" ? await optedIn.CountAsync(c => db.EmailSuppressions.Any(s => s.Email == c.Email!.ToLower())) : 0,
            recent = capDays > 0 ? await optedIn.CountAsync(c => db.ContactLogs.Any(l => l.CustomerId == c.Id && l.CampaignId != null && l.CreatedAt >= since)) : 0,
        };
    }

    /// <summary>The text message for one customer, with {{first_name}} and {{promo_code}} filled in.</summary>
    public static string TextFor(Campaign c, Customer cust) =>
        (c.Message ?? "").Replace("{{first_name}}", string.IsNullOrEmpty(cust.FirstName) ? "there" : cust.FirstName).Replace("{{promo_code}}", c.PromoCode ?? "");

    /// <summary>
    /// Sends to the audience: one message per customer (logged on the account; email and texting aren't connected yet).
    /// Skips suppressed addresses and anyone who got a campaign within cap_days. Returns (sent, skipped).
    /// </summary>
    public static async Task<(int Sent, int Skipped)> SendAsync(AppDbContext db, Campaign c, string websiteUrl)
    {
        var template = c.EmailTemplateId is { } id ? await db.EmailTemplates.FindAsync(id) : null;
        var since = DateTime.UtcNow.AddDays(-c.CapDays);
        var customers = await AudienceQuery(db, c).Include(x => x.Plan).ToListAsync();
        var recent = c.CapDays > 0 ? (await db.ContactLogs.Where(l => l.CampaignId != null && l.CreatedAt >= since).Select(l => l.CustomerId).Distinct().ToListAsync()).ToHashSet() : [];
        var suppressed = (await db.EmailSuppressions.Select(s => s.Email).ToListAsync()).ToHashSet();
        int sent = 0, skipped = 0;
        foreach (var cust in customers)
        {
            if (recent.Contains(cust.Id) || (c.Channel == "Email" && suppressed.Contains((cust.Email ?? "").Trim().ToLowerInvariant()))) { skipped++; continue; }
            db.ContactLogs.Add(new ContactLog
            {
                CustomerId = cust.Id, CampaignId = c.Id, Channel = c.Channel, Template = c.Channel == "Email" ? template?.Name ?? "" : $"Campaign: {c.Name}",
                Body = c.Channel == "Email" ? StripTags(Rodeo.FillIns(template?.Body ?? "", cust, cust.Plan?.Name, websiteUrl)) : TextFor(c, cust),
                Phone = c.Channel == "SMS" ? cust.Phone : null, Status = "not sent", Who = "System",
            });
            sent++;
        }
        (c.Status, c.SentAt, c.Recipients, c.Skipped, c.UpdatedAt) = ("sent", DateTime.UtcNow, sent, skipped, DateTime.UtcNow);
        AdminNotification.Notify(db, await AdminNotification.AppUsersAsync(db, "rodeo"), "campaign", $"Campaign sent: {c.Name}",
            $"{sent:N0} {(c.Channel == "SMS" ? "texts" : "emails")}, {skipped:N0} skipped", $"/admin/rodeo/campaigns/{c.Id}");
        await db.SaveChangesAsync();
        return (sent, skipped);
    }

    public static string StripTags(string html) => System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", "");

    /// <summary>Recipients who reached the goal within the window after the campaign was sent.</summary>
    public static IQueryable<Customer> Converted(AppDbContext db, Campaign c)
    {
        var ids = db.ContactLogs.Where(l => l.CampaignId == c.Id).Select(l => l.CustomerId);
        var from = c.SentAt ?? DateTime.UtcNow;
        var to = from.AddDays(Rodeo.ConversionWindow);
        var q = db.Customers.Where(x => ids.Contains(x.Id));
        return c.Goal switch
        {
            "product" => q.Where(x => db.CustomerProducts.Any(p => p.CustomerId == x.Id && p.Product == c.Product && p.CreatedAt >= from && p.CreatedAt <= to)),
            "renewal" or "winback" => q.Where(x => db.PlanTerms.Any(t => t.CustomerId == x.Id && t.OrderedAt >= from && t.OrderedAt <= to)),
            "rewards" => q.Where(x => db.StarEntries.Any(s => s.CustomerId == x.Id && s.Stars < 0 && s.CreatedAt >= from && s.CreatedAt <= to)),
            _ => q.Where(x => false),
        };
    }

    /// <summary>The funnel for a sent campaign: sent, delivered, opened, clicked (email), converted.</summary>
    public static async Task<object> ResultsAsync(AppDbContext db, Campaign c)
    {
        var m = db.ContactLogs.Where(l => l.CampaignId == c.Id);
        var email = c.Channel == "Email";
        return new
        {
            sent = await m.CountAsync(),
            delivered = await m.CountAsync(l => l.Status != "failed" && l.Status != "not sent" && l.Status != "suppressed" && l.DroppedAt == null),
            opened = email ? await m.CountAsync(l => l.OpenedAt != null) : (int?)null,
            clicked = email ? await m.CountAsync(l => l.ClickedAt != null) : (int?)null,
            converted = await Converted(db, c).CountAsync(),
        };
    }
}
