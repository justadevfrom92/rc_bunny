using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Frontend.Services;

/// <summary>
/// The website's pages are Razor Pages with ebunny's markup, style.css and main.js. These are the few things main.js
/// asks the server for: the brand, the plan catalog, who's signed in, and the Contact Us form.
/// </summary>
public static class SiteEndpoints
{
    public static void MapSite(this WebApplication app)
    {

        // /shared/config/brand.js — the brand settings
        app.MapGet("/shared/config/brand.js", () => Script("ET.brand = " + JsonSerializer.Serialize(Brand) + ";"));

        // /shared/config/catalog.js — markets, plans, rates, fees and pricing modifiers from the database
        app.MapGet("/shared/config/catalog.js", async (AppDbContext db) => Script("ET.defaults = " + JsonSerializer.Serialize(await CatalogAsync(db)) + ";"));

        // Who (if anyone) is signed in to My Account
        app.MapGet("/site/session", async (HttpContext http, AppDbContext db) =>
        {
            string? first = null;
            if (int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                first = await db.Customers.Where(c => c.Id == id).Select(c => c.FirstName).FirstOrDefaultAsync();
            var down = await db.HeartbeatControls.AnyAsync(h => h.Key == "website" && h.Down);
            http.Response.Headers.CacheControl = "no-store";
            return Results.Json(new
            {
                csrf = "-", user = (object?)null, customer = first == null ? null : new { name = first }, cart = 0, visit = (int?)null,
                blocked = down ? new { message = "We're doing some maintenance. Please check back soon." } : null,
                consent = Consent(http),
            });
        });
        app.MapPost("/site/ping", () => Results.Json(new { ok = true }));
        app.MapPost("/site/consent", async (HttpContext http) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(http.Request.Body);
            var choice = body.TryGetProperty("choice", out var c) ? c.GetString() : "necessary";
            bool Flag(string k) => choice == "all" || (choice == "custom" && body.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True);
            var consent = new { analytics = Flag("analytics"), marketing = Flag("marketing"), choice };
            http.Response.Cookies.Append("et_consent", JsonSerializer.Serialize(consent), new CookieOptions { MaxAge = TimeSpan.FromDays(365), HttpOnly = true });
            return Results.Json(new { consent });
        });
        // Content zones come from the CMS (Lando), which this site doesn't have yet
        app.MapGet("/site/components", () => Results.Json(new { }));

        // Contact Us form → Ranger → Web Messages
        app.MapPost("/contact", async (ContactInput input, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(input.First) || string.IsNullOrWhiteSpace(input.Last) || string.IsNullOrWhiteSpace(input.Email)
                || !input.Email.Contains('@') || string.IsNullOrWhiteSpace(input.Topic) || string.IsNullOrWhiteSpace(input.Message))
                return Results.Json(new { message = "Fill in every required field." }, statusCode: 422);
            db.ContactMessages.Add(new ContactMessage
            {
                Name = $"{input.First.Trim()} {input.Last.Trim()}", Email = input.Email.Trim(), Phone = input.Phone?.Trim(),
                Topic = input.Topic.Trim(), Message = input.Message.Trim()[..Math.Min(5000, input.Message.Trim().Length)],
            });
            await db.SaveChangesAsync();
            return Results.Json(new { ok = true }, statusCode: 201);
        });

        // /admin is the admin, like on ebunny; here the admin runs as its own app, so /admin/... goes to the same page there
        app.MapMethods("/admin/{**rest}", ["GET", "POST"], (string? rest) => Results.Redirect($"{Site.AdminUrl}/admin{(string.IsNullOrEmpty(rest) ? "" : "/" + rest)}"));
    }

    public record ContactInput(string? First, string? Last, string? Email, string? Phone, string? Topic, string? Message);

    /// <summary>Same as ebunny's config/brand.php.</summary>
    private static readonly object Brand = new
    {
        name = Site.Name, wordmark = "ENERGY TEXAS", tagline = "POWERED BY TEXANS", domain = "energytexas.com",
        phone = Site.Phone, phoneHref = Site.PhoneLink, hours = "Mon – Fri, 8am – 6pm CT", puct = "XXXXX", rewards = "Rangler Rewards",
        signoutTest = true,
        nav = new[]
        {
            new[] { "index.html", "Home" }, ["plans.html", "Plans"], ["build-your-own-plan.html", "Build Your Own Plan"],
            ["business.html", "Business"], ["shop", "Shop"], ["contact-us.html", "Contact Us"],
        },
    };

    /// <summary>The catalog in ebunny's shape (ET.defaults): MARKETS, ZIP_RANGES, TDSP_FEES, PLANS, PLAN_GROUPS, RATES, REGIONS, TERM_MODS, BYOP_PRODUCTS.</summary>
    public static async Task<object> CatalogAsync(AppDbContext db)
    {
        var markets = await db.Markets.OrderBy(m => m.Id).ToListAsync();
        var plans = await db.Plans.OrderBy(p => p.Id).ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        // The rate in effect today for each plan and market
        var rates = Pricing.CurrentRates(await db.Rates.ToListAsync(), today);
        var regions = new[] { "Generic" }.Concat(markets.Select(m => m.Region).Distinct()).ToList();
        var discounts = await db.TermDiscounts.ToListAsync();
        var etfs = await db.TermEtfs.ToDictionaryAsync(e => e.Term, e => e.Amount);
        var firstBySlug = plans.GroupBy(p => p.Slug).ToDictionary(g => g.Key, g => g.Min(p => p.Id));

        return new
        {
            MARKETS = markets.Select(m => new { id = m.Id, name = m.Code, @short = m.Short, desc = m.Name, region = m.Region, phone = m.Phone }),
            ZIP_RANGES = (await db.ZipRanges.ToListAsync()).Select(z => new object[] { z.From, z.To, markets.First(m => m.Id == z.MarketId).Code }),
            TDSP_FEES = markets.Select(m => new
            {
                market = m.Code, perKwh = m.DeliveryCentsPerKwh, perBill = m.DeliveryPerMonth,
                kwhDate = m.DeliveryEffectiveOn?.ToString("yyyy-MM-dd"), billDate = m.DeliveryEffectiveOn?.ToString("yyyy-MM-dd"),
            }),
            PLANS = plans.Select(p => new
            {
                id = p.Id, type = p.Type == "Business" ? "Biz" : "Resi", term = p.TermMonths, name = p.Name, @internal = p.Code,
                rolloff = p.Rolloff, etf = p.Etf, mrc = p.MonthlyFee, green = p.GreenPercent, active = p.Active,
                tags = p.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries), perks = p.Perks.Split('\n', StringSplitOptions.RemoveEmptyEntries),
                slug = p.Slug, page = firstBySlug[p.Slug] == p.Id ? p.Slug : p.Slug + "-" + p.Code.ToLowerInvariant(),
            }),
            PLAN_GROUPS = (await db.PlanGroups.OrderBy(g => g.Id).ToListAsync()).Select(g => new
            {
                id = g.Id, name = g.Name, slug = g.Slug, plans = g.PlanCodes.Split(',', StringSplitOptions.RemoveEmptyEntries),
            }),
            RATES = rates.Select(r => new
            {
                plan = plans.First(p => p.Id == r.PlanId).Code, market = markets.First(m => m.Id == r.MarketId).Code,
                energy = r.EnergyCents, effective = r.EffectiveOn.ToString("yyyy-MM-dd"),
            }),
            REGIONS = regions,
            TERM_MODS = discounts.GroupBy(d => d.Term).OrderBy(g => g.Key).Select(g =>
            {
                var row = new Dictionary<string, object> { ["term"] = g.Key };
                foreach (var region in regions) row[region] = g.FirstOrDefault(d => d.Region == region)?.Discount ?? 0;
                row["etf"] = (int)etfs.GetValueOrDefault(g.Key);
                return row;
            }),
            BYOP_PRODUCTS = (await db.ByopProducts.OrderBy(b => b.Position).ToListAsync()).Select(b => new
            {
                id = b.Id, key = b.Key, name = b.Name, model = b.Model, type = b.Type, adj = b.RateAdj, monthly = b.Monthly, step = b.Step, active = b.Active, description = b.Description,
            }),
        };
    }

    private static object? Consent(HttpContext http)
    {
        try { return http.Request.Cookies["et_consent"] is { } c ? JsonSerializer.Deserialize<JsonElement>(c) : null; }
        catch (JsonException) { return null; }
    }

    private static IResult Script(string js) =>
        Results.Text("window.ET = window.ET || {};\n" + js + "\n", "application/javascript; charset=utf-8");
}
