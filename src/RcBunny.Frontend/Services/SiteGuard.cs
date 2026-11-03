using System.Net;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Frontend.Services;

/// <summary>
/// In front of every page: blocked IPs and blocked areas (Lando), maintenance mode (Sheriff → Heartbeat),
/// and the page view for Lando → Visitors. Blocks are cached for 30 seconds; visits are written in the background,
/// so none of this slows the page.
/// </summary>
public sealed class SiteGuard(RequestDelegate next, IMemoryCache cache, IServiceScopeFactory scopes, VisitWriter visits)
{
    public const string VisitorCookie = "et_vid";
    private sealed record Rules(List<SiteBlock> Blocks, bool Maintenance, string? Reason);

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "/";
        if (!HttpMethods.IsGet(ctx.Request.Method) || IsAsset(path)) { await next(ctx); return; }

        var rules = await cache.GetOrCreateAsync("site:rules", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var blocks = await db.SiteBlocks.Where(b => b.Active && (b.ExpiresAt == null || b.ExpiresAt > now)).ToListAsync();
            var ctl = await db.HeartbeatControls.FirstOrDefaultAsync(c => c.Key == "website");
            return new Rules(blocks, ctl?.State == "down", ctl?.Reason);
        }) ?? new Rules([], false, null);

        var ip = ctx.Connection.RemoteIpAddress;
        var block = rules.Blocks.FirstOrDefault(b => b.Type == "ip" && ip != null && IpMatches(b.Value, ip))
            ?? rules.Blocks.FirstOrDefault(b => b.Type == "area" && path.StartsWith("/" + b.Value.Trim('/'), StringComparison.OrdinalIgnoreCase));
        var visitor = ctx.Request.Cookies[VisitorCookie];
        if (visitor is null || visitor.Length is < 20 or > 40)
        {
            visitor = Guid.NewGuid().ToString("N");
            ctx.Response.Cookies.Append(VisitorCookie, visitor, new CookieOptions { MaxAge = TimeSpan.FromDays(365), HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true });
        }
        visits.Add(new SiteVisit
        {
            Visitor = visitor, Ip = ip?.ToString() ?? "", Path = path.Trim('/'), Referrer = Trim(ctx.Request.Headers.Referer.ToString(), 250),
            Agent = Trim(ctx.Request.Headers.UserAgent.ToString(), 250), Blocked = block != null, SeenAt = DateTime.UtcNow,
            CustomerId = int.TryParse(ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var cid) ? cid : null,
        }, block?.Id);

        if (block != null)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Message(ctx, "Not available", block.Message ?? "This page isn't available.");
            return;
        }
        // Maintenance: everyone sees the notice except My Account sign-in pages (so staff can still check things)
        if (rules.Maintenance && !path.StartsWith("/healthz"))
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ctx.Response.Headers.RetryAfter = "600";
            await Message(ctx, "We'll be right back", "The website is down for maintenance. Please try again in a few minutes, or call us." + (rules.Reason is { Length: > 0 } r ? $" ({WebUtility.HtmlEncode(r)})" : ""));
            return;
        }
        await next(ctx);
    }

    private static string? Trim(string s, int n) => string.IsNullOrEmpty(s) ? null : s.Length > n ? s[..n] : s;

    private static bool IsAsset(string p) =>
        p.StartsWith("/css/") || p.StartsWith("/img/") || p.StartsWith("/islands/") || p.StartsWith("/site/") || p.StartsWith("/myaccount/chat") || p.Contains('.') && !p.EndsWith(".html");

    /// <summary>"203.0.113.66" or a CIDR range like "192.0.2.0/24".</summary>
    public static bool IpMatches(string rule, IPAddress ip)
    {
        if (!rule.Contains('/')) return IPAddress.TryParse(rule, out var one) && one.Equals(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);
        var parts = rule.Split('/');
        if (!IPAddress.TryParse(parts[0], out var net) || !int.TryParse(parts[1], out var bits)) return false;
        var a = (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).GetAddressBytes(); var b = net.GetAddressBytes();
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length && bits > 0; i++, bits -= 8)
        {
            var mask = bits >= 8 ? 0xFF : (byte)(0xFF << (8 - bits));
            if ((a[i] & mask) != (b[i] & mask)) return false;
        }
        return true;
    }

    private static Task Message(HttpContext ctx, string title, string body)
    {
        ctx.Response.ContentType = "text/html; charset=utf-8";
        return ctx.Response.WriteAsync($"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>{title}</title><link rel=\"stylesheet\" href=\"/css/tokens.css\"><link rel=\"stylesheet\" href=\"/css/site.css\"></head><body><main class=\"section\"><div class=\"wrap\" style=\"max-width:640px;text-align:center;padding:80px 0\"><h1>{title}</h1><p>{WebUtility.HtmlEncode(body)}</p></div></main></body></html>");
    }
}

/// <summary>Writes page views (and block hits) in batches off the request path.</summary>
public sealed class VisitWriter(IServiceScopeFactory scopes, ILogger<VisitWriter> log) : BackgroundService
{
    private readonly Channel<(SiteVisit Visit, int? BlockId)> queue = Channel.CreateBounded<(SiteVisit, int?)>(new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Add(SiteVisit v, int? blockId) => queue.Writer.TryWrite((v, blockId));

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (await queue.Reader.WaitToReadAsync(stop))
        {
            var batch = new List<(SiteVisit, int?)>();
            while (batch.Count < 200 && queue.Reader.TryRead(out var x)) batch.Add(x);
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pages = await db.Pages.Select(p => new { p.Id, p.Path }).ToDictionaryAsync(p => p.Path, p => p.Id, stop);
                foreach (var (v, _) in batch) { v.PageId = pages.TryGetValue(v.Path, out var id) ? id : pages.TryGetValue(v.Path == "" ? "/" : v.Path, out var id2) ? id2 : null; db.SiteVisits.Add(v); }
                foreach (var g in batch.Where(b => b.Item2 != null).GroupBy(b => b.Item2!.Value))
                    await db.SiteBlocks.Where(b => b.Id == g.Key).ExecuteUpdateAsync(s => s.SetProperty(b => b.Hits, b => b.Hits + g.Count()).SetProperty(b => b.LastHitAt, DateTime.UtcNow), stop);
                await db.SaveChangesAsync(stop);
            }
            catch (Exception e) when (!stop.IsCancellationRequested) { log.LogWarning(e, "Couldn't save page views"); }
        }
    }
}
