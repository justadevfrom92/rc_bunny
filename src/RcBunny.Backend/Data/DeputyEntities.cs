using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace RcBunny.Data;

// ---- Deputy: AI agents on open-weight models (ebunny's ai_models, ai_agents, ai_conversations, ai_automation_runs) ----

/// <summary>An open-weight model Deputy's agents can use: downloaded to the local server, or hosted behind an OpenAI-compatible API.</summary>
public class AiModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>local or hosted (Deputy.Providers).</summary>
    public string Provider { get; set; } = "local";
    /// <summary>The id the server knows it by: qwen3:8b, moonshotai/Kimi-K2-Instruct…</summary>
    public string ModelId { get; set; } = "";
    /// <summary>Where a downloaded model came from (file, URL, ollama pull name).</summary>
    public string? Source { get; set; }
    public decimal? SizeGb { get; set; }
    public string? Quantization { get; set; }
    public int? ContextWindow { get; set; }
    /// <summary>available, downloading or missing.</summary>
    public string Status { get; set; } = "available";
    public DateTime? CheckedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsLocal => Provider == "local";
    public string ProviderLabel => Deputy.Providers.GetValueOrDefault(Provider, Provider);
}

/// <summary>An AI agent: what it does (activity), the model it runs on, its instructions, and for automations its schedule.</summary>
public class AiAgent
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>One of Deputy.Activities: sms and chat talk to customers; the rest are scheduled automations (Sheriff).</summary>
    public string Activity { get; set; } = "chat";
    public string? Description { get; set; }
    public int AiModelId { get; set; }
    public AiModel? Model { get; set; }
    public int? FallbackModelId { get; set; }
    public AiModel? Fallback { get; set; }
    public decimal Temperature { get; set; } = 0.3m;
    public int MaxTokens { get; set; } = 2000;
    public string SystemPrompt { get; set; } = "";
    public bool Active { get; set; } = true;
    /// <summary>Cron expression, automation agents only.</summary>
    public string? Schedule { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>True for agents that talk to customers (texts, My Account chat); false for scheduled automations.</summary>
    public bool TalksToCustomers => Deputy.ConversationActivities.Contains(Activity);
    public string ActivityLabel => Deputy.Activities.GetValueOrDefault(Activity, Activity);
}

/// <summary>
/// One conversation with a customer by text or in the My Account chat (or an agent test), with its transcript.
/// Live ones say who is answering now: the bot, nobody yet (waiting for a person), or a person.
/// </summary>
public class AiConversation
{
    public int Id { get; set; }
    public int AiAgentId { get; set; }
    public AiAgent? Agent { get; set; }
    public int? AiModelId { get; set; }
    public AiModel? Model { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>sms, chat or test.</summary>
    public string Channel { get; set; } = "chat";
    public string Topic { get; set; } = "";
    /// <summary>resolved, handed_off, abandoned or failed.</summary>
    public string Outcome { get; set; } = "resolved";
    /// <summary>bot, waiting (for a person), agent (a person is answering) or closed.</summary>
    public string Status { get; set; } = "closed";
    public int? HandedToId { get; set; }
    public User? HandedTo { get; set; }
    /// <summary>Who ran a test.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }
    /// <summary>"Speaker: text" lines.</summary>
    public string Transcript { get; set; } = "";
    public int DurationSec { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
    /// <summary>Resolved, Incomplete… (Corral.Resolutions), like calls and texts.</summary>
    public string? Resolution { get; set; }
    public int? ResolvedById { get; set; }
    public User? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsLive => Status != "closed";

    /// <summary>Adds "Speaker: text" to the transcript (one line; newlines become spaces).</summary>
    public void Say(string who, string text)
    {
        var line = $"{who}: {Regex.Replace(text.Trim(), @"\s+", " ")}";
        Transcript = (Transcript + "\n" + line).TrimStart('\n');
        LastMessageAt = DateTime.UtcNow;
        DurationSec = Math.Max(0, (int)(DateTime.UtcNow - StartedAt).TotalSeconds);
    }

    /// <summary>People who answered show as "Name (agent)"; the AI is "Bot".</summary>
    public static string SpeakerFor(User user) => $"{user.Name} (agent)";

    /// <summary>(speaker, text) from "Speaker: text" lines.</summary>
    public List<(string Who, string Text)> Lines() => Transcript.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(l => l.TrimEnd('\r')).Where(l => l.Trim() != "")
        .Select(l => l.IndexOf(':') is var i and >= 0 ? (l[..i].Trim(), l[(i + 1)..].Trim()) : ("", l.Trim())).ToList();
}

/// <summary>One run of a scheduled automation agent: what it checked, fixed and left for a person.</summary>
public class AiAutomationRun
{
    public int Id { get; set; }
    public int AiAgentId { get; set; }
    public AiAgent? Agent { get; set; }
    public int? AiModelId { get; set; }
    public AiModel? Model { get; set; }
    /// <summary>ok or failed.</summary>
    public string Status { get; set; } = "ok";
    public int Checked { get; set; }
    public int Fixed { get; set; }
    public int Flagged { get; set; }
    public string? Summary { get; set; }
    public int DurationSec { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Deputy's lists (ebunny's config/deputy.php), the agent runner and live conversations. Two ways to run a model, both set
/// up in Sheriff → APIs: local (downloaded and served by Ollama, Integrations:LocalModels:Url, its /api/chat) and hosted
/// (any OpenAI-compatible endpoint, Integrations:OpenModels:Url + ApiKey: vLLM, llama.cpp server, LM Studio, OpenRouter…).
/// </summary>
public static class Deputy
{
    public static readonly Dictionary<string, string> Providers = new() { ["local"] = "Local server (Ollama)", ["hosted"] = "Hosted (OpenAI-compatible)" };

    /// <summary>What each agent does in the business.</summary>
    public static readonly Dictionary<string, string> Activities = new()
    {
        ["sms"] = "Replies to texts", ["chat"] = "My Account chat", ["queues"] = "Triages exception queues", ["enrollments"] = "Fixes stuck enrollments",
        ["payments"] = "Matches unapplied payments", ["surveys"] = "Tags survey comments", ["reports"] = "Summarizes reports",
    };
    /// <summary>The activities that talk to customers; every other activity is an automation that runs on a schedule.</summary>
    public static readonly string[] ConversationActivities = ["sms", "chat"];

    public static readonly Dictionary<string, string> Channels = new() { ["sms"] = "Text", ["chat"] = "My Account Chat", ["test"] = "Test" };
    public static readonly Dictionary<string, string> Outcomes = new() { ["resolved"] = "Resolved", ["handed_off"] = "Handed to a person", ["abandoned"] = "Customer left", ["failed"] = "Model error" };
    /// <summary>Who is answering a conversation right now.</summary>
    public static readonly Dictionary<string, string> Statuses = new() { ["bot"] = "Bot answering", ["waiting"] = "Waiting for a person", ["agent"] = "Person answering", ["closed"] = "Closed" };

    /// <summary>A chat with no new message for this long is closed, and the customer's next message starts a new one.</summary>
    public const int ChatIdleMinutes = 30;

    public static string? HostedUrl(IConfiguration c) => c["Integrations:OpenModels:Url"] is { Length: > 0 } u ? u : null;
    public static string? LocalUrl(IConfiguration c) => c["Integrations:LocalModels:Url"] is { Length: > 0 } u ? u : null;

    // ---- the agent runner -----------------------------------------------------------------------------------

    public record Reply(bool Ok, string Text, AiModel? Model, int InputTokens, int OutputTokens, int Ms, string? Note);
    private class Unavailable(string message) : Exception(message);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };

    /// <summary>
    /// Sends a message to an agent's model and returns the reply. Downloaded models go to the local Ollama server (/api/chat);
    /// hosted ones to the OpenAI-compatible endpoint (/chat/completions). If the agent's model can't answer, its fallback is tried.
    /// history: earlier turns, oldest first, as (role, content).
    /// </summary>
    public static async Task<Reply> RunAsync(IConfiguration config, AiAgent agent, string message, List<(string Role, string Content)>? history = null)
    {
        var messages = new List<object> { new { role = "system", content = agent.SystemPrompt } };
        messages.AddRange((history ?? []).Select(h => new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = message });
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var errors = new List<string>();
        var models = new[] { agent.Model, agent.Fallback }.Where(m => m != null).ToList();
        for (var i = 0; i < models.Count; i++)
        {
            var model = models[i]!;
            try
            {
                var (text, input, output, note) = model.IsLocal ? await LocalAsync(config, agent, model, messages) : await HostedAsync(config, agent, model, messages);
                var notes = ((i > 0 ? "Used the fallback model. " + string.Join(" ", errors) : "") + " " + note).Trim();
                return new Reply(true, text, model, input, output, (int)watch.ElapsedMilliseconds, notes == "" ? null : notes);
            }
            catch (Unavailable e) { errors.Add($"{model.Name}: {e.Message}"); }
        }
        return new Reply(false, "", null, 0, 0, (int)watch.ElapsedMilliseconds, string.Join(" ", errors));
    }

    /// <summary>Ollama's chat API on your own server.</summary>
    private static async Task<(string, int, int, string?)> LocalAsync(IConfiguration config, AiAgent agent, AiModel model, List<object> messages)
    {
        var url = LocalUrl(config) ?? throw new Unavailable("The Local Models URL is not set (Sheriff → APIs → Local Models).");
        var r = await PostAsync(url.TrimEnd('/') + "/api/chat", null, new
        {
            model = model.ModelId, stream = false, messages,
            options = new { temperature = (double)agent.Temperature, num_predict = agent.MaxTokens },
        }, "the local model server");
        var text = r.TryGetProperty("message", out var m) && m.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
        return (text.Trim(), Int(r, "prompt_eval_count"), Int(r, "eval_count"),
            r.TryGetProperty("done_reason", out var d) && d.GetString() == "length" ? "The reply hit the agent's max tokens and was cut off." : null);
    }

    /// <summary>Any OpenAI-compatible endpoint serving open-weight models.</summary>
    private static async Task<(string, int, int, string?)> HostedAsync(IConfiguration config, AiAgent agent, AiModel model, List<object> messages)
    {
        var url = HostedUrl(config) ?? throw new Unavailable("The Open Models API URL is not set (Sheriff → APIs → Open Models API).");
        var r = await PostAsync(url.TrimEnd('/') + "/chat/completions", config["Integrations:OpenModels:ApiKey"], new
        {
            model = model.ModelId, messages, temperature = (double)agent.Temperature, max_tokens = agent.MaxTokens,
        }, "the open models API");
        if (!r.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) throw new Unavailable("The open models API returned no answer.");
        var choice = choices[0];
        var text = choice.TryGetProperty("message", out var m) && m.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
        var usage = r.TryGetProperty("usage", out var u) ? u : default;
        return (text.Trim(), usage.ValueKind == JsonValueKind.Object ? Int(usage, "prompt_tokens") : 0, usage.ValueKind == JsonValueKind.Object ? Int(usage, "completion_tokens") : 0,
            choice.TryGetProperty("finish_reason", out var f) && f.GetString() == "length" ? "The reply hit the agent's max tokens and was cut off." : null);
    }

    private static int Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : 0;

    private static async Task<JsonElement> PostAsync(string url, string? key, object body, string what)
    {
        // A sized body (not chunked), which every model server accepts
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrEmpty(key)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        HttpResponseMessage r;
        try { r = await Http.SendAsync(request); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { throw new Unavailable($"Could not reach {what}."); }
        var raw = await r.Content.ReadAsStringAsync();
        var what1 = char.ToUpper(what[0]) + what[1..];
        if ((int)r.StatusCode is 401 or 403) throw new Unavailable($"{what1} rejected the API key.");
        JsonElement json;
        try { json = JsonSerializer.Deserialize<JsonElement>(raw); } catch { json = default; }
        if (!r.IsSuccessStatusCode)
        {
            var error = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("error", out var e)
                ? e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var em) ? em.GetString() : e.ValueKind == JsonValueKind.String ? e.GetString() : null : null;
            throw new Unavailable($"{what1} answered HTTP {(int)r.StatusCode}{(error != null ? ": " + (error.Length > 200 ? error[..199] + "…" : error) : "")}.");
        }
        if (json.ValueKind != JsonValueKind.Object) throw new Unavailable($"{what1} did not answer with JSON.");
        return json;
    }

    // ---- live conversations ---------------------------------------------------------------------------------
    //  The bot answers until it hands off (it can't help, the model doesn't answer, or the customer asks for a person) or
    //  an agent presses Take Over; from then on the agent's replies go to the customer (texts by SMS, chats in the chat
    //  bubble) and the bot stays quiet until the agent hands it back. The caller saves.

    /// <summary>The agent that answers a channel (sms, chat), if one is active.</summary>
    public static Task<AiAgent?> AgentForAsync(AppDbContext db, string channel) =>
        db.AiAgents.Include(a => a.Model).Include(a => a.Fallback).Where(a => a.Activity == channel && a.Active).OrderBy(a => a.Id).FirstOrDefaultAsync();

    /// <summary>The customer's open chat, or a new one when they send their first message.</summary>
    public static async Task<AiConversation?> ChatForAsync(AppDbContext db, Customer c, bool create = false)
    {
        var since = DateTime.UtcNow.AddMinutes(-ChatIdleMinutes);
        var open = await db.AiConversations.Where(x => x.CustomerId == c.Id && x.Channel == "chat" && x.Status != "closed" && x.LastMessageAt >= since)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        if (open != null || !create) return open;
        var agent = await AgentForAsync(db, "chat");
        var agentId = agent?.Id ?? await db.AiAgents.Where(a => a.Activity == "chat").Select(a => (int?)a.Id).FirstOrDefaultAsync();
        if (agentId == null) return null;
        var conv = new AiConversation
        {
            AiAgentId = agentId.Value, AiModelId = agent?.AiModelId, CustomerId = c.Id, Channel = "chat", Topic = "My Account chat", Outcome = "resolved",
            Status = agent != null ? "bot" : "waiting", Transcript = $"Bot: Hi {c.FirstName}, how can I help today?", StartedAt = DateTime.UtcNow, LastMessageAt = DateTime.UtcNow,
        };
        db.AiConversations.Add(conv);
        await db.SaveChangesAsync();
        return conv;
    }

    /// <summary>The customer wrote: add it, and let the bot answer if it still has the conversation.</summary>
    public static async Task CustomerSaysAsync(AppDbContext db, IConfiguration config, AiConversation conv, string text)
    {
        conv.Say("Customer", text);
        if (conv.Status != "bot") return;
        if (Regex.IsMatch(text, @"\b(person|human|agent|representative|someone)\b", RegexOptions.IgnoreCase))
            await HandOffAsync(db, conv, "Of course. I am getting a person for you now. They will see everything we covered.");
        else await BotAnswersAsync(db, config, conv, text);
    }

    private static async Task BotAnswersAsync(AppDbContext db, IConfiguration config, AiConversation conv, string text)
    {
        var agent = await db.AiAgents.Include(a => a.Model).Include(a => a.Fallback).FirstOrDefaultAsync(a => a.Id == conv.AiAgentId);
        if (agent is not { Active: true }) { await HandOffAsync(db, conv, "Let me get a person to help you with that."); return; }
        // Earlier turns for the model: customer lines are the user, the bot's and people's lines are the assistant
        var history = conv.Lines().SkipLast(1).Where(l => l.Who != "System").Select(l => (l.Who == "Customer" ? "user" : "assistant", l.Text)).ToList();
        var r = await RunAsync(config, agent, text, history);
        conv.AiModelId = r.Model?.Id ?? conv.AiModelId;
        conv.InputTokens += r.InputTokens;
        conv.OutputTokens += r.OutputTokens;
        if (!r.Ok)
        {
            conv.Say("System", "The bot could not answer: " + r.Note);
            await HandOffAsync(db, conv, "Sorry, I can't answer that right now. I am getting a person for you.");
            return;
        }
        var reply = r.Text.Replace("HANDOFF", "").Trim();
        if (reply != "") await DeliverAsync(db, conv, "Bot", reply);
        if (r.Text.Contains("HANDOFF")) await HandOffAsync(db, conv, reply == "" ? "I am getting a person for you now." : null);
    }

    /// <summary>The bot gives the conversation to a person: it waits in Needs a Person until someone takes it.</summary>
    public static async Task HandOffAsync(AppDbContext db, AiConversation conv, string? message = null)
    {
        if (message != null) await DeliverAsync(db, conv, "Bot", message);
        (conv.Status, conv.Outcome) = ("waiting", "handed_off");
    }

    public static void TakeOver(AiConversation conv, User user)
    {
        if (conv.Status == "agent" && conv.HandedToId == user.Id) return;
        (conv.Status, conv.Outcome, conv.HandedToId) = ("agent", "handed_off", user.Id);
        conv.Say("System", $"{user.Name} took over.");
    }

    /// <summary>An agent's reply, taking over first if the bot still had it. Texts go out by SMS.</summary>
    public static Task<string> AgentSaysAsync(AppDbContext db, AiConversation conv, User user, string text)
    {
        TakeOver(conv, user);
        return DeliverAsync(db, conv, AiConversation.SpeakerFor(user), text, user);
    }

    public static void HandBack(AiConversation conv, User user)
    {
        conv.Status = "bot";
        conv.Say("System", $"{user.Name} handed the conversation back to the bot.");
    }

    public static void Close(AiConversation conv, User user)
    {
        conv.Say("System", $"Closed by {user.Name}.");
        conv.Status = "closed";
    }

    /// <summary>The agent calls the customer: logged on the conversation and as a note on the account.</summary>
    public static string Call(AppDbContext db, AiConversation conv, Customer c, User user)
    {
        TakeOver(conv, user);
        conv.Say("System", $"{user.Name} called the customer at {c.Phone}.");
        db.Notes.Add(new Note
        {
            CustomerId = c.Id, UserId = user.Id, Author = user.Name, Category = "Customer Service", Action = "Outbound Call", Priority = "Low",
            Body = $"Called {c.Phone} from Deputy {Channels.GetValueOrDefault(conv.Channel, conv.Channel).ToLowerInvariant()} conversation #{conv.Id}.",
        });
        return c.Phone!;
    }

    /// <summary>Adds the line and, for texts, sends it to the customer's phone (logged on the account; Twilio isn't connected yet).</summary>
    private static async Task<string> DeliverAsync(AppDbContext db, AiConversation conv, string who, string text, User? user = null)
    {
        conv.Say(who, text);
        if (conv.Channel != "sms" || conv.CustomerId == null) return "Sent.";
        var c = conv.Customer ?? await db.Customers.FindAsync(conv.CustomerId);
        if (string.IsNullOrEmpty(c?.Phone)) return "Sent.";
        db.ContactLogs.Add(new ContactLog
        {
            CustomerId = c.Id, Channel = "SMS", Template = user != null ? "Agent text" : "Bot text", Body = text.Length > 480 ? text[..480] : text,
            Phone = c.Phone, Status = "not sent", Who = user?.Name,
        });
        return "Text logged but not sent: Twilio is not configured.";
    }
}
