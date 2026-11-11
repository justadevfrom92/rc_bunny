using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Frontend.Services;

/// <summary>
/// Turns Lando content into website HTML, like ebunny's SiteContent: expands [[block|slug]] (or [[block|id=N]]) codes,
/// filling in the block's {{variables}} from the code ([[block|slug|city=Dallas]]) or the page component, the widgets
/// [[year]], [[phone]], [[zip_form]] and [[plans|group=…|limit=…]], each zone's components, and a page's price grid.
/// Content is written by signed-in Lando editors and is output as HTML.
/// </summary>
public class SiteContent(AppDbContext db)
{
    private static readonly Regex Code = new(@"\[\[block\|(?:id=(\d+)|([A-Za-z0-9_-]+))((?:\|[A-Za-z0-9_]+=[^|\]]*)*)\]\]");
    private static readonly Regex Var = new(@"\{\{\s*([a-z][a-z0-9_]*)\s*(?:\|([^}]*))?\}\}", RegexOptions.IgnoreCase);
    private static readonly Regex Widget = new(@"\[\[(year|phone|zip_form|plans)((?:\|[a-z_]+=[A-Za-z0-9_-]+)*)\]\]");

    /// <summary>Heading for [[zip_form]] on the page being rendered (Address Box Title).</summary>
    public string? AddressBoxTitle { get; set; }

    /// <summary>"|city=Dallas|utility=Oncor" → {city: Dallas, utility: Oncor}</summary>
    private static Dictionary<string, string> Params(string s) => s.Split('|', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => WebUtility.HtmlDecode(p.Length > 1 ? p[1].Trim() : ""));

    /// <summary>The block's HTML with its {{variables}} filled in (or their default text).</summary>
    public static string WithVars(string html, IReadOnlyDictionary<string, string> values) => Var.Replace(html, m =>
        WebUtility.HtmlEncode(values.TryGetValue(m.Groups[1].Value, out var v) && !string.IsNullOrEmpty(v) ? v : m.Groups[2].Value.Trim()));

    public async Task<string> ExpandAsync(string? html, Website? site, int depth = 0)
    {
        if (string.IsNullOrEmpty(html)) return "";
        html = Widgets(html, site);
        var outHtml = new System.Text.StringBuilder();
        var last = 0;
        foreach (Match m in Code.Matches(html))
        {
            outHtml.Append(html, last, m.Index - last);
            last = m.Index + m.Length;
            if (depth >= 3) continue; // a block that includes itself
            var q = db.ContentBlocks.Where(b => b.SiteId == null || b.SiteId == site!.Id);
            var block = m.Groups[1].Success ? await q.FirstOrDefaultAsync(b => b.Id == int.Parse(m.Groups[1].Value)) : await q.FirstOrDefaultAsync(b => b.Slug == m.Groups[2].Value);
            if (block != null) outHtml.Append(await ExpandAsync(WithVars(block.Html, Params(m.Groups[3].Value)), site, depth + 1));
        }
        outHtml.Append(html, last, html.Length - last);
        // Codes this site doesn't support (e.g. old checkout codes) are left out rather than shown
        return depth == 0 ? Regex.Replace(outHtml.ToString(), @"\[\[[a-z_]+(\|[^\]]*)?\]\]", "") : outHtml.ToString();
    }

    private string Widgets(string html, Website? site) => Widget.Replace(html, m =>
    {
        var opt = Params(m.Groups[2].Value);
        return m.Groups[1].Value switch
        {
            "year" => DateTime.Today.Year.ToString(),
            "phone" => WebUtility.HtmlEncode(site?.Phone ?? Site.Phone),
            "zip_form" => $$"""
                <form class="zip-card" data-zip-form novalidate>
                  <h2>{{WebUtility.HtmlEncode(AddressBoxTitle ?? "Find your plan")}}</h2>
                  <p>Enter your zip code to see rates available at your address.</p>
                  <div class="toggle-group" role="group" aria-label="Service type">
                    <button type="button" class="on" data-type="home">Home</button>
                    <button type="button" data-type="business">Business</button>
                  </div>
                  <div class="field">
                    <label>Service Zip Code <input name="zip" inputmode="numeric" maxlength="5" placeholder="e.g. 77001" autocomplete="postal-code"></label>
                  </div>
                  <button class="btn btn-block" type="submit">View Plans</button>
                  <div class="form-msg" role="status"></div>
                </form>
                """,
            _ => $"<div class=\"plan-grid\" id=\"plan-grid\" data-group=\"{WebUtility.HtmlEncode(opt.GetValueOrDefault("group", "resi"))}\"" +
                 (opt.TryGetValue("limit", out var l) && int.TryParse(l, out var n) ? $" data-limit=\"{n}\"" : "") + "></div>",
        };
    });

    /// <summary>Zone → the HTML of its components, in order.</summary>
    public async Task<Dictionary<string, string>> ZonesAsync(Page page)
    {
        var site = await db.Sites.FindAsync(page.SiteId);
        var zones = Cms.Zones.Keys.ToDictionary(z => z, _ => "");
        foreach (var c in await db.PageComponents.Include(c => c.Block).Where(c => c.PageId == page.Id).OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync())
        {
            var b = c.Block;
            if (b == null || (b.SiteId != null && b.SiteId != page.SiteId) || !zones.ContainsKey(c.Zone)) continue;
            var vars = string.IsNullOrEmpty(c.Vars) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(c.Vars)!;
            zones[c.Zone] += $"<div class=\"cms-block\" data-block=\"{WebUtility.HtmlEncode(b.Slug)}\">{await ExpandAsync(WithVars(b.Html, vars), site)}</div>";
        }
        return zones;
    }

    public record GridRow(Plan Plan, string Page, decimal Price, Dictionary<int, decimal> Avg);
    public record Grid(string Type, string? Header, Market? Market, string Formula, List<GridRow> Rows);

    /// <summary>The page's price grid: its plan group priced in its default market with its rating formula (¢/kWh, TDSP included).</summary>
    public async Task<Grid?> PriceGridAsync(Page page)
    {
        if (page.PricegridType == null || page.PricegridGroupId == null || await db.PlanGroups.FindAsync(page.PricegridGroupId) is not { } group) return null;
        var market = page.MarketId is { } mid ? await db.Markets.FindAsync(mid) : await db.Markets.OrderBy(m => m.Id).FirstOrDefaultAsync();
        var formula = page.RatingFormula ?? "avg_1000";
        var rates = Pricing.CurrentRates(await db.Rates.ToListAsync(), DateOnly.FromDateTime(DateTime.Today));
        var plans = await db.Plans.ToListAsync();
        var firstBySlug = plans.GroupBy(p => p.Slug).ToDictionary(g => g.Key, g => g.Min(p => p.Id));
        var rows = new List<GridRow>();
        foreach (var code in group.PlanCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var plan = plans.FirstOrDefault(p => p.Code == code && p.Active);
            var rate = plan == null || market == null ? null : rates.FirstOrDefault(r => r.PlanId == plan.Id && r.MarketId == market.Id);
            if (plan == null || rate == null || market == null) continue;
            decimal Avg(int kwh) => rate.EnergyCents + market.DeliveryCentsPerKwh + (market.DeliveryPerMonth + plan.MonthlyFee) / kwh * 100;
            rows.Add(new GridRow(plan, firstBySlug[plan.Slug] == plan.Id ? plan.Slug : plan.Slug + "-" + plan.Code.ToLowerInvariant(),
                formula == "energy" ? rate.EnergyCents : Avg(int.Parse(formula[4..])), new() { [500] = Avg(500), [1000] = Avg(1000), [2000] = Avg(2000) }));
        }
        return new Grid(page.PricegridType, page.PricegridHeader, market, Cms.RatingFormulas.GetValueOrDefault(formula, ""), rows);
    }

    /// <summary>A page's address on the website ("/", "/plans.html", "/locations/houston").</summary>
    public static string Url(Page p) => p.File != null ? (p.File == "index.html" ? "/" : "/" + p.File) : p.Path == "/" ? "/" : "/" + p.Path;
}
