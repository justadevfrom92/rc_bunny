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

    [HttpGet("heartbeat")]
    public async Task<object> Heartbeat()
    {
        var latest = await db.HealthChecks.GroupBy(c => c.Name).Select(g => g.OrderByDescending(c => c.CheckedAt).First()).ToListAsync();
        return new { checks = latest, controls = await db.HeartbeatControls.OrderBy(c => c.Name).ToListAsync() };
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

    [HttpGet("timeline")]
    public async Task<List<AuditEntry>> Timeline(string? category) =>
        await db.AuditEntries.Where(a => category == null || a.Category == category).OrderByDescending(a => a.CreatedAt).Take(300).ToListAsync();

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
    public async Task<object> Markets() => await db.Markets.OrderBy(m => m.Name).Select(m => new
    {
        m.Id, m.Code, m.Name, m.DeliveryCentsPerKwh, m.DeliveryPerMonth,
        zips = db.ZipRanges.Where(z => z.MarketId == m.Id).Select(z => new { z.Id, z.From, z.To }).ToList(),
        customers = db.Customers.Count(c => c.MarketId == m.Id),
    }).ToListAsync();

    public record MarketInput(string Code, string Name, decimal DeliveryCentsPerKwh, decimal DeliveryPerMonth, int? ZipFrom, int? ZipTo);

    [HttpPost("markets")]
    public async Task<IActionResult> AddMarket(MarketInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Enter a code and a name." });
        if (await db.Markets.AnyAsync(m => m.Code == input.Code.Trim())) return BadRequest(new { error = "There's already a market with that code." });
        var market = new Market { Code = input.Code.Trim().ToUpper(), Name = input.Name.Trim(), DeliveryCentsPerKwh = input.DeliveryCentsPerKwh, DeliveryPerMonth = input.DeliveryPerMonth };
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
    };

    private object Integration(string name, string usedFor, string setting) =>
        new { name, usedFor, setting, configured = !string.IsNullOrEmpty(config[setting]) };

    private async Task<string> MyName() => (await Permissions.CurrentAsync(HttpContext))!.Name;
}
