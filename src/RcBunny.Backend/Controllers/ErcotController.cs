using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// Corral → ERCOT: requests sent to the Texas grid operator for each meter. There's no ERCOT connection yet, so people
/// record the answer here. A rejection puts the account in the "Rejected by ERCOT" queue.
/// </summary>
[ApiController]
[Route("api/ercot")]
[RequirePermission("corral")]
public class ErcotController(AppDbContext db) : ControllerBase
{
    public static readonly string[] Statuses = ["Sent", "Accepted", "Rejected", "Completed"];

    [HttpGet]
    public async Task<object> List(string? status) => new
    {
        statuses = Statuses,
        counts = await db.ErcotTransactions.GroupBy(t => t.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(),
        transactions = await db.ErcotTransactions.Where(t => status == null || t.Status == status).OrderByDescending(t => t.SentAt).Take(300)
            .Select(t => new { t.Id, t.CustomerId, customer = t.Customer!.Name, t.Esiid, t.Type, t.Status, t.Message, t.SentAt })
            .ToListAsync(),
    };

    public record StatusUpdate(string Status, string? Message);

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, StatusUpdate update)
    {
        if (!Statuses.Contains(update.Status)) return BadRequest(new { error = "Pick a status." });
        var transaction = await db.ErcotTransactions.Include(t => t.Customer).FirstOrDefaultAsync(t => t.Id == id);
        if (transaction is null) return NotFound();
        transaction.Status = update.Status;
        transaction.Message = update.Message?.Trim();

        if (transaction.Customer is { } customer)
        {
            if (update.Status == "Rejected")
                db.WorkItems.Add(new WorkItem { CustomerId = customer.Id, Queue = "Rejected by ERCOT", Summary = $"{transaction.Type} rejected: {update.Message}" });
            if (update.Status == "Completed" && transaction.Type is "Switch" or "Move-In") customer.Status = "Active";
            var me = await Permissions.CurrentAsync(HttpContext);
            db.Notes.Add(new Note { CustomerId = customer.Id, Author = me!.Name, Body = $"ERCOT {transaction.Type}: {update.Status}. {update.Message}".Trim() });
        }
        await db.SaveChangesAsync();
        return Ok();
    }
}
