using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Data;

namespace RcBunny.Frontend.Services;

/// <summary>When Sheriff → Heartbeat has the website switched off, every page shows a "back soon" notice instead.</summary>
public class Maintenance(RequestDelegate next, IMemoryCache cache)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/css") || path.StartsWith("/img") || path.StartsWith("/js")) { await next(context); return; }

        var control = await cache.GetOrCreateAsync("maintenance", e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return db.HeartbeatControls.AsNoTracking().FirstOrDefaultAsync(c => c.Key == "website");
        });
        if (control is not { Down: true }) { await next(context); return; }

        context.Response.StatusCode = 503;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync($$"""
            <!DOCTYPE html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Back soon | {{Site.Name}}</title><link rel="stylesheet" href="/css/tokens.css"><link rel="stylesheet" href="/css/site.css"></head>
            <body><section class="section"><div class="wrap" style="max-width:640px;text-align:center">
            <h1>We'll be right back.</h1><p>{{System.Net.WebUtility.HtmlEncode(control.Reason)}}</p>
            <p>Need help now? Call <a href="{{Site.PhoneLink}}">{{Site.Phone}}</a>.</p></div></section></body></html>
            """);
    }
}
