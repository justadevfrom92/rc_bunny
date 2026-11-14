using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Services;

namespace RcBunny.Controllers;

/// <summary>
/// Sheriff → Cron Automations (ebunny's AutomationController): AI agents that don't talk to customers and run on a cron
/// schedule (triage queues, fix enrollments, match payments…). Each has its own page with its runs. The models they use
/// are set up in Deputy.
/// </summary>
[ApiController]
[Route("api/sheriff/automations")]
[RequirePermission("sheriff")]
public class AutomationsController(AppDbContext db) : ControllerBase
{
    private IQueryable<AiAgent> Automations => db.AiAgents.Where(a => !Deputy.ConversationActivities.Contains(a.Activity));

    private static object? ModelRow(AiModel? m) => m == null ? null : new { m.Id, m.Name, m.ModelId, local = m.IsLocal };

    private record Stats(int N, int Checked, int Fixed, int Flagged, int Tokens, double AvgSec, int Failed);

    private async Task<Dictionary<int, Stats>> StatsAsync(DateTime since) =>
        (await db.AiAutomationRuns.Where(r => r.StartedAt >= since).ToListAsync()).GroupBy(r => r.AiAgentId).ToDictionary(g => g.Key, g => new Stats(
            g.Count(), g.Sum(r => r.Checked), g.Sum(r => r.Fixed), g.Sum(r => r.Flagged), g.Sum(r => r.InputTokens + r.OutputTokens), g.Average(r => r.DurationSec), g.Count(r => r.Status == "failed")));

    private static object Run(AiAutomationRun r) => new
    {
        r.Id, r.AiAgentId, agent = r.Agent?.Name, r.StartedAt, r.Summary, r.Checked, r.Fixed, r.Flagged, r.DurationSec, r.Status, r.AiModelId, model = r.Model?.Name,
    };

    [HttpGet]
    public async Task<object> Index()
    {
        var agents = await Automations.Include(a => a.Model).OrderByDescending(a => a.Active).ThenBy(a => a.Name).ToListAsync();
        var week = await StatsAsync(DateTime.Today.AddDays(-6).ToUniversalTime());
        // Each automation's latest run (by when it started)
        var last = (await db.AiAutomationRuns.Select(r => new { r.AiAgentId, r.StartedAt, r.Status }).ToListAsync())
            .GroupBy(r => r.AiAgentId).ToDictionary(g => g.Key, g => g.MaxBy(r => r.StartedAt)!);
        return new
        {
            agents = agents.Select(a => new
            {
                a.Id, a.Name, a.Active, a.Schedule, description = a.Description ?? a.ActivityLabel, describe = Cron.Describe(a.Schedule), model = ModelRow(a.Model),
                next = a.Active ? Cron.Next(a.Schedule) : null, last = last.TryGetValue(a.Id, out var l) ? new { l.StartedAt, l.Status } : null, week = week.GetValueOrDefault(a.Id),
            }),
            runs = (await db.AiAutomationRuns.Include(r => r.Agent).OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id).Take(25).ToListAsync()).Select(Run),
        };
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id, int page = 1)
    {
        var a = await Automations.Include(x => x.Model).Include(x => x.Fallback).FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        var runs = db.AiAutomationRuns.Include(r => r.Model).Where(r => r.AiAgentId == id);
        return Ok(new
        {
            agent = new
            {
                a.Id, a.Name, a.Active, a.Schedule, a.Description, activityLabel = a.ActivityLabel, describe = Cron.Describe(a.Schedule), next = a.Active ? Cron.Next(a.Schedule) : null,
                model = ModelRow(a.Model), fallback = ModelRow(a.Fallback), a.Temperature, a.SystemPrompt, a.AiModelId,
            },
            week = (await StatsAsync(DateTime.Today.AddDays(-6).ToUniversalTime())).GetValueOrDefault(id),
            month = (await StatsAsync(DateTime.Today.AddDays(-29).ToUniversalTime())).GetValueOrDefault(id),
            total = await runs.CountAsync(), page,
            runs = (await runs.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id).Skip((Math.Max(page, 1) - 1) * 25).Take(25).ToListAsync()).Select(Run),
        });
    }

    private async Task<object> Options() => new
    {
        models = (await db.AiModels.OrderBy(m => m.Provider).ThenBy(m => m.Name).ToListAsync()).Select(m => new { m.Id, m.Name, m.ModelId, providerLabel = m.ProviderLabel, m.Status }),
        activities = Deputy.Activities.Where(x => !Deputy.ConversationActivities.Contains(x.Key)).ToDictionary(),
    };

    [HttpGet("new")]
    public async Task<object> New() => new
    {
        agent = new { name = "", activity = "queues", description = "", schedule = "*/15 * * * *", aiModelId = (int?)null, fallbackModelId = (int?)null, temperature = 0.2m, maxTokens = 2000, systemPrompt = "", active = true },
        options = await Options(),
    };

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id) => await Automations.FirstOrDefaultAsync(x => x.Id == id) is { } a
        ? Ok(new { agent = new { a.Id, a.Name, a.Activity, a.Description, a.Schedule, a.AiModelId, a.FallbackModelId, a.Temperature, a.MaxTokens, a.SystemPrompt, a.Active }, options = await Options() })
        : NotFound();

    public record Input(string? Name, string? Activity, string? Description, string? Schedule, int? AiModelId, int? FallbackModelId, decimal? Temperature, int? MaxTokens, string? SystemPrompt, bool Active);

    [HttpPost]
    public Task<IActionResult> Add(Input i) => Save(null, i);

    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, Input i) => Save(id, i);

    private async Task<IActionResult> Save(int? id, Input i)
    {
        var a = id == null ? new AiAgent() : await Automations.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 80) return BadRequest(new { error = "Give the automation a name (up to 80 characters)." });
        if (i.Activity == null || !Deputy.Activities.ContainsKey(i.Activity) || Deputy.ConversationActivities.Contains(i.Activity)) return BadRequest(new { error = "Pick what it does." });
        if (string.IsNullOrWhiteSpace(i.Schedule)) return BadRequest(new { error = "Automations run on a schedule. Enter one, like */15 * * * *." });
        if (!Cron.IsValid(i.Schedule)) return BadRequest(new { error = "Use a cron schedule like */15 * * * * (every 15 minutes) or 0 6 * * * (6am daily)." });
        if (i.AiModelId == null || !await db.AiModels.AnyAsync(m => m.Id == i.AiModelId)) return BadRequest(new { error = "Pick a model." });
        if (i.FallbackModelId != null && (i.FallbackModelId == i.AiModelId || !await db.AiModels.AnyAsync(m => m.Id == i.FallbackModelId))) return BadRequest(new { error = "Pick a different model as the fallback." });
        if (i.Temperature is not (>= 0 and <= 2)) return BadRequest(new { error = "Temperature is between 0 and 2." });
        if (i.MaxTokens is not (>= 256 and <= 64000)) return BadRequest(new { error = "Max tokens is between 256 and 64,000." });
        if (string.IsNullOrWhiteSpace(i.SystemPrompt)) return BadRequest(new { error = "Write the automation's instructions." });
        (a.Name, a.Activity, a.Description, a.Schedule, a.AiModelId, a.FallbackModelId) = (i.Name.Trim(), i.Activity, string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(), i.Schedule.Trim(), i.AiModelId.Value, i.FallbackModelId);
        (a.Temperature, a.MaxTokens, a.SystemPrompt, a.Active, a.UpdatedAt) = (i.Temperature!.Value, i.MaxTokens!.Value, i.SystemPrompt.Trim(), i.Active, DateTime.UtcNow);
        if (id == null) db.AiAgents.Add(a);
        await db.SaveChangesAsync();
        return Ok(new { a.Id, message = $"{a.Name} {(id == null ? "added" : "saved")}" });
    }

    /// <summary>Pause or resume.</summary>
    [HttpPost("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        if (await Automations.FirstOrDefaultAsync(x => x.Id == id) is not { } a) return NotFound();
        a.Active = !a.Active;
        await db.SaveChangesAsync();
        return Ok(new { message = $"{a.Name}{(a.Active ? " resumed" : " paused")}" });
    }
}
