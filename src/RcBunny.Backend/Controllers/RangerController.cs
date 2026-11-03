using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// Ranger: support tickets. Anyone with the ranger permission can work tickets; ticket team managers can also assign
/// tickets to anyone and take tickets over.
/// </summary>
[ApiController]
[Route("api/ranger")]
[RequirePermission("ranger")]
public class RangerController(AppDbContext db) : ControllerBase
{
    public static readonly string[] Statuses = ["new", "open", "waiting", "solved", "closed"];

    [HttpGet("dashboard")]
    public async Task<object> Dashboard()
    {
        var me = await Me();
        var open = db.SupportTickets.Where(t => t.Status != "solved" && t.Status != "closed");
        var rated = await db.SupportTickets.Where(t => t.Rating != null).Select(t => t.Rating).ToListAsync();
        return new
        {
            byStatus = await db.SupportTickets.GroupBy(t => t.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(),
            unassigned = await open.CountAsync(t => t.AssignedToId == null),
            mine = await open.CountAsync(t => t.AssignedToId == me.Id),
            highPriority = await open.CountAsync(t => t.Priority == "high"),
            approval = rated.Count == 0 ? (int?)null : 100 * rated.Count(r => r == "good") / rated.Count,
            oldest = await open.OrderBy(t => t.CreatedAt).Take(5).Select(t => new { t.Id, t.Subject, t.Name, t.CreatedAt }).ToListAsync(),
        };
    }

    /// <summary>Tickets, filtered by status (or "active" for anything not solved/closed) and by who has them.</summary>
    [HttpGet("tickets")]
    public async Task<object> Tickets(string status = "active", string who = "all")
    {
        var me = await Me();
        var query = db.SupportTickets.AsQueryable();
        query = status == "active" ? query.Where(t => t.Status != "solved" && t.Status != "closed") : query.Where(t => t.Status == status);
        if (who == "mine") query = query.Where(t => t.AssignedToId == me.Id);
        if (who == "unassigned") query = query.Where(t => t.AssignedToId == null);
        return await query.OrderByDescending(t => t.Priority == "high").ThenBy(t => t.UpdatedAt).Take(300)
            .Select(t => new { t.Id, t.Subject, t.Name, t.Topic, t.Status, t.Priority, assignedTo = t.AssignedTo!.Name, t.CreatedAt, t.UpdatedAt })
            .ToListAsync();
    }

    [HttpGet("tickets/{id:int}")]
    public async Task<IActionResult> Ticket(int id)
    {
        var me = await Me();
        var ticket = await db.SupportTickets.Include(t => t.AssignedTo).Include(t => t.Customer).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();
        return Ok(new
        {
            ticket.Id, ticket.Subject, ticket.Name, ticket.Email, ticket.Topic, ticket.Status, ticket.Priority, ticket.CreatedAt, ticket.SolvedAt,
            ticket.Rating, ticket.RatingComment, ticket.AssignedToId, assignedTo = ticket.AssignedTo?.Name,
            customer = ticket.Customer is { } c ? new { c.Id, c.Account, c.Name } : null,
            messages = await db.TicketMessages.Where(m => m.TicketId == id).OrderBy(m => m.CreatedAt).ToListAsync(),
            statuses = Statuses,
            isManager = await IsManager(me.Id),
            team = await db.RangerAgents.Where(a => a.Active).Select(a => new { a.UserId, a.User!.Name }).ToListAsync(),
        });
    }

    public record NewMessage(string Body, bool Internal);

    /// <summary>Reply to the customer, or add an internal note (staff only).</summary>
    [HttpPost("tickets/{id:int}/messages")]
    public async Task<IActionResult> AddMessage(int id, NewMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Body)) return BadRequest(new { error = "Write something first." });
        var ticket = await db.SupportTickets.FindAsync(id);
        if (ticket is null) return NotFound();
        var me = await Me();
        db.TicketMessages.Add(new TicketMessage { TicketId = id, Author = me.Name, Internal = message.Internal, Body = message.Body.Trim() });
        if (!message.Internal)
        {
            ticket.FirstResponseAt ??= DateTime.UtcNow;
            if (ticket.Status is "new" or "open") ticket.Status = "waiting"; // waiting on the customer
        }
        ticket.AssignedToId ??= me.Id;
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }

    public record TicketUpdate(string Status, string Priority);

    [HttpPut("tickets/{id:int}")]
    public async Task<IActionResult> Update(int id, TicketUpdate update)
    {
        if (!Statuses.Contains(update.Status)) return BadRequest(new { error = "Pick a status." });
        var ticket = await db.SupportTickets.FindAsync(id);
        if (ticket is null) return NotFound();
        ticket.Status = update.Status;
        ticket.Priority = update.Priority == "high" ? "high" : "normal";
        ticket.SolvedAt = update.Status is "solved" or "closed" ? ticket.SolvedAt ?? DateTime.UtcNow : null;
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Assign a ticket. Anyone can pick up an unassigned ticket for themselves; managers can assign to anyone or take it over.</summary>
    [HttpPost("tickets/{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, [FromQuery] int? userId)
    {
        var ticket = await db.SupportTickets.FindAsync(id);
        if (ticket is null) return NotFound();
        var me = await Me();
        var target = userId ?? me.Id;
        var pickingUp = target == me.Id && ticket.AssignedToId == null;
        if (!pickingUp && !await IsManager(me.Id)) return BadRequest(new { error = "Only ticket team managers can reassign tickets." });
        if (userId != null && !await db.RangerAgents.AnyAsync(a => a.UserId == userId && a.Active)) return BadRequest(new { error = "They're not on the ticket team." });
        ticket.AssignedToId = target;
        if (ticket.Status == "new") ticket.Status = "open";
        ticket.UpdatedAt = DateTime.UtcNow;
        db.TicketMessages.Add(new TicketMessage { TicketId = id, Author = me.Name, Internal = true, Body = target == me.Id ? $"{me.Name} took this ticket." : "Reassigned." });
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- Team -------------------------------------------------------------------------------------------------

    [HttpGet("team")]
    public async Task<object> Team() => new
    {
        agents = await db.RangerAgents.OrderBy(a => a.User!.Name).Select(a => new
        {
            a.Id, a.UserId, a.User!.Name, a.User.Email, a.Team, a.IsManager, a.Active,
            open = db.SupportTickets.Count(t => t.AssignedToId == a.UserId && t.Status != "solved" && t.Status != "closed"),
            solved = db.SupportTickets.Count(t => t.AssignedToId == a.UserId && (t.Status == "solved" || t.Status == "closed")),
        }).ToListAsync(),
        notOnTeam = await db.Users.Where(u => u.Active && !db.RangerAgents.Any(a => a.UserId == u.Id)).OrderBy(u => u.Name).Select(u => new { u.Id, u.Name }).ToListAsync(),
    };

    public record AgentInput(int UserId, string Team, bool IsManager, bool Active);

    [HttpPost("team")]
    public async Task<IActionResult> AddAgent(AgentInput input)
    {
        if (!await IsManager((await Me()).Id)) return BadRequest(new { error = "Only ticket team managers can change the team." });
        if (await db.RangerAgents.AnyAsync(a => a.UserId == input.UserId)) return BadRequest(new { error = "They're already on the team." });
        db.RangerAgents.Add(new RangerAgent { UserId = input.UserId, Team = input.Team == "3rd party" ? "3rd party" : "In-office", IsManager = input.IsManager });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("team/{id:int}")]
    public async Task<IActionResult> UpdateAgent(int id, AgentInput input)
    {
        if (!await IsManager((await Me()).Id)) return BadRequest(new { error = "Only ticket team managers can change the team." });
        var agent = await db.RangerAgents.FindAsync(id);
        if (agent is null) return NotFound();
        agent.Team = input.Team == "3rd party" ? "3rd party" : "In-office";
        agent.IsManager = input.IsManager;
        agent.Active = input.Active;
        await db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Approval ratings customers gave solved tickets.</summary>
    [HttpGet("ratings")]
    public async Task<object> Ratings()
    {
        var rated = await db.SupportTickets.Include(t => t.AssignedTo).Where(t => t.Rating != null).OrderByDescending(t => t.SolvedAt).ToListAsync();
        return new
        {
            total = rated.Count,
            good = rated.Count(t => t.Rating == "good"),
            byAgent = rated.GroupBy(t => t.AssignedTo?.Name ?? "Unassigned").Select(g => new { agent = g.Key, total = g.Count(), good = g.Count(t => t.Rating == "good") }),
            recent = rated.Take(50).Select(t => new { t.Id, t.Subject, t.Name, t.Rating, t.RatingComment, agent = t.AssignedTo?.Name, t.SolvedAt }),
        };
    }

    /// <summary>Every employee, and whether they're on the ticket team.</summary>
    [HttpGet("employees")]
    public async Task<object> Employees() => await db.Users.Include(u => u.Role).OrderBy(u => u.Name)
        .Select(u => new { u.Id, u.Name, u.Email, role = u.Role!.Name, u.Active, team = db.RangerAgents.Where(a => a.UserId == u.Id).Select(a => a.Team).FirstOrDefault() })
        .ToListAsync();

    private async Task<Permissions.SignedIn> Me() => (await Permissions.CurrentAsync(HttpContext))!;

    private Task<bool> IsManager(int userId) => db.RangerAgents.AnyAsync(a => a.UserId == userId && a.IsManager && a.Active);
}
