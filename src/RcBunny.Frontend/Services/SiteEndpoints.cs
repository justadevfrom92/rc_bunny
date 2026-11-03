using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Frontend.Services;

/// <summary>The few non-page endpoints the website needs.</summary>
public static class SiteEndpoints
{
    public record ConsentRequest(string Choice, bool Analytics, bool Marketing, string? Page);
    public record ChatRequest(string Message);

    public static void MapSiteEndpoints(this WebApplication app)
    {
        // Cookie banner answer (Lando → Cookie Consents). Same-site JSON with a custom header (no cross-site posts).
        app.MapPost("/site/consent", async (HttpContext ctx, ConsentRequest r, AppDbContext db) =>
        {
            if (ctx.Request.Headers["X-RCB"] != "1") return Results.BadRequest();
            var (analytics, marketing) = r.Choice switch { "all" => (true, true), "necessary" => (false, false), _ => (r.Analytics, r.Marketing) };
            db.CookieConsents.Add(new CookieConsent
            {
                Visitor = ctx.Request.Cookies[SiteGuard.VisitorCookie] ?? Guid.NewGuid().ToString("N"), Choice = r.Choice is "all" or "necessary" ? r.Choice : "custom",
                Analytics = analytics, Marketing = marketing, PolicyVersion = "2026-01", Ip = ctx.Connection.RemoteIpAddress?.ToString(),
                UserAgent = ctx.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua[..Math.Min(245, ua.Length)] : null, Page = r.Page,
                CustomerId = CustomerId(ctx), CookieJar = new JsonObject(ctx.Request.Cookies.Select(c => new KeyValuePair<string, JsonNode?>(c.Key, c.Key.StartsWith("rcb.") ? "(set)" : c.Value))),
                CookiesSeenAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            ctx.Response.Cookies.Append("et_consent", $"{(analytics ? 1 : 0)}{(marketing ? 1 : 0)}", new CookieOptions { MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax, IsEssential = true });
            return Results.Ok(new { analytics, marketing });
        }).RequireRateLimiting("forms");

        app.MapGet("/sitemap.xml", async (HttpContext ctx, AppDbContext db) =>
        {
            var host = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var paths = new[] { "", "plans", "build-your-own-plan", "business", "contact-us", "shop" }
                .Concat(await db.Pages.Where(p => p.Status == "Published" && p.Redirect == null && !p.NoIndex && p.File == null && !p.Path.StartsWith("checkout") && !p.Path.StartsWith("myaccount")).Select(p => p.Path).ToListAsync())
                .Concat((await db.Plans.Where(p => p.Active).Select(p => p.Slug).ToListAsync()).Select(s => "plans/" + s)).Distinct();
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
            foreach (var p in paths) sb.Append($"  <url><loc>{host}/{System.Security.SecurityElement.Escape(p.Trim('/'))}</loc></url>\n");
            return Results.Text(sb.Append("</urlset>\n").ToString(), "application/xml");
        }).CacheOutput(p => p.Expire(TimeSpan.FromMinutes(10)));

        app.MapGet("/robots.txt", (HttpContext ctx) => Results.Text($"User-agent: *\nDisallow: /myaccount\nDisallow: /checkout\nSitemap: {ctx.Request.Scheme}://{ctx.Request.Host}/sitemap.xml\n"));

        // My Account chat bubble: the Deputy "chat" agent answers until a person takes over (Deputy → Conversations).
        var chat = app.MapGroup("/myaccount/chat").RequireAuthorization();
        chat.MapGet("", async (HttpContext ctx, AppDbContext db) =>
        {
            var c = await Open(db, CustomerId(ctx)!.Value, create: false);
            return Results.Ok(new { status = c?.Status ?? "none", lines = Lines(c?.Transcript) });
        });
        chat.MapPost("", async (HttpContext ctx, ChatRequest r, AppDbContext db, IConfiguration config) =>
        {
            if (ctx.Request.Headers["X-RCB"] != "1" || string.IsNullOrWhiteSpace(r.Message)) return Results.BadRequest();
            var cid = CustomerId(ctx)!.Value;
            var conv = await Open(db, cid, create: true);
            var msg = r.Message.Trim()[..Math.Min(1000, r.Message.Trim().Length)];
            conv!.Transcript += $"\nCustomer: {msg}";
            conv.LastMessageAt = DateTime.UtcNow;
            if (conv.Status == "bot")
            {
                var reply = await BotReply(db, conv, msg, config);
                if (reply.Contains("HANDOFF") || msg.Contains("person", StringComparison.OrdinalIgnoreCase) || msg.Contains("agent", StringComparison.OrdinalIgnoreCase))
                {
                    conv.Status = "waiting";
                    conv.Transcript += "\nBot: I'm getting a person for you now. They'll see everything we covered.";
                }
                else conv.Transcript += $"\nBot: {reply}";
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { status = conv.Status, lines = Lines(conv.Transcript) });
        }).RequireRateLimiting("chat");
        chat.MapPost("/person", async (HttpContext ctx, AppDbContext db) =>
        {
            if (ctx.Request.Headers["X-RCB"] != "1") return Results.BadRequest();
            var conv = await Open(db, CustomerId(ctx)!.Value, create: true);
            if (conv!.Status == "bot") { conv.Status = "waiting"; conv.Transcript += "\nBot: I'm getting a person for you now."; }
            await db.SaveChangesAsync();
            return Results.Ok(new { status = conv.Status, lines = Lines(conv.Transcript) });
        });
    }

    public static int? CustomerId(HttpContext ctx) => int.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static List<object> Lines(string? transcript) => (transcript ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(l => { var i = l.IndexOf(": ", StringComparison.Ordinal); return (object)new { who = i > 0 ? l[..i] : "System", text = i > 0 ? l[(i + 2)..] : l }; }).ToList();

    private static async Task<AiConversation?> Open(AppDbContext db, int customerId, bool create)
    {
        var idle = DateTime.UtcNow.AddMinutes(-(AppConfig.At("deputy.chat_idle_minutes")?.GetValue<int>() ?? 30));
        var conv = await db.AiConversations.AsTracking().Where(c => c.CustomerId == customerId && c.Channel == "chat" && c.Status != "closed" && c.LastMessageAt > idle).OrderByDescending(c => c.Id).FirstOrDefaultAsync();
        if (conv != null || !create) return conv;
        var agent = await db.AiAgents.FirstOrDefaultAsync(a => a.Activity == "chat" && a.Active) ?? await db.AiAgents.FirstAsync();
        var first = await db.Customers.Where(c => c.Id == customerId).Select(c => c.FirstName).FirstAsync();
        conv = new AiConversation { AiAgentId = agent.Id, AiModelId = agent.AiModelId, CustomerId = customerId, Channel = "chat", Topic = "My Account chat", Outcome = "open", Status = "bot", StartedAt = DateTime.UtcNow, LastMessageAt = DateTime.UtcNow, Transcript = $"Bot: Hi {first}, I'm the virtual assistant. How can I help?" };
        db.AiConversations.Add(conv);
        return conv;
    }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Asks the agent's model (Deputy:OllamaUrl / Deputy:HostedUrl); falls back to short answers about the account.</summary>
    private static async Task<string> BotReply(AppDbContext db, AiConversation conv, string msg, IConfiguration config)
    {
        var agent = await db.AiAgents.Include(a => a.AiModel).FirstAsync(a => a.Id == conv.AiAgentId);
        var cust = await db.Customers.Include(c => c.Plan).FirstAsync(c => c.Id == conv.CustomerId);
        var context = $"Customer: {cust.Name}, account {cust.Account}, balance {cust.Balance:C}, due {cust.DueDate:MMM d}, plan {cust.Plan?.Name}, {cust.Stars} reward stars.";
        try
        {
            var m = agent.AiModel!;
            var local = m.Provider == "local";
            var url = local ? (config["Deputy:OllamaUrl"] ?? "http://localhost:11434") + "/api/chat" : (config["Deputy:HostedUrl"] ?? "https://api.together.xyz/v1") + "/chat/completions";
            var messages = new[] { new { role = "system", content = agent.SystemPrompt + "\n" + context }, new { role = "user", content = msg } };
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(local ? new { model = m.ModelId, stream = false, messages } : (object)new { model = m.ModelId, messages, max_tokens = agent.MaxTokens }) };
            if (!local && config["Deputy:HostedApiKey"] is { } key) req.Headers.Authorization = new("Bearer", key);
            using var res = await Http.SendAsync(req);
            var json = JsonNode.Parse(await res.Content.ReadAsStringAsync());
            var text = (local ? json?["message"]?["content"] : json?["choices"]?[0]?["message"]?["content"])?.ToString();
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { }
        var t = msg.ToLowerInvariant();
        return t switch
        {
            _ when t.Contains("balance") || t.Contains("owe") || t.Contains("bill") => $"Your balance is {cust.Balance:C}, due {cust.DueDate:MMMM d}. You can pay it under Bills & Payments → Pay My Bill.",
            _ when t.Contains("star") || t.Contains("reward") => $"You have {cust.Stars:N0} stars. Spend them under Rewards.",
            _ when t.Contains("plan") || t.Contains("renew") || t.Contains("rate") => $"You're on {cust.Plan?.Name ?? "your current plan"}. See renewal options under Plan & Services.",
            _ when t.Contains("outage") || t.Contains("power out") => "Outages are handled by your utility. Call their outage line; I can also get a person for you.",
            _ when t.Contains("move") || t.Contains("transfer") => "You can move your service under Plan & Services → Transfer Service.",
            _ => "I can help with your bill, payments, plan, rewards or moving. Or ask for a person and I'll get one.",
        };
    }
}
