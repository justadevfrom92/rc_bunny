using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Corral → Exceptions: accounts waiting for someone, grouped into queues.</summary>
[ApiController]
[Route("api/queues")]
[RequirePermission("corral")]
public class QueuesController(AppDbContext db) : ControllerBase
{
    public static readonly string[] Queues = ["No ESIID", "Not sent to Utility", "Possible Duplicate", "Switch Hold", "Rejected by ERCOT"];

    /// <summary>Every queue with how many open items it has.</summary>
    [HttpGet]
    public async Task<object> Counts()
    {
        var open = await db.WorkItems.Where(w => w.ResolvedAt == null).GroupBy(w => w.Queue).Select(g => new { queue = g.Key, count = g.Count() }).ToListAsync();
        return Queues.Union(open.Select(o => o.queue)).Select(q => new { queue = q, count = open.FirstOrDefault(o => o.queue == q)?.count ?? 0 });
    }

    [HttpGet("items")]
    public async Task<object> Items(string queue) => await db.WorkItems.Where(w => w.Queue == queue && w.ResolvedAt == null).OrderBy(w => w.CreatedAt)
        .Select(w => new { w.Id, w.CustomerId, customer = w.Customer!.Name, account = w.Customer.Account, status = w.Customer.Status, w.Summary, w.CreatedAt })
        .ToListAsync();

    [HttpPost("items/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id)
    {
        var item = await db.WorkItems.FindAsync(id);
        if (item is null) return NotFound();
        var me = await Permissions.CurrentAsync(HttpContext);
        item.ResolvedAt = DateTime.UtcNow;
        item.ResolvedBy = me!.Name;
        db.Notes.Add(new Note { CustomerId = item.CustomerId, Author = me.Name, Body = $"Resolved \"{item.Queue}\": {item.Summary}" });
        await db.SaveChangesAsync();
        return Ok();
    }
}
