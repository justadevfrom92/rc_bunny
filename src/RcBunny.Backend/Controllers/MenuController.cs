using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// The red counts next to menu links (ebunny's AdminMenu::badge): unassigned tickets, open web messages, unanswered
/// texts, open exceptions, failed crons… Only counts above zero are sent.
/// </summary>
[ApiController]
[Route("api/menu")]
public class MenuController(AppDbContext db) : ControllerBase
{
    [HttpGet("badges")]
    public async Task<IActionResult> Badges()
    {
        if (await Permissions.CurrentAsync(HttpContext) is null) return Unauthorized();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var counts = new Dictionary<string, int>();
        foreach (var q in await db.WorkItems.Where(w => w.ResolvedAt == null).GroupBy(w => w.Queue).Select(g => new { g.Key, n = g.Count() }).ToListAsync())
            counts["queue:" + q.Key] = q.n;
        counts["queues"] = counts.Values.Sum();

        // Texts whose latest message on the account came in from the customer
        var latestTexts = db.ContactLogs.Where(l => l.Channel == "SMS").GroupBy(l => l.CustomerId).Select(g => g.Max(l => l.Id));
        counts["sms"] = await db.ContactLogs.CountAsync(l => latestTexts.Contains(l.Id) && l.Direction == "in");
        counts["messages"] = await db.ContactMessages.CountAsync(m => m.HandledAt == null);
        counts["ranger_unassigned"] = await db.SupportTickets.CountAsync(t => Ranger.Active.Contains(t.Status) && t.AssignedToId == null);
        counts["ledger_pending"] = await db.LedgerEntries.CountAsync(e => e.Status == "pending");
        counts["refunds_pending"] = await db.Refunds.CountAsync(r => r.Status == "requested" || r.Status == "approved");
        counts["crm_due"] = await db.CrmActivities.CountAsync(a => a.DueOn != null && a.DoneAt == null && a.DueOn <= today);
        counts["redemptions_open"] = await db.StarEntries.CountAsync(s => s.OfferId != null && s.Fulfillment == "pending");

        // Crons whose latest run failed, and health checks whose latest result is bad
        var runs = await db.JobRuns.GroupBy(r => r.Job).Select(g => g.OrderByDescending(r => r.StartedAt).First().Ok).ToListAsync();
        counts["failed_jobs"] = runs.Count(ok => !ok);
        var checks = await db.HealthChecks.GroupBy(c => c.Name).Select(g => g.OrderByDescending(c => c.CheckedAt).First().Ok).ToListAsync();
        counts["health_problems"] = checks.Count(ok => !ok);

        return Ok(counts.Where(c => c.Value > 0).ToDictionary());
    }
}
