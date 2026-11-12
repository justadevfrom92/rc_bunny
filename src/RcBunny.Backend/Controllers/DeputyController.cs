using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// Deputy: AI agents on open-weight models, the same screens as ebunny's Deputy. Which model each agent runs on (downloaded
/// to the local server or hosted behind an OpenAI-compatible API); conversations with customers by text and in the My
/// Account chat, where a person can take over, reply or call; and a place to test an agent. (Scheduled automations are
/// Sheriff's.)
/// </summary>
[ApiController]
[Route("api/deputy")]
[RequirePermission("deputy")]
public class DeputyController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private async Task<User> Me() => (await db.Users.FindAsync((await Permissions.CurrentAsync(HttpContext))!.Id))!;

    private static object? ModelRow(AiModel? m) => m == null ? null : new { m.Id, m.Name, m.ModelId, local = m.IsLocal, m.Provider, providerLabel = m.ProviderLabel, m.Status };
    private static object CustomerRow(Customer? c) => c == null ? null! : new { c.Id, c.Account, c.Name, c.Phone };

    private IQueryable<AiConversation> Conversations => db.AiConversations.Include(c => c.Agent).Include(c => c.Model).Include(c => c.Customer).Include(c => c.HandedTo).Include(c => c.User);

    private static object Row(AiConversation c) => new
    {
        c.Id, c.Channel, channelLabel = Deputy.Channels.GetValueOrDefault(c.Channel, c.Channel), c.Topic, c.Outcome, outcomeLabel = Deputy.Outcomes.GetValueOrDefault(c.Outcome, c.Outcome),
        c.Status, statusLabel = Deputy.Statuses.GetValueOrDefault(c.Status, c.Status), live = c.IsLive, c.StartedAt, c.LastMessageAt, c.DurationSec, c.Resolution,
        agent = c.Agent == null ? null : new { c.Agent.Id, c.Agent.Name }, model = ModelRow(c.Model), customer = c.Customer == null ? null : CustomerRow(c.Customer),
        handedTo = c.HandedTo == null ? null : new { c.HandedTo.Id, c.HandedTo.Name }, c.HandedToId, tester = c.User?.Name,
    };

    // ---- Dashboard ----------------------------------------------------------------------------------------------

    [HttpGet("dashboard")]
    public async Task<object> Dashboard()
    {
        var since = DateTime.UtcNow.Date.AddDays(-29);
        var stats = await db.AiConversations.Where(c => c.StartedAt >= since && (c.Channel == "sms" || c.Channel == "chat"))
            .Select(c => new { c.AiAgentId, c.AiModelId, c.InputTokens, c.OutputTokens, c.DurationSec, c.Outcome }).ToListAsync();
        var byAgent = stats.GroupBy(s => s.AiAgentId).ToDictionary(g => g.Key, g => new
        {
            n = g.Count(), tokens = g.Sum(s => s.InputTokens + s.OutputTokens), avgSec = (int)g.Average(s => s.DurationSec),
            resolved = g.Count(s => s.Outcome == "resolved"), handed = g.Count(s => s.Outcome == "handed_off"),
        });
        var byModel = stats.Where(s => s.AiModelId != null).GroupBy(s => s.AiModelId!.Value).ToDictionary(g => g.Key, g => new { n = g.Count(), tokens = g.Sum(s => s.InputTokens + s.OutputTokens) });
        var total = stats.Count;
        var agents = await db.AiAgents.Include(a => a.Model).Include(a => a.Fallback).Where(a => Deputy.ConversationActivities.Contains(a.Activity))
            .OrderByDescending(a => a.Active).ThenBy(a => a.Name).ToListAsync();
        var models = await db.AiModels.OrderBy(m => m.Provider).ThenBy(m => m.Name).ToListAsync();
        var agentCounts = await db.AiAgents.GroupBy(a => a.AiModelId).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        return new
        {
            agents = agents.Select(a => new { a.Id, a.Name, a.Active, activityLabel = a.ActivityLabel, a.Temperature, model = ModelRow(a.Model), fallback = ModelRow(a.Fallback), stats = byAgent.GetValueOrDefault(a.Id) }),
            models = models.Select(m => new { model = ModelRow(m), agents = agentCounts.GetValueOrDefault(m.Id), stats = byModel.GetValueOrDefault(m.Id) }),
            total, resolvedRate = total == 0 ? 0 : (int)Math.Round(100.0 * byAgent.Values.Sum(s => s.resolved) / total),
            handed = byAgent.Values.Sum(s => s.handed), tokens = byAgent.Values.Sum(s => s.tokens), local = models.Count(m => m.IsLocal),
            ready = new { hosted = Deputy.HostedUrl(config) != null, local = Deputy.LocalUrl(config) != null },
            live = await db.AiConversations.Where(c => c.Status != "closed").GroupBy(c => c.Status).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n),
            recent = (await Conversations.Where(c => c.Channel == "sms" || c.Channel == "chat").OrderByDescending(c => c.StartedAt).Take(8).ToListAsync()).Select(Row),
        };
    }

    // ---- Conversations ------------------------------------------------------------------------------------------

    /// <summary>Texts and My Account chats. Live ones on top (waiting for a person, then being answered); the search covers every conversation.</summary>
    [HttpGet("conversations")]
    public async Task<object> List(DateOnly? start, DateOnly? end, string? account, int? agent, int? model, string? channel, string? outcome, string? topic, int page = 1)
    {
        var q = Conversations.Where(c => channel == "test" ? c.Channel == "test" : c.Channel == "sms" || c.Channel == "chat");
        if (start is { } s) q = q.Where(c => c.StartedAt >= s.ToDateTime(TimeOnly.MinValue));
        if (end is { } e) q = q.Where(c => c.StartedAt < e.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (!string.IsNullOrWhiteSpace(account)) q = q.Where(c => c.Customer!.Account.StartsWith(account.Trim()));
        if (agent != null) q = q.Where(c => c.AiAgentId == agent);
        if (model != null) q = q.Where(c => c.AiModelId == model);
        if (channel is "sms" or "chat") q = q.Where(c => c.Channel == channel);
        if (!string.IsNullOrEmpty(outcome)) q = q.Where(c => c.Outcome == outcome);
        if (!string.IsNullOrEmpty(topic)) q = q.Where(c => c.Topic == topic);
        async Task<IEnumerable<object>> Live(params string[] statuses) =>
            (await Conversations.Where(c => statuses.Contains(c.Status)).OrderBy(c => c.LastMessageAt).ToListAsync()).Select(Row);
        return new
        {
            me = (await Me()).Id, waiting = await Live("waiting"), answering = await Live("agent", "bot"),
            total = await q.CountAsync(), page,
            conversations = (await q.OrderByDescending(c => c.StartedAt).ThenByDescending(c => c.Id).Skip((Math.Max(page, 1) - 1) * 50).Take(50).ToListAsync()).Select(Row),
            agents = await db.AiAgents.Where(a => Deputy.ConversationActivities.Contains(a.Activity)).OrderBy(a => a.Name).Select(a => new { a.Id, a.Name }).ToListAsync(),
            models = await db.AiModels.OrderBy(m => m.Name).Select(m => new { m.Id, m.Name }).ToListAsync(),
            topics = await db.AiConversations.Where(c => c.Channel == "sms" || c.Channel == "chat").Select(c => c.Topic).Distinct().OrderBy(t => t).ToListAsync(),
            channels = Deputy.Channels, outcomes = Deputy.Outcomes,
        };
    }

    /// <summary>One conversation. While it's live the page asks again every few seconds for new messages.</summary>
    [HttpGet("conversations/{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var c = await Conversations.Include(x => x.ResolvedBy).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        var me = await Me();
        return Ok(new
        {
            conversation = Row(c), agentActivity = c.Agent?.ActivityLabel, c.InputTokens, c.OutputTokens, resolvedBy = c.ResolvedBy?.Name, c.ResolvedAt,
            mine = c.Status == "agent" && c.HandedToId == me.Id, lines = c.Lines().Select(l => new { who = l.Who, text = l.Text }),
        });
    }

    private async Task<AiConversation?> Live(int id) => await db.AiConversations.Include(c => c.Customer).FirstOrDefaultAsync(c => c.Id == id);

    [HttpPost("conversations/{id:int}/take-over")]
    public async Task<IActionResult> TakeOver(int id)
    {
        if (await Live(id) is not { } c) return NotFound();
        if (!c.IsLive) return UnprocessableEntity(new { error = "This conversation is closed." });
        Deputy.TakeOver(c, await Me());
        await db.SaveChangesAsync();
        return Ok(new { message = "You have this conversation. The bot will stay quiet until you hand it back." });
    }

    public record ReplyInput(string? Message);

    [HttpPost("conversations/{id:int}/reply")]
    public async Task<IActionResult> Reply(int id, ReplyInput input)
    {
        if (await Live(id) is not { } c) return NotFound();
        if (!c.IsLive) return UnprocessableEntity(new { error = "This conversation is closed." });
        var max = c.Channel == "sms" ? 480 : 2000;
        if (string.IsNullOrWhiteSpace(input.Message)) return BadRequest(new { error = "Type a reply." });
        if (input.Message.Length > max) return BadRequest(new { error = $"Keep it to {max} characters." });
        var message = await Deputy.AgentSaysAsync(db, c, await Me(), input.Message);
        await db.SaveChangesAsync();
        return Ok(new { message });
    }

    [HttpPost("conversations/{id:int}/hand-back")]
    public async Task<IActionResult> HandBack(int id)
    {
        if (await Live(id) is not { } c) return NotFound();
        if (!c.IsLive) return UnprocessableEntity(new { error = "This conversation is closed." });
        Deputy.HandBack(c, await Me());
        await db.SaveChangesAsync();
        return Ok(new { message = "The bot is answering again." });
    }

    [HttpPost("conversations/{id:int}/close")]
    public async Task<IActionResult> Close(int id)
    {
        if (await Live(id) is not { } c) return NotFound();
        if (!c.IsLive) return UnprocessableEntity(new { error = "This conversation is already closed." });
        Deputy.Close(c, await Me());
        await db.SaveChangesAsync();
        return Ok(new { message = $"Conversation #{c.Id} closed." });
    }

    /// <summary>Call the customer: logged here and on the account, then the browser dials the number.</summary>
    [HttpPost("conversations/{id:int}/call")]
    public async Task<IActionResult> Call(int id)
    {
        if (await Live(id) is not { } c) return NotFound();
        if (c.Customer is not { Phone.Length: > 0 } customer) return UnprocessableEntity(new { error = "This customer has no phone number." });
        if (!c.IsLive) return UnprocessableEntity(new { error = "This conversation is closed." });
        var phone = Deputy.Call(db, c, customer, await Me());
        await db.SaveChangesAsync();
        return Ok(new { message = $"Calling {phone}.", dial = Regex.Replace(phone, @"[^\d+]", "") });
    }

    public record ResolutionInput(string Resolution);

    /// <summary>Resolved, Incomplete… for a chat or text conversation.</summary>
    [HttpPost("conversations/{id:int}/resolution")]
    public async Task<IActionResult> Resolution(int id, ResolutionInput input)
    {
        if (!Corral.Resolutions.Any(r => r.Resolution == input.Resolution)) return BadRequest(new { error = "Pick a resolution." });
        if (await db.AiConversations.FindAsync(id) is not { } c) return NotFound();
        (c.Resolution, c.ResolvedById, c.ResolvedAt) = (input.Resolution, (await Me()).Id, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return Ok(new { message = $"Marked {input.Resolution}." });
    }

    // ---- Agents -------------------------------------------------------------------------------------------------

    [HttpGet("agents")]
    public async Task<object> Agents()
    {
        var counts = await db.AiConversations.Where(c => c.Channel != "test").GroupBy(c => c.AiAgentId)
            .Select(g => new { g.Key, n = g.Count(), last = g.Max(c => c.StartedAt) }).ToDictionaryAsync(x => x.Key);
        return (await db.AiAgents.Include(a => a.Model).Include(a => a.Fallback).Where(a => Deputy.ConversationActivities.Contains(a.Activity))
            .OrderByDescending(a => a.Active).ThenBy(a => a.Name).ToListAsync())
            .Select(a => new { a.Id, a.Name, a.Active, a.Activity, activityLabel = a.ActivityLabel, a.Description, model = ModelRow(a.Model), fallback = ModelRow(a.Fallback),
                conversations = counts.GetValueOrDefault(a.Id)?.n ?? 0, lastAt = counts.GetValueOrDefault(a.Id)?.last });
    }

    private async Task<object> ModelOptions() => (await db.AiModels.OrderBy(m => m.Provider).ThenBy(m => m.Name).ToListAsync())
        .Select(m => new { m.Id, m.Name, m.ModelId, m.Status, providerLabel = m.ProviderLabel });

    [HttpGet("agents/new")]
    public async Task<object> NewAgent() => new
    {
        agent = new { name = "", activity = "chat", description = "", aiModelId = (int?)null, fallbackModelId = (int?)null, temperature = 0.3m, maxTokens = 2000, systemPrompt = "", active = true },
        models = await ModelOptions(), activities = Deputy.ConversationActivities.ToDictionary(a => a, a => Deputy.Activities[a]),
    };

    [HttpGet("agents/{id:int}")]
    public async Task<IActionResult> Agent(int id)
    {
        var a = await db.AiAgents.FindAsync(id);
        if (a is not { TalksToCustomers: true }) return NotFound(); // scheduled automations are edited in Sheriff
        return Ok(new
        {
            agent = new { a.Id, a.Name, a.Activity, a.Description, a.AiModelId, a.FallbackModelId, a.Temperature, a.MaxTokens, a.SystemPrompt, a.Active },
            models = await ModelOptions(), activities = Deputy.ConversationActivities.ToDictionary(x => x, x => Deputy.Activities[x]),
        });
    }

    public record AgentInput(string? Name, string? Activity, string? Description, int? AiModelId, int? FallbackModelId, decimal? Temperature, int? MaxTokens, string? SystemPrompt, bool Active);

    [HttpPost("agents")]
    public Task<IActionResult> AddAgent(AgentInput input) => SaveAgent(null, input);

    [HttpPut("agents/{id:int}")]
    public Task<IActionResult> UpdateAgent(int id, AgentInput input) => SaveAgent(id, input);

    private async Task<IActionResult> SaveAgent(int? id, AgentInput input)
    {
        var a = id == null ? new AiAgent() : await db.AiAgents.FindAsync(id);
        if (a == null || (id != null && !a.TalksToCustomers)) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 80) return BadRequest(new { error = "Give the agent a name (up to 80 characters)." });
        if (!Deputy.ConversationActivities.Contains(input.Activity)) return BadRequest(new { error = "Pick an activity." });
        if (input.AiModelId == null || !await db.AiModels.AnyAsync(m => m.Id == input.AiModelId)) return BadRequest(new { error = "Pick a model." });
        if (input.FallbackModelId != null && (input.FallbackModelId == input.AiModelId || !await db.AiModels.AnyAsync(m => m.Id == input.FallbackModelId)))
            return BadRequest(new { error = "Pick a different model as the fallback." });
        if (input.Temperature is not (>= 0 and <= 2)) return BadRequest(new { error = "Temperature is between 0 and 2." });
        if (input.MaxTokens is not (>= 256 and <= 64000)) return BadRequest(new { error = "Max tokens is between 256 and 64,000." });
        if (string.IsNullOrWhiteSpace(input.SystemPrompt)) return BadRequest(new { error = "Write the agent's instructions." });
        (a.Name, a.Activity, a.Description, a.AiModelId, a.FallbackModelId) = (input.Name.Trim(), input.Activity!, string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(), input.AiModelId.Value, input.FallbackModelId);
        (a.Temperature, a.MaxTokens, a.SystemPrompt, a.Active, a.Schedule, a.UpdatedAt) = (input.Temperature!.Value, input.MaxTokens!.Value, input.SystemPrompt.Trim(), input.Active, null, DateTime.UtcNow);
        if (id == null) db.AiAgents.Add(a);
        await db.SaveChangesAsync();
        return Ok(new { message = "Agent saved" });
    }

    /// <summary>Test an agent: send it a message and see what its model answers. Saved as a Test conversation.</summary>
    [HttpGet("agents/{id:int}/test")]
    public async Task<IActionResult> TestPage(int id)
    {
        var a = await db.AiAgents.Include(x => x.Model).Include(x => x.Fallback).FirstOrDefaultAsync(x => x.Id == id);
        if (a is not { TalksToCustomers: true }) return NotFound();
        return Ok(new
        {
            agent = new { a.Id, a.Name, activityLabel = a.ActivityLabel, model = a.Model?.Name, fallback = a.Fallback?.Name },
            tests = (await Conversations.Where(c => c.AiAgentId == id && c.Channel == "test").OrderByDescending(c => c.StartedAt).Take(10).ToListAsync()).Select(Row),
        });
    }

    public record TestInput(string? Message);

    [HttpPost("agents/{id:int}/test")]
    public async Task<IActionResult> Test(int id, TestInput input)
    {
        var a = await db.AiAgents.Include(x => x.Model).Include(x => x.Fallback).FirstOrDefaultAsync(x => x.Id == id);
        if (a is not { TalksToCustomers: true }) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 4000) return BadRequest(new { error = "Write a message (up to 4,000 characters)." });
        var r = await Deputy.RunAsync(config, a, input.Message);
        var c = new AiConversation
        {
            AiAgentId = a.Id, AiModelId = r.Model?.Id, Channel = "test", Topic = "Test", Outcome = r.Ok ? "resolved" : "failed", UserId = (await Me()).Id,
            StartedAt = DateTime.UtcNow, DurationSec = (int)Math.Ceiling(r.Ms / 1000.0), InputTokens = r.InputTokens, OutputTokens = r.OutputTokens,
            Transcript = $"Tester: {input.Message.Replace("\n", " ")}\n" + (r.Ok ? $"Agent: {r.Text.Replace("\n", " ")}" : $"System: No answer. {r.Note}"),
        };
        db.AiConversations.Add(c);
        await db.SaveChangesAsync();
        return Ok(new { r.Ok, r.Text, modelName = r.Model?.Name, r.InputTokens, r.OutputTokens, r.Ms, r.Note, conversation = c.Id });
    }

    // ---- Models -------------------------------------------------------------------------------------------------

    [HttpGet("models")]
    public async Task<object> Models()
    {
        var agents = await db.AiAgents.GroupBy(a => a.AiModelId).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var fallback = await db.AiAgents.Where(a => a.FallbackModelId != null).GroupBy(a => a.FallbackModelId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        return (await db.AiModels.OrderBy(m => m.Provider).ThenBy(m => m.Name).ToListAsync()).Select(m => new
        {
            model = ModelRow(m), m.Source, m.Quantization, m.SizeGb, m.ContextWindow, m.CheckedAt, agents = agents.GetValueOrDefault(m.Id), asFallback = fallback.GetValueOrDefault(m.Id),
        });
    }

    [HttpGet("models/{id:int}")]
    public async Task<IActionResult> Model(int id) => await db.AiModels.FindAsync(id) is { } m
        ? Ok(new { model = new { m.Id, m.Name, m.Provider, m.ModelId, m.Source, m.SizeGb, m.Quantization, m.ContextWindow, m.Notes }, providers = Deputy.Providers })
        : NotFound();

    [HttpGet("models/new")]
    public object NewModel() => new { model = new { name = "", provider = "local", modelId = "", source = "", sizeGb = (decimal?)null, quantization = "", contextWindow = (int?)null, notes = "" }, providers = Deputy.Providers };

    public record ModelInput(string? Name, string? Provider, string? ModelId, string? Source, decimal? SizeGb, string? Quantization, int? ContextWindow, string? Notes);

    [HttpPost("models")]
    public Task<IActionResult> AddModel(ModelInput input) => SaveModel(null, input);

    [HttpPut("models/{id:int}")]
    public Task<IActionResult> UpdateModel(int id, ModelInput input) => SaveModel(id, input);

    private async Task<IActionResult> SaveModel(int? id, ModelInput input)
    {
        var m = id == null ? new AiModel { Status = "missing" } : await db.AiModels.FindAsync(id);
        if (m == null) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Give the model a name." });
        if (input.Provider == null || !Deputy.Providers.ContainsKey(input.Provider)) return BadRequest(new { error = "Pick a provider." });
        if (string.IsNullOrWhiteSpace(input.ModelId) || !Regex.IsMatch(input.ModelId.Trim(), @"^[A-Za-z0-9._:/\-]+$"))
            return BadRequest(new { error = "Use the model's id as the server knows it, like qwen3:8b or moonshotai/Kimi-K2-Instruct." });
        if (await db.AiModels.AnyAsync(x => x.Provider == input.Provider && x.ModelId == input.ModelId.Trim() && x.Id != m.Id)) return BadRequest(new { error = "That model is already here." });
        if (input.ContextWindow is < 512) return BadRequest(new { error = "The context window is at least 512 tokens." });
        string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        (m.Name, m.Provider, m.ModelId, m.Source, m.SizeGb, m.Quantization, m.ContextWindow, m.Notes, m.UpdatedAt) =
            (input.Name.Trim(), input.Provider, input.ModelId.Trim(), Blank(input.Source), input.SizeGb, Blank(input.Quantization), input.ContextWindow, Blank(input.Notes), DateTime.UtcNow);
        if (id == null) db.AiModels.Add(m);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{m.Name} saved. Use Check to confirm it is available." });
    }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>Is the model there? Downloaded: the local server's list of models. Hosted: the API's /models list.</summary>
    [HttpPost("models/{id:int}/check")]
    public async Task<IActionResult> Check(int id)
    {
        if (await db.AiModels.FindAsync(id) is not { } m) return NotFound();
        var (status, message) = m.IsLocal ? await CheckLocal(m) : await CheckHosted(m);
        (m.Status, m.CheckedAt) = (status, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{m.Name}: {message}" });
    }

    private async Task<(string, string)> CheckHosted(AiModel m)
    {
        if (Deputy.HostedUrl(config) is not { } url) return (m.Status, "the Open Models API URL is not set, so it can't be checked (Sheriff → APIs → Open Models API).");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url.TrimEnd('/') + "/models");
            if (config["Integrations:OpenModels:ApiKey"] is { Length: > 0 } key) request.Headers.Authorization = new("Bearer", key);
            using var r = await Http.SendAsync(request);
            if (!r.IsSuccessStatusCode) return (m.Status, $"the open models API answered HTTP {(int)r.StatusCode}.");
            var json = JsonSerializer.Deserialize<JsonElement>(await r.Content.ReadAsStringAsync());
            var ids = json.TryGetProperty("data", out var d) ? d.EnumerateArray().Select(x => x.TryGetProperty("id", out var i) ? i.GetString() : null).ToList() : [];
            return ids.Contains(m.ModelId) ? ("available", "served by the open models API.") : ("missing", $"the open models API doesn't list {m.ModelId}. Check the id against the provider's model list.");
        }
        catch { return (m.Status, "could not reach the open models API."); }
    }

    private async Task<(string, string)> CheckLocal(AiModel m)
    {
        if (Deputy.LocalUrl(config) is not { } url) return (m.Status, "the Local Models URL is not set, so it can't be checked (Sheriff → APIs → Local Models).");
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(await Http.GetStringAsync(url.TrimEnd('/') + "/api/tags"));
            var names = json.TryGetProperty("models", out var list) ? list.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() : null).ToList() : [];
            return names.Contains(m.ModelId) || names.Contains(m.ModelId + ":latest")
                ? ("available", "downloaded and ready on the local server.")
                : ("missing", $"not on the local server. Download it there (for Ollama: ollama pull {m.ModelId}).");
        }
        catch { return (m.Status, "could not reach the local model server."); }
    }
}
