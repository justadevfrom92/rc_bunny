using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Frontend.Services;

public sealed record CmsPage(Page Page, string? Primary, string? Secondary, string? Parent, string? Auxiliary, IReadOnlyDictionary<string, List<string>> Zones, string? Template);

/// <summary>
/// Pages built in Lando: the page's content blocks, its zone components, and modules. Blocks can include other blocks
/// with [[block|slug|name=value]] and fill {{name|default}} from those values; [[zip_form]] is the plan finder.
/// Rendered pages are cached for a minute, like the other public pages.
/// </summary>
public sealed partial class Cms(IServiceScopeFactory scopes, IMemoryCache cache)
{
    [GeneratedRegex(@"\[\[block\|([a-z0-9-]+)((?:\|[^\]|]+)*)\]\]")]
    private static partial Regex Include();
    [GeneratedRegex(@"\{\{\s*([a-z_]+)(?:\|([^}]*))?\s*\}\}")]
    private static partial Regex Var();

    public async Task<CmsPage?> FindAsync(string path) => await cache.GetOrCreateAsync($"cms:{path}", async e =>
    {
        e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var page = await db.Pages.Include(p => p.Template).FirstOrDefaultAsync(p => p.Path == path && p.Status != "Draft");
        if (page is null) return null;
        var blocks = await db.ContentBlocks.ToDictionaryAsync(b => b.Id);
        var bySlug = blocks.Values.Where(b => b.Slug != null).GroupBy(b => b.Slug!).ToDictionary(g => g.Key, g => g.First());
        string? R(int? id) => id is { } i && blocks.TryGetValue(i, out var b) ? Render(b.Html, bySlug, null, 0) : null;
        var zones = (await db.PageComponents.Where(c => c.PageId == page.Id).OrderBy(c => c.Position).ToListAsync())
            .GroupBy(c => c.Zone).ToDictionary(g => g.Key, g => g.Select(c => blocks.TryGetValue(c.ContentBlockId, out var b)
                ? Render(b.Html, bySlug, c.Vars?.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? ""), 0) : "").ToList());
        return new CmsPage(page, R(page.ContentPrimaryId), R(page.ContentSecondaryId), R(page.ContentParentId), R(page.ContentAuxiliaryId), zones, page.Template?.Name);
    });

    public static string Render(string html, IReadOnlyDictionary<string, ContentBlock> bySlug, IDictionary<string, string>? vars, int depth)
    {
        if (depth > 5) return html;
        html = Include().Replace(html, m =>
        {
            if (!bySlug.TryGetValue(m.Groups[1].Value, out var inner)) return "";
            var v = m.Groups[2].Value.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
            return Render(inner.Html, bySlug, v, depth + 1);
        });
        html = Var().Replace(html, m => vars != null && vars.TryGetValue(m.Groups[1].Value, out var val) ? WebUtility.HtmlEncode(val) : WebUtility.HtmlEncode(m.Groups[2].Value));
        return html.Replace("[[zip_form]]", "<form class=\"zip-card\" action=\"/plans\" method=\"get\" style=\"max-width:420px\"><div class=\"field\"><label for=\"cms-zip\">Your zip code</label><input id=\"cms-zip\" name=\"zip\" inputmode=\"numeric\" maxlength=\"5\" pattern=\"\\d{5}\"></div><button class=\"btn btn-block\">See Plans</button></form>");
    }
}

/// <summary>A new account from the website sign-up (ebunny's Enrollment service).</summary>
public static class Enrollment
{
    public static async Task<int?> MarketForZipAsync(AppDbContext db, string zip) =>
        int.TryParse(zip, out var z) ? await db.ZipRanges.Where(r => z >= r.ZipFrom && z <= r.ZipTo).Select(r => (int?)r.MarketId).FirstOrDefaultAsync() : null;

    public static async Task<Customer> CreateAsync(AppDbContext db, SignupData d, string? ip)
    {
        var max = (await db.Customers.Select(c => c.Account).ToListAsync()).Select(a => long.TryParse(a, out var n) ? n : 0).DefaultIfEmpty(1219000000).Max();
        var acct = (max + 1).ToString();
        // Credit check (Experian when its keys are set): a lower score means a deposit
        var score = Random.Shared.Next(520, 830);
        var deposit = score < 600 ? 200m : score < 650 ? 100m : 0m;
        var plan = await db.Plans.FirstAsync(p => p.Id == d.PlanId);
        var c = new Customer
        {
            Account = acct, Ticket = "17651" + DateTime.UtcNow.Ticks.ToString()[^14..], Name = d.Biz && !string.IsNullOrWhiteSpace(d.BusinessName) ? d.BusinessName! : $"{d.FirstName} {d.LastName}",
            FirstName = d.FirstName, LastName = d.LastName, Type = d.Biz ? "Small Business" : "Residential", Email = d.Email?.ToLowerInvariant(), Phone = d.Phone, PhoneType = d.PhoneType,
            Language = d.Language ?? "English", Address = d.Address!, Unit = d.Unit, City = d.City!, Zip = d.Zip!, MarketId = d.MarketId, Esiid = string.IsNullOrWhiteSpace(d.Esiid) ? null : d.Esiid,
            PlanId = plan.Id, Status = deposit > 0 ? "Pending - Deposit Due" : "Submitted", Exception = string.IsNullOrWhiteSpace(d.Esiid) ? null : null, Source = "Website",
            EnrollmentType = d.EnrollmentType == "Move-In" ? "MoveIn" : "Switch", MoveSwitch = d.EnrollmentType == "Move-In" ? "move" : "switch", RequestedStart = d.StartDate, StartDate = d.StartDate,
            TecScore = score, DepositDue = deposit, SsnLast4 = d.SsnLast4, MarketingOptIn = d.MarketingOptIn, Autopay = d.Autopay, Paperless = d.Paperless, PeakPerks = d.PeakPerks,
            Ip = ip, Msid = d.Msid ?? "1001 - Organic", Channel = "1 - Organic (default)", PromoCode = d.PromoCode, ReferredBy = d.ReferredBy, ReferralCode = "REF" + acct[^5..],
            RateClass = d.Biz ? "Commercial" : "Residential", BillingStreet = d.Address, BillingCity = d.City, BillingState = "TX", BillingZip = d.Zip,
            Username = string.IsNullOrWhiteSpace(d.Username) ? null : d.Username.Trim().ToLowerInvariant(), Password = string.IsNullOrEmpty(d.PasswordHash) ? null : d.PasswordHash,
        };
        db.Customers.Add(c);
        c.Notes.Add(new Note { Author = "System", Category = "Order", Action = "New Enrollment", Body = $"Enrollment submitted via Website: {plan.Name}." });
        c.Notes.Add(new Note { Author = "System", Body = deposit > 0 ? $"Credit check complete. Deposit of ${deposit:N0} required." : "Credit check complete. No deposit required." });
        db.QueueLogs.Add(new QueueLog { Customer = c, Queue = "QueueCartFulfilledFlowing", EnteredAt = DateTime.UtcNow, Note = "Website order" });
        if (c.Esiid is null) db.WorkItems.Add(new WorkItem { Customer = c, Queue = "unprocessed-orders", Summary = "Website order without an ESIID" });
        foreach (var p in new[] { ("AutoPay", d.Autopay), ("Paperless Billing", d.Paperless), ("Peak Perks", d.PeakPerks) }.Where(x => x.Item2))
            db.CustomerProducts.Add(new CustomerProduct { Customer = c, Product = p.Item1 });
        db.ContactLogs.Add(new ContactLog { Customer = c, Channel = "Email", Template = "Welcome Letter", Status = "logged", SentAt = DateTime.UtcNow, Direction = "out" });
        await db.SaveChangesAsync();
        return c;
    }
}

/// <summary>What the sign-up has collected so far (saved on the signups row, so it survives "save and finish later").</summary>
public sealed class SignupData
{
    public bool Biz { get; set; }
    public string? Zip { get; set; }
    public string? Address { get; set; }
    public string? Unit { get; set; }
    public string? City { get; set; }
    public string? Esiid { get; set; }
    public string EnrollmentType { get; set; } = "Switch";
    public DateOnly? StartDate { get; set; }
    public int? MarketId { get; set; }
    public int? PlanId { get; set; }
    public string? BusinessName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhoneType { get; set; }
    public string? Language { get; set; }
    public string? SsnLast4 { get; set; }
    public bool MarketingOptIn { get; set; } = true;
    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
    public bool Autopay { get; set; } = true;
    public bool Paperless { get; set; } = true;
    public bool PeakPerks { get; set; }
    public string? Msid { get; set; }
    public string? PromoCode { get; set; }
    public string? ReferredBy { get; set; }
}
