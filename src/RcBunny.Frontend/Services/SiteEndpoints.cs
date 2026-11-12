using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

        // Each page asks once as it opens: who is signed in, and (with ?path=) that's the page view Lando counts. A blocked
        // IP address or area, or the website switched off in Sheriff, gets a short notice instead of the page.
        app.MapGet("/site/session", async (HttpContext http, AppDbContext db, IMemoryCache cache, string? path, string? @ref) =>
        {
            var customerId = CustomerId(http);
            string? first = customerId == null ? null : await db.Customers.Where(c => c.Id == customerId).Select(c => c.FirstName).FirstOrDefaultAsync();
            var visitor = Visitor(http);
            SiteVisit? visit = null;
            SiteBlock? block = null;
            if (!string.IsNullOrEmpty(path))
            {
                var (normal, pageId) = await NormalizeAsync(db, path);
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "";
                block = await BlockForAsync(db, cache, ip, normal);
                if (block != null)
                {
                    await db.SiteBlocks.Where(b => b.Id == block.Id).ExecuteUpdateAsync(u => u.SetProperty(b => b.Hits, b => b.Hits + 1).SetProperty(b => b.LastHitAt, DateTime.UtcNow));
                }
                var referrer = @ref != null && !@ref.StartsWith($"{http.Request.Scheme}://{http.Request.Host}") ? Cut(@ref, 250) : null;
                visit = new SiteVisit { Visitor = visitor ?? NewVisitor(), Ip = ip, Path = Cut(normal, 250), PageId = pageId, CustomerId = customerId, Referrer = referrer,
                    Agent = Cut(http.Request.Headers.UserAgent.ToString(), 250), Blocked = block != null };
                db.SiteVisits.Add(visit);
                await db.SaveChangesAsync();
                http.Response.Cookies.Append(VisitorCookie, visit.Visitor, new CookieOptions { MaxAge = TimeSpan.FromDays(365), HttpOnly = true, SameSite = SameSiteMode.Lax });
            }
            var down = await db.HeartbeatControls.AnyAsync(h => h.Key == "website" && h.Down);
            var consent = await CurrentConsentAsync(db, customerId, visit?.Visitor ?? visitor);
            http.Response.Headers.CacheControl = "no-store";
            return Results.Json(new
            {
                csrf = "-", user = (object?)null, customer = first == null ? null : new { name = first }, cart = 0,
                visit = visit != null && block == null ? visit.Id : (int?)null,
                blocked = block != null ? new { message = block.Message ?? "This page isn't available." } : down ? new { message = "We're doing some maintenance. Please check back soon." } : null,
                consent = consent == null ? null : new { analytics = consent.Analytics, marketing = consent.Marketing, choice = consent.Choice },
            });
        });

        // The open page's heartbeat, so Lando can show who is on the site now; also keeps the cookie cache on this browser's latest choice current
        app.MapPost("/site/ping", async (HttpContext http, AppDbContext db, int? v) =>
        {
            if (Visitor(http) is { } visitor)
            {
                if (v is { } id)
                {
                    var since = DateTime.UtcNow.AddHours(-12);
                    await db.SiteVisits.Where(x => x.Id == id && x.Visitor == visitor && x.CreatedAt >= since).ExecuteUpdateAsync(u => u.SetProperty(x => x.SeenAt, DateTime.UtcNow));
                }
                var stale = DateTime.UtcNow.AddMinutes(-10);
                var latest = await db.CookieConsents.Where(k => k.Visitor == visitor).OrderByDescending(k => k.Id).FirstOrDefaultAsync();
                if (latest != null && (latest.CookiesSeenAt == null || latest.CookiesSeenAt < stale))
                {
                    (latest.CookieJar, latest.CookiesSeenAt) = (Jar(http), DateTime.UtcNow);
                    await db.SaveChangesAsync();
                }
            }
            http.Response.Headers.CacheControl = "no-store";
            return Results.Json(new { ok = true });
        });

        // The cookie banner's answer: Accept all, Necessary only, or chosen categories. Stored for the browser and the signed-in customer.
        app.MapPost("/site/consent", async (HttpContext http, AppDbContext db, ConsentInput input) =>
        {
            if (input.Choice is not ("all" or "necessary" or "custom")) return Results.Json(new { message = "Pick a choice." }, statusCode: 422);
            var visitor = Visitor(http) ?? NewVisitor();
            var (analytics, marketing) = input.Choice switch { "all" => (true, true), "necessary" => (false, false), _ => (input.Analytics, input.Marketing) };
            db.CookieConsents.Add(new CookieConsent
            {
                Visitor = visitor, CustomerId = CustomerId(http), Choice = input.Choice, Analytics = analytics, Marketing = marketing, Ip = http.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Cut(http.Request.Headers.UserAgent.ToString(), 245), Page = input.Page == null ? null : Cut(input.Page, 250), CookieJar = Jar(http), CookiesSeenAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            http.Response.Cookies.Append(VisitorCookie, visitor, new CookieOptions { MaxAge = TimeSpan.FromDays(365), HttpOnly = true, SameSite = SameSiteMode.Lax });
            return Results.Json(new { consent = new { analytics, marketing, choice = input.Choice } });
        });

        // /site/components?page=plans.html — the Lando components for a built-in page's zones
        app.MapGet("/site/components", async (AppDbContext db, SiteContent content, string? page) =>
        {
            var file = Path.GetFileName(page ?? "") is { Length: > 0 } f ? f : "index.html";
            var p = await db.Pages.FirstOrDefaultAsync(x => x.File == file && x.Status == "Published");
            if (p == null) return Results.Json(new { });
            content.AddressBoxTitle = p.AddressBoxTitle;
            return Results.Json(await content.ZonesAsync(p));
        });

        // /amp/<path>: a Lando page's AMP Content as an AMP page (Edit Page → AMP Content)
        app.MapGet("/amp/{**path}", async (HttpContext http, AppDbContext db, SiteContent content, string path) =>
        {
            var page = await db.Pages.FirstOrDefaultAsync(p => p.Path == path.Trim('/') && p.Status == "Published");
            var source = page?.CopyFromId is { } from ? await db.Pages.FindAsync(from) ?? page : page;
            var amp = source?.ContentAmpId is { } id ? await db.ContentBlocks.FindAsync(id) : null;
            if (page == null || page.Redirect != null || page.File != null || amp == null) return Results.NotFound();
            var site = await db.Sites.FindAsync(page.SiteId);
            string E(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var url = $"{http.Request.Scheme}://{http.Request.Host}/{page.Path}";
            return Results.Content($$$"""
                <!doctype html>
                <html ⚡ lang="en">
                <head>
                  <meta charset="utf-8">
                  <script async src="https://cdn.ampproject.org/v0.js"></script>
                  <title>{{{E(page.HtmlTitle ?? page.Title + " | " + site?.Name)}}}</title>
                  <link rel="canonical" href="{{{url}}}">
                  <meta name="viewport" content="width=device-width">
                  {{{(page.MetaDescription != null ? $"<meta name=\"description\" content=\"{E(page.MetaDescription)}\">" : "")}}}
                  <style amp-boilerplate>body{-webkit-animation:-amp-start 8s steps(1,end) 0s 1 normal both;-moz-animation:-amp-start 8s steps(1,end) 0s 1 normal both;-ms-animation:-amp-start 8s steps(1,end) 0s 1 normal both;animation:-amp-start 8s steps(1,end) 0s 1 normal both}@-webkit-keyframes -amp-start{from{visibility:hidden}to{visibility:visible}}@-moz-keyframes -amp-start{from{visibility:hidden}to{visibility:visible}}@-ms-keyframes -amp-start{from{visibility:hidden}to{visibility:visible}}@-o-keyframes -amp-start{from{visibility:hidden}to{visibility:visible}}@keyframes -amp-start{from{visibility:hidden}to{visibility:visible}}</style><noscript><style amp-boilerplate>body{-webkit-animation:none;-moz-animation:none;-ms-animation:none;animation:none}</style></noscript>
                  <style amp-custom>body{font-family:Arial,sans-serif;color:#102247;margin:0}header{background:#102247;color:#fff;padding:14px 16px;font-weight:700}main{padding:16px}a{color:#0083c1}</style>
                </head>
                <body>
                  <header><a href="{{{url}}}" style="color:#fff;text-decoration:none">{{{E(site?.Name)}}}</a></header>
                  <main>
                    <h1>{{{E(source!.PageTitle ?? page.Title)}}}</h1>
                    {{{await content.ExpandAsync(amp.Html, site)}}}
                  </main>
                </body>
                </html>
                """, "text/html; charset=utf-8");
        });

        // The sitemap Lando builds (List Pages → Rebuild Sitemap)
        app.MapGet("/sitemap.xml", (IConfiguration config, IWebHostEnvironment env) =>
        {
            var file = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage", "sitemap.xml"));
            return File.Exists(file) ? Results.File(file, "application/xml") : Results.NotFound();
        });

        // Contact Us form → Ranger → Web Messages
        app.MapPost("/contact", async (ContactInput input, AppDbContext db, HttpContext http) =>
        {
            if (string.IsNullOrWhiteSpace(input.First) || string.IsNullOrWhiteSpace(input.Last) || string.IsNullOrWhiteSpace(input.Email)
                || !input.Email.Contains('@') || string.IsNullOrWhiteSpace(input.Topic) || string.IsNullOrWhiteSpace(input.Message))
                return Results.Json(new { message = "Fill in every required field." }, statusCode: 422);
            var msg = db.ContactMessages.Add(new ContactMessage
            {
                Name = $"{input.First.Trim()} {input.Last.Trim()}", Email = input.Email.Trim(), Phone = input.Phone?.Trim(),
                Topic = input.Topic.Trim(), Message = input.Message.Trim()[..Math.Min(5000, input.Message.Trim().Length)],
                Ip = http.Connection.RemoteIpAddress?.ToString(),
            }).Entity;
            await db.SaveChangesAsync();
            AdminNotification.Notify(db, await AdminNotification.AppUsersAsync(db, "ranger"), "email", $"New email from {msg.Name}",
                $"{msg.Topic}: {(msg.Message.Length > 140 ? msg.Message[..140] : msg.Message)}", $"/admin/ranger/messages/{msg.Id}");
            await db.SaveChangesAsync();
            return Results.Json(new { ok = true }, statusCode: 201);
        });

        // /admin is the admin, like on ebunny; here the admin runs as its own app, so /admin/... goes to the same page there
        app.MapMethods("/admin/{**rest}", ["GET", "POST"], (string? rest) => Results.Redirect($"{Site.AdminUrl}/admin{(string.IsNullOrEmpty(rest) ? "" : "/" + rest)}"));
    }

    public record ConsentInput(string? Choice, bool Analytics, bool Marketing, string? Page);

    /// <summary>The et_vid cookie: a random id per browser, so Lando can tell visits apart (no personal details).</summary>
    public const string VisitorCookie = "et_vid";

    private static string? Visitor(HttpContext http) =>
        http.Request.Cookies[VisitorCookie] is { } v && System.Text.RegularExpressions.Regex.IsMatch(v, "^[a-zA-Z0-9]{20,40}$") ? v : null;

    private static string NewVisitor() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static int? CustomerId(HttpContext http) => int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static string Cut(string s, int n) => s.Length > n ? s[..n] : s;

    /// <summary>"/plans.html" → the plans page's path and id; "/" and "/index.html" are the home page.</summary>
    private static async Task<(string Path, int? PageId)> NormalizeAsync(AppDbContext db, string raw)
    {
        var path = raw.Split('?')[0].Split('#')[0].Trim('/');
        if (path is "" or "index.html") path = "/";
        var page = path.EndsWith(".html") || path == "/"
            ? await db.Pages.FirstOrDefaultAsync(p => p.File == (path == "/" ? "index.html" : path))
            : await db.Pages.FirstOrDefaultAsync(p => p.Path == path);
        return (page?.Path ?? path, page?.Id);
    }

    /// <summary>The block in force for this IP or path, if any (cached briefly: it's checked on every page).</summary>
    private static async Task<SiteBlock?> BlockForAsync(AppDbContext db, IMemoryCache cache, string ip, string path)
    {
        var blocks = await cache.GetOrCreateAsync("site-blocks", e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return db.SiteBlocks.AsNoTracking().ToListAsync();
        }) ?? [];
        var inForce = blocks.Where(b => b.InForce).ToList();
        return inForce.FirstOrDefault(b => b.Type == "ip" && b.MatchesIp(ip)) ?? inForce.FirstOrDefault(b => b.Type == "area" && path != "/" && b.MatchesPath(path));
    }

    /// <summary>The cookie choice in force: a signed-in customer's own (from any device), else this browser's.</summary>
    private static async Task<CookieConsent?> CurrentConsentAsync(AppDbContext db, int? customerId, string? visitor)
    {
        var q = db.CookieConsents.Where(k => k.PolicyVersion == CookieConsent.PolicyNow).OrderByDescending(k => k.Id);
        if (customerId != null && await q.FirstOrDefaultAsync(k => k.CustomerId == customerId) is { } mine) return mine;
        return visitor == null ? null : await q.FirstOrDefaultAsync(k => k.Visitor == visitor);
    }

    /// <summary>The cookies the browser sent, as JSON. Sign-in and form tokens are cut short: enough to tell them apart, never enough to use.</summary>
    private static string Jar(HttpContext http)
    {
        var jar = new Dictionary<string, string>();
        foreach (var (name, value) in http.Request.Cookies.Take(40))
        {
            var secret = name.Contains("session", StringComparison.OrdinalIgnoreCase) || name.StartsWith("rcb.") || name.StartsWith(".AspNetCore") || name == "XSRF-TOKEN";
            jar[Cut(name, 60)] = secret ? Cut(value, 8) + "…" : Cut(value, 120);
        }
        return JsonSerializer.Serialize(jar);
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

    private static IResult Script(string js) =>
        Results.Text("window.ET = window.ET || {};\n" + js + "\n", "application/javascript; charset=utf-8");
}
