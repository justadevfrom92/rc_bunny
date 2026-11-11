using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// Lando: the website. Site (dashboard, health, visitors, blocked IPs and areas, cookie consents), Pages (with their
/// activity and components), Page Templates and Content Blocks; the same screens as ebunny's Lando.
/// </summary>
[ApiController]
[Route("api/lando")]
[RequirePermission("lando")]
public partial class LandoController(AppDbContext db, IMemoryCache cache, IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    private static DateTime Now => DateTime.UtcNow;
    private static DateTime OnlineSince => DateTime.UtcNow.AddMinutes(-Cms.OnlineMinutes);
    /// <summary>Midnight today (local), in UTC like the stored times.</summary>
    private static DateTime TodayStart => DateTime.Today.ToUniversalTime();
    private async Task<Permissions.SignedIn> Me() => (await Permissions.CurrentAsync(HttpContext))!;
    private string Storage => Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage"));

    // ---- Dashboard ----------------------------------------------------------------------------------------------

    [HttpGet("dashboard")]
    public async Task<object> Dashboard()
    {
        var views = db.SiteVisits.Where(v => !v.Blocked);
        var online = await views.Where(v => v.SeenAt >= OnlineSince).ToListAsync();
        var today = await views.Where(v => v.CreatedAt >= TodayStart).ToListAsync();
        var yesterday = await views.Where(v => v.CreatedAt >= TodayStart.AddDays(-1) && v.CreatedAt < TodayStart).Select(v => v.CreatedAt).ToListAsync();
        var checks = await Checks();
        var blocks = await db.SiteBlocks.ToListAsync();
        // One row per person on the site now, most recently active first
        var customers = await Names(online.Select(v => v.CustomerId));
        var onSite = online.GroupBy(v => v.Visitor).Select(g => g.MaxBy(v => v.SeenAt)!).OrderByDescending(v => v.SeenAt).Take(10).Select(v =>
        {
            var trail = today.Where(x => x.Visitor == v.Visitor).OrderBy(x => x.CreatedAt).ToList();
            var first = trail.FirstOrDefault() ?? v;
            return new
            {
                v.Id, v.Visitor, v.Path, v.PageId, v.Ip, v.SeenAt, v.CustomerId, customer = v.CustomerId is { } c ? customers.GetValueOrDefault(c) : null,
                trail = trail.Select(x => x.Path).Distinct().Where(p => p != v.Path).TakeLast(4), pages = Math.Max(1, trail.Count), arrived = first.CreatedAt,
                from = trail.FirstOrDefault(x => x.Referrer != null)?.Referrer, device = Device(v.Agent), browser = Browser(v.Agent),
            };
        });
        return new
        {
            online = online.Select(v => v.Visitor).Distinct().Count(),
            onlineCustomers = online.Where(v => v.CustomerId != null).Select(v => v.CustomerId).Distinct().Count(),
            visitorsToday = today.Select(v => v.Visitor).Distinct().Count(), viewsToday = today.Count,
            visitors30 = await views.Where(v => v.CreatedAt >= TodayStart.AddDays(-29)).Select(v => v.Visitor).Distinct().CountAsync(),
            accounts = await db.Customers.CountAsync(c => c.Username != null && c.PasswordHash != null), customers = await db.Customers.CountAsync(),
            signedInToday = today.Where(v => v.CustomerId != null).Select(v => v.CustomerId).Distinct().Count(),
            problems = checks.Count(c => c.State != "ok"), checks = checks.OrderBy(c => c.State switch { "bad" => 0, "warn" => 1, _ => 2 }).Take(8), checkCount = checks.Count,
            blockedIps = blocks.Count(b => b.Type == "ip" && b.InForce), blockedAreas = blocks.Count(b => b.Type == "area" && b.InForce),
            blockedToday = await db.SiteVisits.CountAsync(v => v.Blocked && v.CreatedAt >= TodayStart),
            pages = await db.Pages.CountAsync(p => p.Status == "Published"),
            hours = Enumerable.Range(0, 24).Select(h => new
            {
                hour = h, today = today.Count(v => v.CreatedAt.ToLocalTime().Hour == h), yesterday = yesterday.Count(t => t.ToLocalTime().Hour == h),
            }),
            onSite,
            topPages = today.GroupBy(v => (v.Path, v.PageId)).Select(g => new { path = g.Key.Path, pageId = g.Key.PageId, n = g.Count(), people = g.Select(v => v.Visitor).Distinct().Count() })
                .OrderByDescending(x => x.n).Take(8),
            recentBlocks = await db.SiteVisits.Where(v => v.Blocked).OrderByDescending(v => v.CreatedAt).Take(8).Select(v => new { v.Id, v.CreatedAt, v.Ip, v.Path }).ToListAsync(),
            changes = await db.History.Where(h => h.Group == "Website").OrderByDescending(h => h.CreatedAt).Take(8).Select(h => new { h.Id, h.CreatedAt, h.Who, h.Summary }).ToListAsync(),
        };
    }

    private async Task<Dictionary<int, string>> Names(IEnumerable<int?> ids)
    {
        var list = ids.OfType<int>().Distinct().ToList();
        return await db.Customers.Where(c => list.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
    }

    private static string Device(string? agent) => agent == null ? "—" : agent.Contains("iPad") ? "Tablet" : agent.Contains("iPhone") || agent.Contains("Mobile") || agent.Contains("Android") ? "Phone" : "Computer";
    private static string Browser(string? agent) => agent == null ? "" : agent.Contains("Edg/") ? "Edge" : agent.Contains("Chrome") ? "Chrome" : agent.Contains("Firefox") ? "Firefox"
        : agent.Contains("Safari") ? "Safari" : agent.Split('/')[0];

    // ---- Site Health ------------------------------------------------------------------------------------------

    public record Check(string Group, string Name, string State, string Found, string? Fix);

    [HttpGet("health")]
    public async Task<object> Health() => new
    {
        checks = await Checks(),
        about = new Dictionary<string, string>
        {
            [".NET"] = Environment.Version.ToString(), ["Operating system"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ["Environment"] = env.EnvironmentName, ["Database"] = db.Database.ProviderName?.Split('.').Last() ?? "—",
            ["Server time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"), ["Machine"] = Environment.MachineName,
            ["Started"] = Process.GetCurrentProcess().StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
        },
    };

    /// <summary>ebunny's DashboardController::checks: the server, settings, content and traffic.</summary>
    private async Task<List<Check>> Checks()
    {
        var checks = new List<Check>();
        void Add(string group, string name, string state, string found, string? fix = null) => checks.Add(new Check(group, name, state, found, fix));

        // Server
        var sw = Stopwatch.StartNew();
        try
        {
            await db.Database.ExecuteSqlRawAsync("select 1");
            var ms = Math.Round(sw.Elapsed.TotalMilliseconds, 1);
            Add("Server", "Database", ms > 200 ? "warn" : "ok", $"Answering in {ms} ms", ms > 200 ? "The database is slow to answer." : null);
        }
        catch { Add("Server", "Database", "bad", "Not answering", "Check the connection string in appsettings."); }
        cache.Set("health-check", "ok", TimeSpan.FromSeconds(10));
        Add("Server", "Cache", cache.Get<string>("health-check") == "ok" ? "ok" : "bad", "In memory");
        var writable = true;
        try { Directory.CreateDirectory(Storage); var probe = Path.Combine(Storage, ".health"); await System.IO.File.WriteAllTextAsync(probe, "ok"); System.IO.File.Delete(probe); }
        catch { writable = false; }
        Add("Server", "Storage", writable ? "ok" : "bad", writable ? "storage/ is writable" : "storage/ is not writable", writable ? null : "Give the web server write access to storage/.");
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Storage)!);
            var pct = (int)Math.Round(100.0 * drive.AvailableFreeSpace / drive.TotalSize);
            Add("Server", "Disk space", pct < 5 ? "bad" : pct < 15 ? "warn" : "ok", $"{Bytes(drive.AvailableFreeSpace)} free ({pct}%)", pct < 15 ? "Free up space on the server." : null);
        }
        catch { }
        var failed = (await db.JobRuns.ToListAsync()).GroupBy(r => r.Job).Count(g => !g.MaxBy(r => r.StartedAt)!.Ok);
        Add("Server", "Failed background jobs", failed > 0 ? "warn" : "ok", failed > 0 ? $"{failed} failed" : "None", failed > 0 ? "Sheriff → Crons shows them." : null);

        // Settings
        var prod = env.IsProduction();
        Add("Settings", "Debug mode", env.IsDevelopment() ? (prod ? "bad" : "warn") : "ok", env.IsDevelopment() ? "On: visitors would see error details" : "Off",
            env.IsDevelopment() ? "Run with ASPNETCORE_ENVIRONMENT=Production on the live site." : null);
        var site = config["Site:Url"] ?? "http://localhost:5100";
        var https = site.StartsWith("https://");
        Add("Settings", "HTTPS", https ? "ok" : prod ? "bad" : "warn", "The website is " + site, https ? null : "Use an https:// address on the live site.");
        var mail = !string.IsNullOrEmpty(config["Integrations:Smtp:Host"]);
        Add("Settings", "Email delivery", mail ? "ok" : "warn", mail ? "SMTP is set up" : "SMTP isn't set up", mail ? null : "Emails from the website (password resets, the contact form) are only logged.");
        foreach (var (name, key, used) in new[] { ("Stripe", "Integrations:Stripe:SecretKey", "Payments at sign-up and in My Account"), ("Experian", "Integrations:Experian:ApiKey", "Credit checks at sign-up") })
        {
            var ready = !string.IsNullOrEmpty(config[key]);
            Add("Settings", name, ready ? "ok" : "warn", ready ? "Set up in appsettings" : "Not set up in appsettings", ready ? null : used + " can't work until it is.");
        }

        // Content
        var pages = await db.Pages.ToListAsync();
        var drafts = pages.Count(p => p.Status != "Published");
        Add("Content", "Pages", "ok", $"{pages.Count - drafts} published" + (drafts > 0 ? $", {drafts} not published" : ""));
        var noDesc = pages.Count(p => p.Status == "Published" && p.Redirect == null && p.File == null && string.IsNullOrEmpty(p.MetaDescription));
        Add("Content", "Page descriptions", noDesc > 0 ? "warn" : "ok", noDesc > 0 ? $"{noDesc} published {(noDesc == 1 ? "page has" : "pages have")} no meta description" : "Every published page has one",
            noDesc > 0 ? "Search engines show a description under the link; add one in each page's settings." : null);
        var sitemap = Path.Combine(Storage, "sitemap.xml");
        if (!System.IO.File.Exists(sitemap)) Add("Content", "Sitemap", "bad", "sitemap.xml is missing", "Use Rebuild Sitemap on List Pages.");
        else
        {
            var built = System.IO.File.GetLastWriteTimeUtc(sitemap);
            var changed = pages.Count(p => p.UpdatedAt > built);
            Add("Content", "Sitemap", changed > 0 ? "warn" : "ok", $"Built {built.ToLocalTime():M/d/yyyy h:mm tt}" + (changed > 0 ? $"; {changed} {(changed == 1 ? "page" : "pages")} changed since" : ""),
                changed > 0 ? "Use Rebuild Sitemap on List Pages." : null);
        }

        // Traffic
        var blockedHour = await db.SiteVisits.CountAsync(v => v.Blocked && v.CreatedAt >= Now.AddHours(-1));
        Add("Traffic", "Blocked requests, last hour", blockedHour > 100 ? "warn" : "ok", $"{blockedHour} turned away", blockedHour > 100 ? "Someone keeps trying; see Visitors." : null);
        var lastView = await db.SiteVisits.Where(v => !v.Blocked).MaxAsync(v => (DateTime?)v.CreatedAt);
        var quiet = lastView == null || Now - lastView > TimeSpan.FromHours(6);
        Add("Traffic", "Last page view", quiet ? "warn" : "ok", lastView == null ? "None yet" : Ago(lastView.Value), quiet ? "No visits for hours: check that the site is up." : null);
        return checks;
    }

    private static string Bytes(long b) => b > 1L << 30 ? $"{b / (double)(1L << 30):0.0} GB" : $"{b / (double)(1 << 20):0} MB";
    private static string Ago(DateTime at)
    {
        var s = Now - at;
        return s.TotalMinutes < 1 ? "just now" : s.TotalHours < 1 ? $"{(int)s.TotalMinutes} minutes ago" : s.TotalDays < 1 ? $"{(int)s.TotalHours} hours ago" : $"{(int)s.TotalDays} days ago";
    }

    // ---- Visitors ---------------------------------------------------------------------------------------------

    [HttpGet("visitors")]
    public async Task<object> Visitors(string? who, DateOnly? start, DateOnly? end, string? ip, string? path, string? blocked, string? visitor, int page = 1)
    {
        var q = db.SiteVisits.AsQueryable();
        if (who == "online") q = q.Where(v => v.SeenAt >= OnlineSince && !v.Blocked);
        if (who == "customers") q = q.Where(v => v.CustomerId != null);
        if (start is { } s) q = q.Where(v => v.CreatedAt >= s.ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        if (end is { } e) q = q.Where(v => v.CreatedAt < e.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(ip)) q = ip.EndsWith('*') ? q.Where(v => v.Ip.StartsWith(ip.TrimEnd('*'))) : q.Where(v => v.Ip == ip.Trim());
        if (!string.IsNullOrWhiteSpace(path)) q = q.Where(v => v.Path.Contains(path.Trim().Trim('/')));
        if (blocked is "0" or "1") q = q.Where(v => v.Blocked == (blocked == "1"));
        if (!string.IsNullOrWhiteSpace(visitor)) q = q.Where(v => v.Visitor == visitor);
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(v => v.CreatedAt).Skip((Math.Max(page, 1) - 1) * 25).Take(25).ToListAsync();
        var names = await Names(rows.Select(v => v.CustomerId));
        var accounts = await db.Customers.Where(c => names.Keys.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Account);
        var ipBlocks = await db.SiteBlocks.Where(b => b.Type == "ip").ToListAsync();
        return new
        {
            total, page, online = await db.SiteVisits.Where(v => v.SeenAt >= OnlineSince && !v.Blocked).Select(v => v.Visitor).Distinct().CountAsync(),
            visits = rows.Select(v => new
            {
                v.Id, v.CreatedAt, v.SeenAt, v.Path, v.PageId, referrer = Host(v.Referrer), v.Ip, v.Visitor, v.CustomerId,
                customer = v.CustomerId is { } c ? names.GetValueOrDefault(c) : null, account = v.CustomerId is { } a ? accounts.GetValueOrDefault(a) : null,
                v.Blocked, on = v.SeenAt >= OnlineSince, block = ipBlocks.FirstOrDefault(b => b.MatchesIp(v.Ip))?.Id,
            }),
        };
    }

    private static string? Host(string? url) => url == null ? null : Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;

    // ---- Blocked IPs and areas --------------------------------------------------------------------------------

    private static string? Type(string kind) => kind switch { "ips" => "ip", "areas" => "area", _ => null };

    [HttpGet("blocked/{kind}")]
    public async Task<IActionResult> Blocked(string kind)
    {
        if (Type(kind) is not { } type) return NotFound();
        var blocks = await db.SiteBlocks.Where(b => b.Type == type).OrderByDescending(b => b.Active).ThenByDescending(b => b.CreatedAt).ToListAsync();
        var users = await db.Users.ToDictionaryAsync(u => u.Id, u => u.Name);
        var visits = type == "ip" ? await db.SiteVisits.Where(v => blocks.Select(b => b.Value).Contains(v.Ip)).GroupBy(v => v.Ip)
            .Select(g => new { ip = g.Key, at = g.Max(v => v.CreatedAt), n = g.Count() }).ToListAsync() : [];
        var paths = type == "area" ? await db.Pages.Select(p => p.Path).ToListAsync() : [];
        return Ok(blocks.Select(b => new
        {
            b.Id, b.Value, b.Reason, by = b.UserId is { } u ? users.GetValueOrDefault(u) : null, b.CreatedAt, b.ExpiresAt, b.Hits, b.LastHitAt, b.Active, inForce = b.InForce,
            lastSeen = visits.FirstOrDefault(v => v.ip == b.Value), covers = paths.Count(b.MatchesPath),
        }));
    }

    [HttpGet("blocked/{kind}/form")]
    public async Task<IActionResult> BlockForm(string kind, int? id)
    {
        if (Type(kind) is not { } type) return NotFound();
        var b = id == null ? null : await db.SiteBlocks.FirstOrDefaultAsync(x => x.Id == id && x.Type == type);
        if (id != null && b == null) return NotFound();
        object? views = null;
        if (b != null)
        {
            var since = Now.AddDays(-30);
            var q = db.SiteVisits.Where(v => v.CreatedAt >= since);
            var list = type == "ip"
                ? b.Value.EndsWith('*') ? await q.Where(v => v.Ip.StartsWith(b.Value.TrimEnd('*'))).ToListAsync() : await q.Where(v => v.Ip == b.Value).ToListAsync()
                : (await q.Where(v => v.Path == b.Value || v.Path.StartsWith(b.Value + "/")).ToListAsync());
            if (list.Count > 0) views = new { n = list.Count, people = list.Select(v => v.Visitor).Distinct().Count(), last = list.Max(v => v.CreatedAt) };
        }
        return Ok(new { block = b, views });
    }

    public record BlockInput(string? Value, string? Reason, string? Message, DateTime? ExpiresAt, bool Active);

    [HttpPost("blocked/{kind}")]
    public Task<IActionResult> AddBlock(string kind, BlockInput input) => SaveBlock(kind, null, input);

    [HttpPut("blocked/{kind}/{id:int}")]
    public Task<IActionResult> UpdateBlock(string kind, int id, BlockInput input) => SaveBlock(kind, id, input);

    private async Task<IActionResult> SaveBlock(string kind, int? id, BlockInput input)
    {
        if (Type(kind) is not { } type) return NotFound();
        var b = id == null ? new SiteBlock { Type = type, UserId = (await Me()).Id } : await db.SiteBlocks.FirstOrDefaultAsync(x => x.Id == id && x.Type == type);
        if (b == null) return NotFound();
        var value = (input.Value ?? "").Trim();
        if (type == "area") value = value.Trim('/');
        if (value == "") return BadRequest(new { error = type == "ip" ? "Enter the IP address." : "Enter the area's path." });
        if (type == "ip" && !Regex.IsMatch(value, @"^[0-9a-fA-F:.]+\*?$")) return BadRequest(new { error = "That isn't an IP address (end with * for a range)." });
        if (type == "area" && (value.Split('/')[0] is "admin" || value == "")) return BadRequest(new { error = "The home page and the admin can't be blocked." });
        if (await db.SiteBlocks.AnyAsync(x => x.Type == type && x.Value == value && x.Id != b.Id)) return BadRequest(new { error = $"{value} is already on the list." });
        (b.Value, b.Reason, b.Message, b.ExpiresAt, b.Active) = (value, Blank(input.Reason), Blank(input.Message), input.ExpiresAt?.ToUniversalTime(), input.Active);
        if (id == null) db.SiteBlocks.Add(b);
        await db.SaveChangesAsync();
        return Ok(new { b.Id, message = id == null ? $"{value} blocked" : "Saved" });
    }

    [HttpPost("blocked/{kind}/{id:int}/toggle")]
    public async Task<IActionResult> ToggleBlock(string kind, int id)
    {
        var b = await db.SiteBlocks.FirstOrDefaultAsync(x => x.Id == id && x.Type == Type(kind));
        if (b == null) return NotFound();
        b.Active = !b.Active;
        await db.SaveChangesAsync();
        return Ok(new { message = b.Active ? $"{b.Value} blocked" : $"{b.Value} unblocked" });
    }

    [HttpDelete("blocked/{kind}/{id:int}")]
    public async Task<IActionResult> RemoveBlock(string kind, int id)
    {
        var b = await db.SiteBlocks.FirstOrDefaultAsync(x => x.Id == id && x.Type == Type(kind));
        if (b == null) return NotFound();
        db.SiteBlocks.Remove(b);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{b.Value} removed" });
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ---- Cookie consents --------------------------------------------------------------------------------------

    [HttpGet("consents")]
    public async Task<object> Consents(string? who, string? choice, string? q, int page = 1)
    {
        var recent = await db.CookieConsents.Where(k => k.CreatedAt >= Now.AddDays(-30)).ToListAsync();
        var list = db.CookieConsents.AsQueryable();
        if (who == "customer") list = list.Where(k => k.CustomerId != null);
        if (who == "employee") list = list.Where(k => k.UserId != null);
        if (who == "anonymous") list = list.Where(k => k.CustomerId == null && k.UserId == null);
        if (choice is "all" or "necessary" or "custom") list = list.Where(k => k.Choice == choice);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            var ids = await db.Customers.Where(c => c.Account.Contains(t) || c.Name.Contains(t)).Select(c => (int?)c.Id).ToListAsync();
            list = list.Where(k => ids.Contains(k.CustomerId) || (k.Ip != null && k.Ip.Contains(t)) || k.Visitor.Contains(t));
        }
        var total = await list.CountAsync();
        var rows = await list.OrderByDescending(k => k.CreatedAt).Skip((Math.Max(page, 1) - 1) * 25).Take(25).ToListAsync();
        var customers = await db.Customers.Where(c => rows.Select(k => k.CustomerId).Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.Account, c.Name });
        var users = await db.Users.Where(u => rows.Select(k => k.UserId).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
        return new
        {
            all = recent.Count, totals = recent.GroupBy(k => k.Choice).ToDictionary(g => g.Key, g => g.Count()),
            analytics = recent.Count(k => k.Analytics), marketing = recent.Count(k => k.Marketing),
            cookies = CookieConsent.Cookies.Select(c => new { c.Name, c.Category, c.Purpose, c.Lasts }), policy = CookieConsent.PolicyNow,
            total, page,
            consents = rows.Select(k => new
            {
                k.Id, k.CreatedAt, k.Choice, label = k.Label, allowed = k.Allowed(), k.Page, k.Ip, k.Visitor, k.CustomerId,
                account = k.CustomerId is { } c ? customers.GetValueOrDefault(c)?.Account : null, customer = k.CustomerId is { } d ? customers.GetValueOrDefault(d)?.Name : null,
                user = k.UserId is { } u ? users.GetValueOrDefault(u) : null,
            }),
        };
    }

    [HttpGet("consents/{id:int}")]
    public async Task<IActionResult> Consent(int id)
    {
        var k = await db.CookieConsents.FindAsync(id);
        if (k is null) return NotFound();
        var customer = k.CustomerId == null ? null : await db.Customers.Where(c => c.Id == k.CustomerId).Select(c => new { c.Id, c.Account, c.Name }).FirstOrDefaultAsync();
        var user = k.UserId == null ? null : await db.Users.Where(u => u.Id == k.UserId).Select(u => u.Name).FirstOrDefaultAsync();
        var jar = string.IsNullOrEmpty(k.CookieJar) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(k.CookieJar)!;
        var known = CookieConsent.Cookies.Select(c => c.Name).ToHashSet();
        var allowed = k.Allowed();
        return Ok(new
        {
            consent = new { k.Id, k.Choice, label = k.Label, k.Analytics, k.Marketing, k.PolicyVersion, current = k.PolicyVersion == CookieConsent.PolicyNow, k.Ip, k.UserAgent, k.Page, k.Visitor, k.CreatedAt, k.CookiesSeenAt },
            customer, user, allowed, jar, unknown = jar.Where(j => !known.Contains(j.Key)).ToDictionary(),
            unexpected = jar.Keys.Where(n => known.Contains(n) && !allowed.Contains(n)),
            cookies = CookieConsent.Cookies.Select(c => new { c.Name, c.Category, c.Purpose, c.Lasts }),
            others = await db.CookieConsents.Where(x => x.Id != id && (x.Visitor == k.Visitor || (k.CustomerId != null && x.CustomerId == k.CustomerId)))
                .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.CreatedAt, x.Choice, x.PolicyVersion }).ToListAsync(),
            visits = await db.SiteVisits.Where(v => v.Visitor == k.Visitor).OrderByDescending(v => v.CreatedAt).Take(50).Select(v => new { v.Id, v.CreatedAt, v.Path, v.Ip }).ToListAsync(),
        });
    }
}
