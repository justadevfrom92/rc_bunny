using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Backend.Resources;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend;

/// <summary>Rodeo campaigns: who's in the audience, and sending (logged until Salesforce/Twilio keys are set).</summary>
public static class Campaigns
{
    /// <summary>
    /// Audience JSON (as built in Rodeo): statuses[], types[], markets[], phone_types[], without_product, with_product,
    /// autopay (bool), paperless (bool). Marketing-opt-out customers and suppressed addresses are always left out.
    /// </summary>
    public static IQueryable<Customer> Audience(AppDbContext db, JsonNode? audience, string channel)
    {
        var a = audience as JsonObject ?? new JsonObject();
        var q = db.Customers.Where(c => c.MarketingOptIn);
        List<string> L(string k) => a[k] is JsonArray arr ? arr.Select(x => x!.ToString()).ToList() : [];
        if (L("statuses") is { Count: > 0 } st) q = q.Where(c => st.Contains(c.Status));
        if (L("types") is { Count: > 0 } ty) q = q.Where(c => ty.Contains(c.Type));
        if (L("phone_types") is { Count: > 0 } pt) q = q.Where(c => c.PhoneType != null && pt.Contains(c.PhoneType));
        if (L("markets") is { Count: > 0 } mk) q = q.Where(c => c.Market != null && mk.Contains(c.Market.Name));
        if (a["without_product"]?.ToString() is { Length: > 0 } wp) q = q.Where(c => !db.CustomerProducts.Any(p => p.CustomerId == c.Id && p.Product == wp && p.RemovedAt == null));
        if (a["with_product"]?.ToString() is { Length: > 0 } hp) q = q.Where(c => db.CustomerProducts.Any(p => p.CustomerId == c.Id && p.Product == hp && p.RemovedAt == null));
        if (a["autopay"] is JsonValue ap && ap.TryGetValue<bool>(out var apv)) q = q.Where(c => c.Autopay == apv);
        if (a["paperless"] is JsonValue pp && pp.TryGetValue<bool>(out var ppv)) q = q.Where(c => c.Paperless == ppv);
        q = channel == "SMS" ? q.Where(c => c.Phone != null) : q.Where(c => c.Email != null && !db.EmailSuppressions.Any(s => s.Email == c.Email));
        return q;
    }

    public static async Task<string> SendAsync(AppDbContext db, Campaign campaign)
    {
        var template = campaign.EmailTemplateId is { } tid ? await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == tid) : null;
        if (campaign.Channel == "Email" && template is null) throw new ActionError("Pick an email template first.");
        if (campaign.Channel == "SMS" && string.IsNullOrWhiteSpace(campaign.Message)) throw new ActionError("Write the text message first.");
        var people = await Audience(db, campaign.Audience, campaign.Channel).Include(c => c.Plan).ToListAsync();
        var since = DateTime.UtcNow.AddDays(-Math.Max(0, campaign.CapDays));
        var recent = campaign.CapDays > 0
            ? (await db.ContactLogs.Where(l => l.CampaignId != null && l.CreatedAt >= since).Select(l => l.CustomerId).ToListAsync()).ToHashSet()
            : [];
        var live = Environment.GetEnvironmentVariable(campaign.Channel == "SMS" ? "TWILIO_SID" : "SALESFORCE_CLIENT_ID") != null;
        int sent = 0, skipped = 0;
        foreach (var c in people)
        {
            if (recent.Contains(c.Id)) { skipped++; continue; }
            db.ContactLogs.Add(new ContactLog
            {
                CustomerId = c.Id, CampaignId = campaign.Id, Channel = campaign.Channel, Template = template?.Name ?? campaign.Name,
                Body = campaign.Channel == "SMS" ? TemplateText.Render(campaign.Message!, c, c.Plan?.Name) : null,
                Status = live ? "sent" : "logged", SentAt = DateTime.UtcNow, Direction = "out", Phone = campaign.Channel == "SMS" ? c.Phone : null,
            });
            sent++;
        }
        campaign.Status = "sent"; campaign.SentAt = DateTime.UtcNow; campaign.Recipients = sent; campaign.Skipped = skipped;
        return $"{(live ? "Sent" : "Logged (no Salesforce/Twilio keys yet)")} to {sent:N0} customers{(skipped > 0 ? $"; {skipped} skipped (messaged in the last {campaign.CapDays} days)" : "")}.";
    }
}

/// <summary>Rodeo PDFs: fill a template for one customer and keep a copy.</summary>
public static class Pdfs
{
    public static async Task<string> MakeAsync(AppDbContext db, PdfTemplate t, string account, int userId)
    {
        var c = await db.Customers.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Account == account) ?? throw new ActionError("No account with that number.");
        var (bytes, pages, body) = await RenderAsync(db, t, c);
        db.PdfGenerations.Add(new PdfGeneration
        {
            PdfTemplateId = t.Id, CustomerId = c.Id, UserId = userId, FileName = $"{t.Name.ToLowerInvariant().Replace(' ', '-')}-{c.Account}.pdf",
            Pages = pages, Size = bytes.Length, Title = TemplateText.Render(t.Title, c, c.Plan?.Name), Body = body, Footer = t.Footer, ShowAddress = t.ShowAddress,
        });
        return $"PDF made for {c.Name}; it's under PDFs Made.";
    }

    public static async Task<(byte[] Bytes, int Pages, string Body)> RenderAsync(AppDbContext db, PdfTemplate t, Customer? c)
    {
        var header = t.PdfHeaderId is { } h ? await db.PdfParts.FirstOrDefaultAsync(p => p.Id == h) : await db.PdfParts.FirstOrDefaultAsync(p => p.Kind == "header" && p.IsDefault);
        var footer = t.PdfFooterId is { } f ? await db.PdfParts.FirstOrDefaultAsync(p => p.Id == f) : await db.PdfParts.FirstOrDefaultAsync(p => p.Kind == "footer" && p.IsDefault);
        var body = TemplateText.Render(t.Body, c, c?.Plan?.Name);
        var pdf = SimplePdf.Letter(header?.Title, header?.Subtitle, header?.Color ?? "#102247",
            t.ShowAddress && c != null ? $"{c.Name}\n{c.Address}\n{c.City}, TX {c.Zip}" : null,
            TemplateText.Render(t.Title, c, c?.Plan?.Name), body + (t.Footer is { } tf ? "\n\n" + TemplateText.Render(tf, c) : ""), footer?.Body);
        return (pdf.Output(t.Name), pdf.PageCount, body);
    }
}

/// <summary>Deputy: talks to the configured model server (Ollama locally, an OpenAI-compatible endpoint when hosted).</summary>
public static class Deputy
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(25) };

    private static (string Url, string? Key) Endpoint(IConfiguration config, AiModel m) => m.Provider == "local"
        ? (config["Deputy:OllamaUrl"] ?? "http://localhost:11434", null)
        : (config["Deputy:HostedUrl"] ?? "https://api.together.xyz/v1", config["Deputy:HostedApiKey"]);

    public static async Task<string> CheckAsync(ActionContext c, AiModel m)
    {
        var config = c.Services.GetRequiredService<IConfiguration>();
        var (url, key) = Endpoint(config, m);
        m.CheckedAt = DateTime.UtcNow;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, m.Provider == "local" ? $"{url}/api/tags" : $"{url}/models");
            if (key != null) req.Headers.Authorization = new("Bearer", key);
            using var res = await Http.SendAsync(req);
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) { m.Status = "error"; return $"The server answered {(int)res.StatusCode}."; }
            m.Status = text.Contains(m.ModelId) ? "available" : "offline";
            return m.Status == "available" ? $"{m.Name} is available at {url}." : $"Connected to {url}, but {m.ModelId} isn't installed there.";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            m.Status = "offline";
            return $"Couldn't reach {url} ({(m.Provider == "local" ? "is Ollama running? Set Deputy:OllamaUrl" : "set Deputy:HostedUrl and Deputy:HostedApiKey")}).";
        }
    }

    public static async Task<string> TestAsync(ActionContext c, AiAgent agent, string message)
    {
        var model = await c.Db.AiModels.FirstAsync(x => x.Id == agent.AiModelId);
        var config = c.Services.GetRequiredService<IConfiguration>();
        var (url, key) = Endpoint(config, model);
        var started = DateTime.UtcNow;
        string reply, outcome = "resolved";
        try
        {
            object body = model.Provider == "local"
                ? new { model = model.ModelId, stream = false, options = new { temperature = agent.Temperature }, messages = new[] { new { role = "system", content = agent.SystemPrompt }, new { role = "user", content = message } } }
                : new { model = model.ModelId, temperature = agent.Temperature, max_tokens = agent.MaxTokens, messages = new[] { new { role = "system", content = agent.SystemPrompt }, new { role = "user", content = message } } };
            using var req = new HttpRequestMessage(HttpMethod.Post, model.Provider == "local" ? $"{url}/api/chat" : $"{url}/chat/completions") { Content = JsonContent.Create(body) };
            if (key != null) req.Headers.Authorization = new("Bearer", key);
            using var res = await Http.SendAsync(req);
            var json = JsonNode.Parse(await res.Content.ReadAsStringAsync());
            reply = (model.Provider == "local" ? json?["message"]?["content"] : json?["choices"]?[0]?["message"]?["content"])?.ToString() ?? $"(no answer; status {(int)res.StatusCode})";
            if (reply.Contains("HANDOFF")) outcome = "handed_off";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            reply = $"(Model not reachable at {url}: this is where {model.Name} would answer.)";
            outcome = "failed";
        }
        c.Db.AiConversations.Add(new AiConversation
        {
            AiAgentId = agent.Id, AiModelId = model.Id, Channel = "test", Topic = "Test", Outcome = outcome, UserId = c.Me.UserId, Status = "closed",
            Transcript = $"Customer: {message}\nBot: {reply}", StartedAt = started, LastMessageAt = DateTime.UtcNow, DurationSec = (int)(DateTime.UtcNow - started).TotalSeconds,
        });
        return reply;
    }
}
