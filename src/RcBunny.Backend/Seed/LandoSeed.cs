using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Seed;

public static partial class Seeder
{
    /// <summary>
    /// Lando: the website, its templates, content blocks and pages (ebunny's CmsSeeder and ContentModulesSeeder, from
    /// Seed/data/templates, blocks and pages.json), then 30 days of visits, blocked IPs and areas, and cookie choices
    /// (SiteTrafficSeeder, CookieConsentSeeder). Visits come from documentation-range IPs; everyone is fictional.
    /// </summary>
    private static async Task SeedLandoAsync(AppDbContext db, User admin)
    {
        var site = new Website { Name = "Energy Texas", Domain = "localhost" };
        db.Sites.Add(site);
        foreach (var t in Load("templates")) db.Templates.Add(new Template { Name = S(t, "name"), Description = S(t, "desc") });
        await db.SaveChangesAsync();
        var templates = await db.Templates.ToDictionaryAsync(t => t.Name, t => t.Id);

        // Categories come from the block name prefix ("Home - Hero" → Home)
        var categories = new Dictionary<string, BlockCategory>();
        BlockCategory Category(string name) => categories.TryGetValue(name, out var c) ? c : categories[name] = db.BlockCategories.Add(new BlockCategory { Name = name }).Entity;
        foreach (var b in Load("blocks"))
        {
            var name = S(b, "name");
            var updated = DateTime.Parse(S(b, "updated"));
            db.ContentBlocks.Add(new ContentBlock { Name = name, Slug = Slugify(name), Category = Category(name.Split(" - ")[0]), Html = S(b, "html"), CreatedAt = updated, UpdatedAt = updated });
        }
        var pageContent = Category("Page Content");
        foreach (var p in Load("pages"))
        {
            var path = S(p, "path");
            ContentBlock? content = null;
            if (p["content"] is { } html)
                content = db.ContentBlocks.Add(new ContentBlock { Name = S(p, "title") + " - Primary", Slug = "page-" + Slugify(path) + "-primary", Html = html.ToString(), Category = pageContent, SiteId = site.Id }).Entity;
            var file = Cms.Files.GetValueOrDefault(path);
            var redirect = S(p, "redirect");
            await db.SaveChangesAsync();
            db.Pages.Add(new Page
            {
                SiteId = site.Id, Path = path, Title = S(p, "title"), TemplateId = templates.GetValueOrDefault(S(p, "template")) is var t and > 0 ? t : null,
                Redirect = file != null || redirect == "" ? null : redirect, Status = S(p, "status"), File = file, ContentPrimaryId = content?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-120), UpdatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 60)),
            });
        }
        await db.SaveChangesAsync();

        // Modules: blocks with {{variables}} the pages fill in (ebunny's ContentModulesSeeder)
        var modules = Category("Modules");
        void Module(string slug, string name, string html) => db.ContentBlocks.Add(new ContentBlock { Slug = slug, Name = name, Html = html, Category = modules });
        Module("location-intro", "Module - Location Intro",
            "<section class=\"loc-intro\"><h2>Electricity plans in {{city|your city}}</h2><p>We serve {{city|your city}} and the {{utility|local utility}} service area. {{note|Compare fixed-rate plans and start service in minutes.}}</p>[[zip_form]]</section>");
        Module("promo-band", "Module - Promo Band",
            "<div class=\"rewards-band\"><b>{{heading|Earn Rangler Rewards}}</b> {{text|Earn stars on every on-time payment.}} <a href=\"{{link|/refer-a-friend}}\">{{button|Learn more}}</a></div>");
        Module("job-post", "Module - Job Post",
            "<article class=\"job-post\"><p>{{summary}}</p><ul><li><b>Location:</b> {{location|Houston, TX}}</li><li><b>Type:</b> {{type|Full time}}</li><li><b>Team:</b> {{team}}</li></ul><p>Send your resume through the <a href=\"/contact-us.html\">contact page</a>.</p></article>");
        var uses = new Dictionary<string, string>
        {
            ["page-locations-houston-primary"] = "[[block|location-intro|city=Houston|utility=CenterPoint]]",
            ["page-locations-dallas-primary"] = "[[block|location-intro|city=Dallas|utility=Oncor]]",
            ["page-locations-fort-worth-primary"] = "[[block|location-intro|city=Fort Worth|utility=Oncor]]",
            ["page-locations-corpus-christi-primary"] = "[[block|location-intro|city=Corpus Christi|utility=AEP Texas Central|note=Coastal homes: ask about our storm-season plans.]]",
            ["rewards-promo-band"] = "[[block|promo-band|heading=Rangler Rewards|text=Earn stars on every on-time payment and spend them on bill credits.|link=/myaccount/rewards|button=See your stars]]",
            ["page-careers-lead-developer-primary"] = "[[block|job-post|summary=Lead our web and customer-account development. Experience with PHP/Laravel and plain JavaScript preferred.|team=Technology]]",
            ["page-careers-staff-accountant-primary"] = "[[block|job-post|summary=Help close the books each month and support billing reconciliation. CPA or CPA track preferred.|team=Finance|location=Austin, TX]]",
        };
        foreach (var b in await db.ContentBlocks.Where(b => uses.Keys.Contains(b.Slug)).ToListAsync()) b.Html = uses[b.Slug];
        await db.SaveChangesAsync();

        // The rewards band as a component in the About Us sidebar
        var band = await db.ContentBlocks.FirstOrDefaultAsync(b => b.Slug == "rewards-promo-band");
        var about = await db.Pages.FirstOrDefaultAsync(p => p.Path == "about-us");
        if (band != null && about != null) db.PageComponents.Add(new PageComponent { PageId = about.Id, ContentBlockId = band.Id, Zone = "sidebar" });
        await db.SaveChangesAsync();

        await SeedPageHistoryAsync(db, admin);
        await SeedTrafficAsync(db, admin);
    }

    /// <summary>Sample edit history for the pages (ebunny's PageHistorySeeder): who made each page and later edits to it.</summary>
    private static async Task SeedPageHistoryAsync(AppDbContext db, User admin)
    {
        var r = new Random(7311);
        var editors = await db.Users.Where(u => u.Email == "admin@example.com" || u.Email == "marketing@example.com" || u.Email == "pricing@example.com").ToListAsync();
        if (editors.Count == 0) editors.Add(admin);
        (string Field, Func<Page, string> Old, Func<Page, string> New)[] edits =
        [
            ("html_title", p => p.HtmlTitle ?? "", p => p.Title + " | Energy Texas"),
            ("meta_description", p => p.MetaDescription ?? "", p => $"Learn about {p.Title.ToLowerInvariant()} from Energy Texas, a Texas retail electricity provider."),
            ("title", p => p.Title + " Page", p => p.Title),
            ("status", _ => "Draft", p => p.Status),
            ("no_index", _ => "1", p => p.NoIndex ? "1" : "0"),
            ("phone", _ => "", _ => "(800) 555-0142"),
        ];
        HistoryEntry Row(int id, string type, string model, string action, string summary, object? changes, User who, DateTime at) => new()
        {
            RecordType = type, RecordId = id, Model = model, Group = "Website", Action = action, Summary = summary, UserId = who.Id, Who = who.Name,
            Changes = changes == null ? null : JsonSerializer.Serialize(changes), CreatedAt = at,
        };
        foreach (var p in await db.Pages.ToListAsync())
        {
            var at = DateTime.Today.AddDays(-r.Next(60, 121)).AddHours(r.Next(8, 18)).AddMinutes(r.Next(60)).ToUniversalTime();
            db.History.Add(Row(p.Id, "Page", "Page_model", "created", "Page created: " + p.Title, new { path = p.Path, title = p.Title }, editors[r.Next(editors.Count)], at));
            foreach (var (field, old, @new) in edits.OrderBy(_ => r.Next()).Take(r.Next(2, 5)))
            {
                at = at.Date.AddDays(r.Next(2, 19)).AddHours(r.Next(8, 18)).AddMinutes(r.Next(60));
                if (at > DateTime.UtcNow) break;
                db.History.Add(Row(p.Id, "Page", "Page_model", "updated", $"{p.Title} — {field.Replace('_', ' ')} changed",
                    new Dictionary<string, string[]> { [field] = [old(p), @new(p)] }, editors[r.Next(editors.Count)], at));
            }
        }
        foreach (var c in await db.PageComponents.Include(c => c.Block).Include(c => c.Page).ToListAsync())
            db.History.Add(Row(c.Id, "PageComponent", "PageComponent_model", "created", $"Content block added to {c.Page?.Title}: {c.Block?.Name}", new { page_id = c.PageId, zone = c.Zone },
                editors[r.Next(editors.Count)], DateTime.UtcNow.AddDays(-r.Next(5, 41))));
        await db.SaveChangesAsync();
    }

    /// <summary>"Home - Hero" → "home-hero", "locations/houston" → "locations-houston" (Laravel's Str::slug).</summary>
    private static string Slugify(string s) => Regex.Replace(s.ToLowerInvariant().Replace("&", "and"), "[^a-z0-9]+", "-").Trim('-');

    /// <summary>30 days of website visits, people on the site now, a scraper and a bot range turned away, and cookie choices.</summary>
    private static async Task SeedTrafficAsync(AppDbContext db, User admin)
    {
        var r = new Random(92);
        var now = DateTime.UtcNow;
        var pages = await db.Pages.Where(p => p.Status == "Published" && p.Redirect == null).ToDictionaryAsync(p => p.Path, p => p.Id);
        // Popular pages get most of the views
        var weights = new Dictionary<string, int> { ["/"] = 30, ["plans"] = 14, ["byop"] = 6, ["business"] = 5, ["contact-us"] = 4, ["freedom-flex"] = 4, ["giddy-up"] = 3, ["locations/houston"] = 3, ["locations/dallas"] = 3, ["checkout"] = 4 };
        var weighted = weights.Where(w => pages.ContainsKey(w.Key)).SelectMany(w => Enumerable.Repeat(w.Key, w.Value))
            .Concat(pages.Keys.Where(p => !p.StartsWith("myaccount") && !p.StartsWith("checkout/") && p != "404")).ToList();
        var customers = await db.Customers.OrderBy(c => c.Id).Select(c => c.Id).Take(60).ToListAsync();
        string?[] refs = [null, null, null, "https://www.google.com/", "https://www.google.com/", "https://www.bing.com/", "https://www.facebook.com/", "https://www.powertochoose.org/"];
        string[] agents = ["Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) Safari/604.1", "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) Safari/605.1", "Mozilla/5.0 (Linux; Android 14) Chrome/128.0 Mobile"];
        string Id() => new(Enumerable.Range(0, 32).Select(_ => "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[r.Next(62)]).ToArray());
        var people = Enumerable.Range(0, 420).Select(_ => (Visitor: Id(), Ip: (r.Next(2) == 0 ? "198.51.100." : "203.0.113.") + r.Next(1, 255),
            Customer: r.Next(6) == 0 ? customers[r.Next(customers.Count)] : (int?)null, Agent: agents[r.Next(agents.Length)])).Where(p => p.Ip != "203.0.113.66").ToList();

        var visits = new List<SiteVisit>();
        void Add((string Visitor, string Ip, int? Customer, string Agent) p, string path, DateTime at, int stay, bool blocked = false, string? referrer = null) =>
            visits.Add(new SiteVisit { Visitor = p.Visitor, Ip = p.Ip, Path = path, PageId = pages.TryGetValue(path, out var id) ? id : null, CustomerId = p.Customer,
                Referrer = referrer, Agent = p.Agent, Blocked = blocked, CreatedAt = at, SeenAt = at.AddSeconds(stay) });
        int[] hours = [7, 8, 9, 10, 10, 11, 11, 12, 12, 13, 14, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 23, 1, 6];
        string[] myAccount = ["myaccount/dashboard", "myaccount/bill-and-payments/pay-bill", "myaccount/rewards", "myaccount/support"];
        for (var d = 29; d >= 0; d--)
        {
            var day = DateTime.Today.AddDays(-d);
            var sessions = r.Next(45, 81) + (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? -15 : 0) + (29 - d);
            for (var s = 0; s < sessions; s++)
            {
                var at = day.AddHours(hours[r.Next(hours.Length)]).AddMinutes(r.Next(60)).AddSeconds(r.Next(60)).ToUniversalTime();
                if (at > now) continue;
                var p = people[r.Next(people.Count)];
                var referrer = refs[r.Next(refs.Length)];
                for (var n = 1; n <= r.Next(1, 6); n++)
                {
                    var path = n == 1 ? weighted[r.Next(weighted.Count)] : p.Customer != null && r.Next(2) == 0 ? myAccount[r.Next(4)] : weighted[r.Next(weighted.Count)];
                    var stay = r.Next(15, 241);
                    Add(p, path, at, stay, false, n == 1 ? referrer : null);
                    at = at.AddSeconds(stay + r.Next(2, 21));
                }
            }
        }
        // On the site right now
        foreach (var (p, i) in people.OrderBy(_ => r.Next()).Take(9).Select((p, i) => (p, i)))
        {
            var at = now.AddMinutes(-r.Next(1, 26));
            Add(p, i < 3 ? "/" : weighted[r.Next(weighted.Count)], at, (int)(now.AddSeconds(-r.Next(5, 121)) - at).TotalSeconds);
        }
        // A scraper on the rates pages, and a bot range probing for admin logins
        var scraper = (Visitor: Id(), Ip: "203.0.113.66", Customer: (int?)null, Agent: "python-requests/2.32");
        string[] scraped = ["plans", "business/plans", "byop", "historical-efls"];
        for (var i = 1; i <= 60; i++) Add(scraper, scraped[i % 4], now.AddDays(-3).AddMinutes(i), 2, i > 40);
        string[] probes = ["wp-admin", "wp-login.php", ".env", "xmlrpc.php"];
        for (var i = 1; i <= 36; i++) Add((Id(), "192.0.2." + r.Next(1, 255), null, "Mozilla/5.0 (compatible; scanner)"), probes[i % 4], now.AddHours(-r.Next(1, 241)), 1, true);
        db.SiteVisits.AddRange(visits);

        SiteBlock Block(string type, string value, string reason, int hits = 0, DateTime? last = null, bool active = true, DateTime? expires = null, string? message = null) =>
            new() { Type = type, Value = value, Reason = reason, Hits = hits, LastHitAt = last, Active = active, ExpiresAt = expires, Message = message, UserId = admin.Id, CreatedAt = now.AddDays(-r.Next(4, 41)) };
        db.SiteBlocks.AddRange(
            Block("ip", "203.0.113.66", "Scraping the plans and rates pages every few seconds", 20, now.AddDays(-3).AddMinutes(60)),
            Block("ip", "192.0.2.*", "Bot range probing for WordPress and .env files", 36, now.AddHours(-1)),
            Block("ip", "198.51.100.250", "Repeated fake sign-ups", 4, now.AddDays(-2), expires: now.AddDays(5)),
            Block("ip", "203.0.113.12", "Contact form spam (resolved)", 11, now.AddDays(-20), active: false),
            Block("area", "wp-admin", "Not a WordPress site: only bots ask for this", 9),
            Block("area", ".env", "Bots looking for secrets", 9),
            Block("area", "xmlrpc.php", "Bots", 9),
            Block("area", "careers/staff-accountant", "Position filled", active: false, message: "This position has been filled. See our other openings on the Careers page."));
        await db.SaveChangesAsync();

        // Cookie banner choices from recent visitors and a few signed-in customers (ebunny's CookieConsentSeeder)
        string[] browsers = ["Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_6) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15",
            "Mozilla/5.0 (Linux; Android 15; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36"];
        var chosen = visits.Where(v => !v.Blocked).OrderBy(_ => r.Next()).Take(120).DistinctBy(v => v.Visitor).ToList();
        foreach (var (v, i) in chosen.Select((v, i) => (v, i)))
        {
            var roll = r.Next(1, 101);
            var (choice, a, m) = roll <= 62 ? ("all", true, true) : roll <= 85 ? ("necessary", false, false) : ("custom", true, r.Next(2) == 1);
            var k = new CookieConsent { Visitor = v.Visitor, CustomerId = i < 15 ? customers[i] : null, Choice = choice, Analytics = a, Marketing = m, Ip = v.Ip,
                UserAgent = browsers[i % browsers.Length], Page = "/" + (v.Path == "/" ? "" : v.Path), CreatedAt = v.CreatedAt, CookiesSeenAt = v.CreatedAt.AddMinutes(r.Next(1, 91)) };
            k.CookieJar = JsonSerializer.Serialize(Jar(k, i, r));
            db.CookieConsents.Add(k);
        }
        await db.SaveChangesAsync();
    }

    /// <summary>A made-up cookie cache for a choice: the cookies it allows, now and then one it doesn't (set before they chose) or a stray.</summary>
    private static Dictionary<string, string> Jar(CookieConsent k, int i, Random r)
    {
        var ts = new DateTimeOffset(k.CreatedAt).ToUnixTimeSeconds();
        var values = new Dictionary<string, string>
        {
            ["site_session"] = "eyJpdiI6…", ["XSRF-TOKEN"] = "eyJpdiI6…", ["et_vid"] = k.Visitor,
            ["cookie_consent"] = JsonSerializer.Serialize(new { v = k.PolicyVersion, a = k.Analytics ? 1 : 0, m = k.Marketing ? 1 : 0 }),
            ["_ga"] = $"GA1.1.{r.Next(100000000, 999999999)}.{ts}", ["_ga_ID"] = $"GS1.1.{ts}.{r.Next(1, 10)}.1.{ts + 300}.0.0.0",
            ["_clck"] = $"{Guid.NewGuid().ToString("N")[..6]}|2|fq{r.Next(1, 10)}|0|{r.Next(1700, 1801)}", ["_fbp"] = $"fb.1.{ts * 1000}.{r.Next(100000000, 999999999)}",
            ["_gcl_au"] = $"1.1.{r.Next(100000000, 999999999)}.{ts}", ["li_fat_id"] = Guid.NewGuid().ToString(),
        };
        var jar = k.Allowed().ToDictionary(n => n, n => values[n]);
        if (i % 9 == 4 && !k.Analytics) jar["_ga"] = values["_ga"];                  // set before they chose
        if (i % 13 == 6) jar["wp-settings-time-1"] = (ts - 86400 * 400).ToString();  // left over from the old site
        return jar;
    }
}
