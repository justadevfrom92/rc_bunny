using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Jobs;

namespace RcBunny.Controllers;

/// <summary>Sheriff: heartbeat, timeline, crons, caches, markets and integrations. (Staff and roles are in StaffController.)</summary>
[ApiController]
[Route("api/sheriff")]
[RequirePermission("sheriff")]
public class SheriffController(AppDbContext db, IMemoryCache cache, IConfiguration config) : ControllerBase
{
    // ---- Heartbeat --------------------------------------------------------------------------------------------

    /// <summary>
    /// Heartbeat (ebunny's sheriff/heartbeat): overall status, uptime, the last 24 hours in 15-minute blocks, each check
    /// now, the crons and the latest actions. A control that's switched off (the website) shows as Taken Down.
    /// </summary>
    [HttpGet("heartbeat")]
    public async Task<object> Heartbeat()
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var recent = await db.HealthChecks.Where(c => c.CheckedAt >= since).ToListAsync();
        var latest = await db.HealthChecks.GroupBy(c => c.Name).Select(g => g.OrderByDescending(c => c.CheckedAt).First()).ToListAsync();
        var controls = await db.HeartbeatControls.OrderBy(c => c.Name).ToListAsync();
        var runs = await db.JobRuns.OrderByDescending(r => r.StartedAt).Take(500).ToListAsync();
        var start = DateTime.UtcNow.AddMinutes(-15 * 96);
        var blocks = Enumerable.Range(0, 96).Select(i =>
        {
            var from = start.AddMinutes(15 * i);
            var inBlock = recent.Where(c => c.CheckedAt >= from && c.CheckedAt < from.AddMinutes(15)).ToList();
            return new { from, n = inBlock.Count, status = inBlock.Count == 0 ? "none" : inBlock.Any(c => !c.Ok) ? "bad" : "ok",
                rows = inBlock.Where(c => !c.Ok).Select(c => new { c.Name, c.Detail, c.CheckedAt }) };
        }).ToList();
        var checks = latest.Select(c => new { key = c.Name, name = c.Name, status = c.Ok ? "ok" : "bad", detail = c.Detail, checkedAt = c.CheckedAt, control = (int?)null, reason = (string?)null })
            .Concat(controls.Select(c => new { key = c.Key, name = c.Name, status = c.Down ? "down" : "ok", detail = c.Down ? $"Taken down: {c.Reason}" : "Up", checkedAt = c.ChangedAt, control = (int?)c.Id, reason = c.Reason }))
            .ToList();
        return new
        {
            status = checks.Any(c => c.status == "bad") ? "bad" : checks.Any(c => c.status == "down") ? "down" : "ok",
            lastCheck = latest.Count == 0 ? (DateTime?)null : latest.Max(c => c.CheckedAt),
            uptime = recent.Count == 0 ? (double?)null : Math.Round(100.0 * recent.Count(c => c.Ok) / recent.Count, 1),
            beats24 = recent.Count, blocks, checks,
            scheduler = runs.FirstOrDefault()?.StartedAt,
            failed24 = runs.Count(r => !r.Ok && r.StartedAt >= since),
            crons = RcBunny.Jobs.Scheduler.All.Select(j => new { j.Name, j.Schedule, last = runs.FirstOrDefault(r => r.Job == j.Name) }),
            actions = await db.History.OrderByDescending(h => h.CreatedAt).Take(10).Select(h => new { h.Id, h.CreatedAt, h.Who, h.Summary }).ToListAsync(),
            controls,
        };
    }

    [HttpGet("heartbeat/checks")]
    public async Task<List<HealthCheck>> Checks() => await db.HealthChecks.OrderByDescending(c => c.CheckedAt).Take(300).ToListAsync();

    [HttpPost("heartbeat/run")]
    public async Task<JobRun> RunChecks() =>
        await Scheduler.RunAsync(HttpContext.RequestServices, Scheduler.All.First(j => j.Name == "Heartbeat checks"), await MyName());

    public record ControlInput(bool Down, string? Reason);

    /// <summary>Flip a switch, e.g. take the website down for maintenance. The website notices within 30 seconds.</summary>
    [HttpPost("heartbeat/controls/{id:int}")]
    public async Task<IActionResult> SetControl(int id, ControlInput input)
    {
        var control = await db.HeartbeatControls.FindAsync(id);
        if (control is null) return NotFound();
        if (input.Down && string.IsNullOrWhiteSpace(input.Reason)) return BadRequest(new { error = "Say why (the website shows it)." });
        control.Down = input.Down;
        control.Reason = input.Down ? input.Reason!.Trim() : null;
        control.ChangedAt = DateTime.UtcNow;
        Audit.Add(db, await MyName(), "heartbeat", input.Down ? $"Took {control.Name} down: {control.Reason}" : $"Brought {control.Name} back up.");
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- Timeline ---------------------------------------------------------------------------------------------

    public record TimelineEvent(string Src, int Seq, DateTime At, string Kind, string Status, string Title, string? Detail, int? UserId, string? Who);

    /// <summary>
    /// Timeline (ebunny's sheriff/timeline): everything that ran or happened, newest first: cron jobs, heartbeat
    /// controls, sign-ins and access changes, and every change people made. Search, dates, kinds, status, one person.
    /// </summary>
    [HttpGet("timeline")]
    public async Task<object> Timeline(string? q, DateOnly? from, DateOnly? to, string? kind, string? status, int? user, int page = 1)
    {
        var users = await db.Users.ToDictionaryAsync(u => u.Name, u => u.Id);
        var events = (await db.JobRuns.ToListAsync()).Select(r => new TimelineEvent("job_run", r.Id, r.StartedAt, "job", r.Ok ? "ok" : "bad", r.Job, r.Message,
                r.RunBy != null && users.TryGetValue(r.RunBy, out var id) ? id : null, r.RunBy))
            .Concat((await db.AuditEntries.ToListAsync()).Select(a => new TimelineEvent("audit_entry", a.Id, a.CreatedAt, a.Category == "heartbeat" ? "job" : "user", "ok",
                a.Category switch { "sign-in" => "Signed in", "access" => "Access", "heartbeat" => "Heartbeat control", "market" => "Markets", _ => a.Category }, a.What,
                users.TryGetValue(a.Who, out var id) ? id : null, a.Who)))
            .Concat((await db.AiAutomationRuns.Include(r => r.Agent).ToListAsync()).Select(r => new TimelineEvent("automation_run", r.Id, r.StartedAt, "automation", r.Status == "ok" ? "ok" : "bad",
                r.Agent?.Name ?? "Automation", r.Summary, null, null)))
            .Concat((await db.History.ToListAsync()).Select(h => new TimelineEvent("history_entry", h.Id, h.CreatedAt, "user", "ok", h.Model, h.Summary, h.UserId, h.Who)))
            .ToList();
        var person = user == null ? null : await db.Users.Where(u => u.Id == user).Select(u => new { u.Id, u.Name }).FirstOrDefaultAsync();
        IEnumerable<TimelineEvent> shown = events;
        if (user != null) shown = shown.Where(e => e.UserId == user);
        if (!string.IsNullOrWhiteSpace(q)) shown = shown.Where(e => $"{e.Title} {e.Detail} {e.Who}".Contains(q.Trim(), StringComparison.OrdinalIgnoreCase));
        if (from != null) shown = shown.Where(e => DateOnly.FromDateTime(e.At.ToLocalTime()) >= from);
        if (to != null) shown = shown.Where(e => DateOnly.FromDateTime(e.At.ToLocalTime()) <= to);
        var kinds = (kind ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (kinds.Length > 0) shown = shown.Where(e => kinds.Contains(e.Kind));
        var list = shown.ToList();
        var counts = list.GroupBy(e => e.Status).ToDictionary(g => g.Key, g => g.Count());
        if (!string.IsNullOrEmpty(status)) list = list.Where(e => e.Status == status).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new
        {
            person, total = list.Count, page, counts,
            days = Enumerable.Range(0, 14).Select(i => today.AddDays(i - 13)).Select(d =>
            {
                var day = events.Where(e => (user == null || e.UserId == user) && DateOnly.FromDateTime(e.At.ToLocalTime()) == d).ToList();
                return new { date = d, n = day.Count, bad = day.Count(e => e.Status == "bad"), warn = day.Count(e => e.Status == "warn") };
            }),
            events = list.OrderByDescending(e => e.At).Skip((Math.Max(page, 1) - 1) * 25).Take(25),
        };
    }

    /// <summary>Control Log (ebunny's sheriff/heartbeat-log): every heartbeat control anyone used.</summary>
    [HttpGet("heartbeat/log")]
    public async Task<object> ControlLog() =>
        await db.AuditEntries.Where(a => a.Category == "heartbeat").OrderByDescending(a => a.CreatedAt).Take(200).ToListAsync();

    // ---- Crons ------------------------------------------------------------------------------------------------

    [HttpGet("jobs")]
    public async Task<object> Jobs()
    {
        var runs = await db.JobRuns.OrderByDescending(r => r.StartedAt).Take(500).ToListAsync();
        return new
        {
            jobs = Scheduler.All.Select(j => new { j.Name, j.Schedule, last = runs.FirstOrDefault(r => r.Job == j.Name) }),
            runs = runs.Take(100),
        };
    }

    [HttpPost("jobs/run")]
    public async Task<IActionResult> RunJob([FromQuery] string name)
    {
        var job = Scheduler.All.FirstOrDefault(j => j.Name == name);
        if (job is null) return NotFound();
        return Ok(await Scheduler.RunAsync(HttpContext.RequestServices, job, await MyName()));
    }

    // ---- Caches -----------------------------------------------------------------------------------------------

    /// <summary>Clears the admin's memory cache (who can do what). The website keeps its own one-minute cache.</summary>
    [HttpPost("caches/clear")]
    public async Task<IActionResult> ClearCaches()
    {
        if (cache is MemoryCache memory) memory.Compact(1.0);
        Audit.Add(db, await MyName(), "access", "Cleared the admin caches.");
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- Markets ----------------------------------------------------------------------------------------------

    [HttpGet("markets")]
    public async Task<object> Markets() => await db.Markets.OrderBy(m => m.Id).Select(m => new
    {
        m.Id, m.Code, m.Name, m.Short, m.Region, m.Phone, m.DeliveryCentsPerKwh, m.DeliveryPerMonth,
        zips = db.ZipRanges.Where(z => z.MarketId == m.Id).Select(z => new { z.Id, z.From, z.To }).ToList(),
        customers = db.Customers.Count(c => c.MarketId == m.Id),
    }).ToListAsync();

    public record MarketInput(string Code, string Name, decimal DeliveryCentsPerKwh, decimal DeliveryPerMonth, int? ZipFrom, int? ZipTo, string? Short = null, string? Region = null, string? Phone = null);

    [HttpPost("markets")]
    public async Task<IActionResult> AddMarket(MarketInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Enter a code and a name." });
        if (await db.Markets.AnyAsync(m => m.Code == input.Code.Trim())) return BadRequest(new { error = "There's already a market with that code." });
        var market = new Market { Code = input.Code.Trim().ToUpper(), Name = input.Name.Trim(), DeliveryCentsPerKwh = input.DeliveryCentsPerKwh, DeliveryPerMonth = input.DeliveryPerMonth,
            Short = input.Short?.Trim() ?? "", Region = input.Region?.Trim() ?? "", Phone = input.Phone?.Trim() ?? "" };
        db.Markets.Add(market);
        await db.SaveChangesAsync();
        var problem = await AddZips(market.Id, input.ZipFrom, input.ZipTo);
        Audit.Add(db, await MyName(), "market", $"Added the {market.Name} market.");
        await db.SaveChangesAsync();
        return problem is null ? Ok() : BadRequest(new { error = $"Market added, but {problem}" });
    }

    [HttpPut("markets/{id:int}")]
    public async Task<IActionResult> UpdateMarket(int id, MarketInput input)
    {
        var market = await db.Markets.FindAsync(id);
        if (market is null) return NotFound();
        market.Name = input.Name.Trim();
        (market.Short, market.Region, market.Phone) = (input.Short?.Trim() ?? market.Short, input.Region?.Trim() ?? market.Region, input.Phone?.Trim() ?? market.Phone);
        market.DeliveryCentsPerKwh = input.DeliveryCentsPerKwh;
        market.DeliveryPerMonth = input.DeliveryPerMonth;
        var problem = await AddZips(id, input.ZipFrom, input.ZipTo);
        Audit.Add(db, await MyName(), "market", $"Changed the {market.Name} market: {input.DeliveryCentsPerKwh}¢/kWh + ${input.DeliveryPerMonth}/month delivery.");
        await db.SaveChangesAsync();
        return problem is null ? Ok() : BadRequest(new { error = problem });
    }

    [HttpDelete("markets/zips/{id:int}")]
    public async Task<IActionResult> RemoveZips(int id)
    {
        await db.ZipRanges.Where(z => z.Id == id).ExecuteDeleteAsync();
        return Ok();
    }

    private async Task<string?> AddZips(int marketId, int? from, int? to)
    {
        if (from is null && to is null) return null;
        if (from is not { } f || to is not { } t || f > t || f < 70000 || t > 79999) return "zip codes must be a Texas range (70000–79999), lowest first.";
        if (await db.ZipRanges.AnyAsync(z => z.From <= t && z.To >= f)) return "those zips overlap another market's.";
        db.ZipRanges.Add(new ZipRange { MarketId = marketId, From = f, To = t });
        return null;
    }

    // ---- Integrations -----------------------------------------------------------------------------------------

    /// <summary>The outside services ebunny used, and whether each has its settings. None are connected in this version yet.</summary>
    [HttpGet("integrations")]
    public object Integrations() => new[]
    {
        Integration("Stripe", "Card payments (My Account, AutoPay)", "Integrations:Stripe:SecretKey"),
        Integration("Twilio", "Text messages (Corral → SMS)", "Integrations:Twilio:AuthToken"),
        Integration("ERCOT", "Switch and move-in requests (Corral → ERCOT)", "Integrations:Ercot:Certificate"),
        Integration("Experian", "Credit checks on new orders", "Integrations:Experian:ApiKey"),
        Integration("SMTP", "Emails to customers", "Integrations:Smtp:Host"),
        Integration("Open Models API", "Hosted open-weight models for Deputy (Qwen, Kimi, DeepSeek…) through an OpenAI-compatible endpoint", "Integrations:OpenModels:Url"),
        Integration("Local Models", "Downloaded open-weight models for Deputy, served by Ollama on your own server", "Integrations:LocalModels:Url"),
    };

    private object Integration(string name, string usedFor, string setting) =>
        new { name, usedFor, setting, configured = !string.IsNullOrEmpty(config[setting]) };

    private async Task<string> MyName() => (await Permissions.CurrentAsync(HttpContext))!.Name;
}
