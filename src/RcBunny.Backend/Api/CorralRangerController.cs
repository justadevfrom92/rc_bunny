using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Backend.Access;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Api;

/// <summary>Corral pages beyond the generic screens: exception queue overview, CRM pipeline, phone orders.</summary>
[Route("api/corral"), RequirePerm("corral")]
public class CorralExtrasController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("queues")]
    public async Task<IActionResult> Queues()
    {
        var open = await db.WorkItems.Where(w => w.ResolvedAt == null).GroupBy(w => w.Queue)
            .Select(g => new { queue = g.Key, open = g.Count(), oldest = g.Min(w => w.CreatedAt) }).ToListAsync();
        var defs = AppConfig.Get("admin")["queues"]!.AsObject();
        return Ok(defs.Select(kv => new
        {
            key = kv.Key, name = kv.Value![0]!.ToString(), description = kv.Value[1]!.ToString(),
            open = open.FirstOrDefault(o => o.queue == kv.Key)?.open ?? 0,
            oldestDays = open.FirstOrDefault(o => o.queue == kv.Key) is { } o ? (int)(DateTime.UtcNow - o.oldest).TotalDays : (int?)null,
        }));
    }

    [HttpGet("crm/pipeline")]
    public async Task<IActionResult> Pipeline([FromQuery] bool mine = false)
    {
        var since = DateTime.UtcNow.AddDays(-60);
        var q = db.CrmLeads.Include(l => l.Owner).Where(l => l.ClosedAt == null || l.ClosedAt > since);
        if (mine) q = q.Where(l => l.OwnerId == Me.UserId);
        var leads = await q.OrderBy(l => l.NextFollowUpOn).ToListAsync();
        var stages = new[] { "new", "contacted", "qualified", "quote", "negotiation", "won", "lost" };
        return Ok(stages.Select(s => new
        {
            stage = s,
            value = leads.Where(l => l.Stage == s).Sum(l => l.EstValue),
            leads = leads.Where(l => l.Stage == s).Select(l => new { l.Id, l.Name, l.Company, l.EstValue, l.NextFollowUpOn, owner = l.Owner?.Name, l.Kind }),
        }));
    }

    [HttpGet("orders/options")]
    public async Task<IActionResult> OrderOptions([FromQuery] string type = "Resi", [FromQuery] string? zip = null)
    {
        int.TryParse(zip, out var z);
        var market = await db.ZipRanges.Include(r => r.Market).Where(r => z >= r.ZipFrom && z <= r.ZipTo).Select(r => r.Market).FirstOrDefaultAsync();
        var plans = await db.Plans.Where(p => p.Active && p.Type == type).OrderBy(p => p.Term).ThenBy(p => p.Name).Select(p => new { p.Id, p.Name, p.Internal, p.Term, p.Etf }).ToListAsync();
        return Ok(new { market = market == null ? null : new { market.Id, market.Name, market.Description }, plans, channels = await db.MarketingChannels.Where(c => c.Active).Select(c => new { c.Msid, c.Name }).ToListAsync() });
    }

    public record OrderRequest(string Type, string FirstName, string LastName, string? Company, string Email, string Phone, string Address, string? Unit, string City, string Zip,
        string? Esiid, int PlanId, DateOnly? StartDate, string EnrollmentType, string? Msid, string? PromoCode, bool Autopay, bool Paperless, string? Note);

    /// <summary>Create Order / Create Order - Biz: a new account taken by phone, just like a website sign-up.</summary>
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(OrderRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName)) return Problem422("Enter the customer's name.");
        if (!System.Net.Mail.MailAddress.TryCreate(r.Email, out _)) return Problem422("Enter a valid email.", "email");
        if (!int.TryParse(r.Zip, out var z)) return Problem422("Enter a 5-digit zip.", "zip");
        var market = await db.ZipRanges.Where(x => z >= x.ZipFrom && z <= x.ZipTo).Select(x => x.MarketId).FirstOrDefaultAsync();
        if (market == 0) return Problem422("We don't serve that zip code.", "zip");
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == r.PlanId && p.Active);
        if (plan is null) return Problem422("Pick a plan.", "planId");
        var biz = r.Type == "Small Business";
        var acct = await NextAccount(db);
        var c = new Customer
        {
            Account = acct, Ticket = "17651" + DateTime.UtcNow.Ticks.ToString()[^14..], Name = biz && !string.IsNullOrWhiteSpace(r.Company) ? r.Company! : $"{r.FirstName} {r.LastName}",
            FirstName = r.FirstName.Trim(), LastName = r.LastName.Trim(), Type = biz ? "Small Business" : "Residential", Email = r.Email.Trim().ToLowerInvariant(), Phone = r.Phone,
            Address = r.Address, Unit = r.Unit, City = r.City, Zip = r.Zip, MarketId = market, Esiid = string.IsNullOrWhiteSpace(r.Esiid) ? null : r.Esiid, PlanId = plan.Id,
            Status = "Submitted", Exception = string.IsNullOrWhiteSpace(r.Esiid) ? "No ESIID" : null, Source = "Phone", EnrollmentType = r.EnrollmentType, MoveSwitch = r.EnrollmentType == "MoveIn" ? "move" : "switch",
            RequestedStart = r.StartDate, StartDate = r.StartDate, Msid = r.Msid ?? "2001 - Call Center", Channel = "2 - Agent", PromoCode = r.PromoCode, Autopay = r.Autopay, Paperless = r.Paperless,
            RateClass = biz ? "Commercial" : "Residential", BillingStreet = r.Address, BillingCity = r.City, BillingState = "TX", BillingZip = r.Zip, ReferralCode = "REF" + acct[^5..],
        };
        db.Customers.Add(c);
        c.Notes.Add(new Note { UserId = Me.UserId, Author = Me.Name, Category = "Order", Action = "New Enrollment", Disposition = "New Enrollment", Body = $"Phone order taken by {Me.Name}: {plan.Name}." + (string.IsNullOrWhiteSpace(r.Note) ? "" : " " + r.Note) });
        db.QueueLogs.Add(new QueueLog { Customer = c, Queue = "QueueCartFulfilledFlowing", EnteredAt = DateTime.UtcNow, UserId = Me.UserId, Note = "Phone order" });
        if (c.Exception != null) db.WorkItems.Add(new WorkItem { Customer = c, Queue = "unprocessed-orders", Summary = "No ESIID on a phone order" });
        Audit(db, "order_created", c.Account, plan.Internal);
        await db.SaveChangesAsync();
        return Ok(new { c.Id, c.Account });
    }

    public static async Task<string> NextAccount(AppDbContext db)
    {
        var max = (await db.Customers.Select(c => c.Account).ToListAsync()).Select(a => long.TryParse(a, out var n) ? n : 0).DefaultIfEmpty(1219000000).Max();
        return (max + 1).ToString();
    }
}

/// <summary>
/// Ranger: the ticket queue and conversation. Who sees what: in-office people see every ticket; a 3rd-party rep sees
/// unassigned tickets and their own; a 3rd-party manager or company admin sees unassigned tickets and their company's.
/// Managers (and admins) can assign, take over and join in.
/// </summary>
[Route("api/ranger"), RequirePerm("ranger")]
public class RangerController(AppDbContext db) : ApiControllerBase
{
    private async Task<RangerAgent?> MyAgent() => await db.RangerAgents.FirstOrDefaultAsync(a => a.UserId == Me.UserId && a.Active);

    private async Task<bool> IsManager() => Me.Has("sheriff") || (await MyAgent())?.Level is "manager" or "company_admin";

    private async Task<IQueryable<SupportTicket>> Visible()
    {
        var q = db.SupportTickets.AsQueryable();
        if (!Me.IsExternal) return q;
        var agent = await MyAgent();
        if (agent?.Level is "manager" or "company_admin")
        {
            var mates = await db.Users.Where(u => u.Company == Me.Company).Select(u => u.Id).ToListAsync();
            return q.Where(t => t.AssignedTo == null || mates.Contains(t.AssignedTo.Value) || t.Watchers.Any(w => w.UserId == Me.UserId));
        }
        return q.Where(t => t.AssignedTo == null || t.AssignedTo == Me.UserId || t.Watchers.Any(w => w.UserId == Me.UserId));
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var q = await Visible();
        var since = DateTime.UtcNow.AddDays(-30);
        var open = q.Where(t => t.Status != "solved" && t.Status != "closed");
        var responded = await q.Where(t => t.FirstResponseAt != null && t.CreatedAt > since).Select(t => new { t.CreatedAt, t.FirstResponseAt }).ToListAsync();
        var rated = await q.Where(t => t.Rating != null && t.RatedAt > since).Select(t => t.Rating).ToListAsync();
        return Ok(new
        {
            open = await open.CountAsync(), unassigned = await open.CountAsync(t => t.AssignedTo == null), urgent = await open.CountAsync(t => t.Priority == "urgent" || t.Priority == "high"),
            mine = await open.CountAsync(t => t.AssignedTo == Me.UserId), waiting = await open.CountAsync(t => t.Status == "waiting"),
            managerRequests = await open.CountAsync(t => t.ManagerRequestedAt != null),
            solved30 = await q.CountAsync(t => t.SolvedAt > since),
            avgFirstResponseMinutes = responded.Count == 0 ? 0 : (int)responded.Average(t => (t.FirstResponseAt!.Value - t.CreatedAt).TotalMinutes),
            approval = rated.Count == 0 ? (int?)null : (int)Math.Round(100.0 * rated.Count(r => r == "good") / rated.Count),
            byTopic = await open.GroupBy(t => t.Topic).Select(g => new { topic = g.Key, n = g.Count() }).ToListAsync(),
            isManager = await IsManager(),
            messages = await db.ContactMessages.CountAsync(m => m.HandledAt == null),
        });
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> Tickets([FromQuery] string view = "open", [FromQuery] string? q = null, [FromQuery] int page = 1)
    {
        var query = await Visible();
        query = view switch
        {
            "mine" => query.Where(t => t.AssignedTo == Me.UserId && t.Status != "solved" && t.Status != "closed"),
            "unassigned" => query.Where(t => t.AssignedTo == null && t.Status != "solved" && t.Status != "closed"),
            "urgent" => query.Where(t => (t.Priority == "urgent" || t.Priority == "high") && t.Status != "solved" && t.Status != "closed"),
            "joined" => query.Where(t => t.Watchers.Any(w => w.UserId == Me.UserId)),
            "manager" => query.Where(t => t.ManagerRequestedAt != null && t.Status != "solved" && t.Status != "closed"),
            "solved" => query.Where(t => t.Status == "solved" || t.Status == "closed"),
            "all" => query,
            _ => query.Where(t => t.Status != "solved" && t.Status != "closed"),
        };
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(t => t.Subject.Contains(q) || t.Name.Contains(q) || t.Email.Contains(q));
        var total = await query.CountAsync();
        // Urgent first, then the ones waiting longest for us
        var rows = await query.OrderBy(t => t.Priority == "urgent" ? 0 : t.Priority == "high" ? 1 : t.Priority == "normal" ? 2 : 3).ThenBy(t => t.LastActivityAt)
            .Skip((Math.Max(1, page) - 1) * 50).Take(50)
            .Select(t => new { t.Id, t.Subject, t.Name, t.Status, t.Priority, t.Channel, t.Topic, t.LastActivityAt, t.CreatedAt, assignee = t.AssignedToUser != null ? t.AssignedToUser.Name : null, manager = t.ManagerRequestedAt != null, t.Rating })
            .ToListAsync();
        return Ok(new { rows, total });
    }

    private async Task<SupportTicket?> Find(int id, bool tracking = false)
    {
        var q = await Visible();
        if (tracking) q = q.AsTracking();
        return await q.FirstOrDefaultAsync(t => t.Id == id);
    }

    [HttpGet("tickets/{id:int}")]
    public async Task<IActionResult> Ticket(int id)
    {
        var t = await Find(id);
        if (t is null) return NotFound();
        var customer = t.CustomerId is { } cid ? await db.Customers.Include(c => c.Plan).Where(c => c.Id == cid).Select(c => new { c.Id, c.Account, c.Name, c.Status, c.Balance, plan = c.Plan != null ? c.Plan.Name : null, c.Phone, c.Email }).FirstOrDefaultAsync() : null;
        var messages = await db.SupportTicketMessages.Where(m => m.SupportTicketId == id).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => new { m.Id, m.Author, m.Body, m.Via, m.Direction, m.Duration, m.CreatedAt, who = m.User != null ? m.User.Name : null, kind = "message" }).ToListAsync();
        var notes = await db.Notes.Where(n => n.SupportTicketId == id).Select(n => new { n.Id, Author = "note", n.Body, Via = (string?)null, Direction = (string?)null, Duration = (int?)null, n.CreatedAt, who = (string?)n.Author, kind = "note" }).ToListAsync();
        var files = await db.SupportTicketAttachments.Where(a => a.SupportTicketId == id).Select(a => new { a.Id, a.Name, a.Mime, a.Size, a.FromCustomer, a.CreatedAt }).ToListAsync();
        var watchers = await db.SupportTicketWatchers.Where(w => w.SupportTicketId == id).Select(w => new { w.UserId, w.User!.Name }).ToListAsync();
        var team = await db.RangerAgents.Where(a => a.Active).Select(a => new { a.UserId, a.User!.Name, a.Team, a.Level, a.User.Company }).ToListAsync();
        if (Me.IsExternal) team = team.Where(a => a.Company == Me.Company).ToList();
        return Ok(new
        {
            ticket = new { t.Id, t.Subject, t.Name, t.Email, t.Status, t.Priority, t.Topic, t.Channel, t.AssignedTo, t.CreatedAt, t.FirstResponseAt, t.SolvedAt, t.Rating, t.RatingComment, t.ManagerRequestedAt, t.ManagerRequestNote },
            customer, timeline = messages.Concat(notes).OrderBy(m => m.CreatedAt), files, watchers, team,
            can = new { manage = await IsManager(), joined = watchers.Any(w => w.UserId == Me.UserId), mine = t.AssignedTo == Me.UserId },
        });
    }

    private async Task<IActionResult> Change(int id, Func<SupportTicket, Task<string?>> change)
    {
        var t = await Find(id, true);
        if (t is null) return NotFound();
        string? err;
        try { err = await change(t); } catch (Resources.ActionError e) { err = e.Message; }
        if (err != null) return Problem422(err);
        t.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private void System(SupportTicket t, string body) => db.SupportTicketMessages.Add(new SupportTicketMessage { SupportTicketId = t.Id, Author = "system", Body = body, UserId = Me.UserId, Via = "portal" });

    public record ReplyRequest(string Body, string? Status);

    /// <summary>A reply the customer sees (My Account / email). Picking up an unassigned ticket assigns it to you.</summary>
    [HttpPost("tickets/{id:int}/reply")]
    public Task<IActionResult> Reply(int id, ReplyRequest r) => Change(id, t =>
    {
        if (string.IsNullOrWhiteSpace(r.Body)) return Task.FromResult<string?>("Write a reply.");
        if (t.AssignedTo == null) { t.AssignedTo = Me.UserId; System(t, $"{Me.Name} picked this up."); }
        db.SupportTicketMessages.Add(new SupportTicketMessage { SupportTicketId = t.Id, Author = "agent", Body = r.Body.Trim(), UserId = Me.UserId, Via = t.Channel == "Email" ? "email" : "portal", Contact = t.Channel == "Email" ? t.Email : null });
        t.FirstResponseAt ??= DateTime.UtcNow;
        t.Status = r.Status is "waiting" or "solved" ? r.Status : t.Status == "new" ? "open" : t.Status;
        if (t.Status == "solved") t.SolvedAt = DateTime.UtcNow;
        return Task.FromResult<string?>(null);
    });

    public record NoteRequest(string Body, string? Priority);

    /// <summary>Internal note: on the ticket and the customer's account, never shown to the customer.</summary>
    [HttpPost("tickets/{id:int}/notes")]
    public Task<IActionResult> AddNote(int id, NoteRequest r) => Change(id, t =>
    {
        if (string.IsNullOrWhiteSpace(r.Body)) return Task.FromResult<string?>("Write a note.");
        if (t.CustomerId is null) return Task.FromResult<string?>("This ticket isn't linked to an account.");
        db.Notes.Add(new Note { CustomerId = t.CustomerId.Value, UserId = Me.UserId, Author = Me.Name, Category = "Ticket", Action = "Internal Note", Priority = r.Priority ?? "Low", Body = r.Body.Trim(), SupportTicketId = t.Id, Disposition = $"T{t.Id}" });
        return Task.FromResult<string?>(null);
    });

    public record UpdateRequest(string? Status, string? Priority, int? AssignedTo);

    [HttpPatch("tickets/{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateRequest r)
    {
        var manager = await IsManager();
        return await Change(id, async t =>
        {
            if (r.Status is { } s && s != t.Status)
            {
                if (!new[] { "new", "open", "waiting", "solved", "closed" }.Contains(s)) return "Pick a status.";
                System(t, $"{Me.Name} set the status to {char.ToUpper(s[0]) + s[1..]}.");
                t.Status = s;
                if (s is "solved" or "closed") t.SolvedAt ??= DateTime.UtcNow;
            }
            if (r.Priority is { } p && p != t.Priority) { t.Priority = p; System(t, $"{Me.Name} set the priority to {p}."); }
            if (r.AssignedTo != t.AssignedTo && r.AssignedTo is not null)
            {
                if (!manager) return "Only managers can assign tickets.";
                var who = await db.Users.FirstOrDefaultAsync(u => u.Id == r.AssignedTo);
                if (who is null || (Me.IsExternal && who.Company != Me.Company)) return "Pick someone on your team.";
                t.AssignedTo = who.Id;
                System(t, $"{Me.Name} assigned this to {who.Name}.");
                db.AdminNotifications.Add(new AdminNotification { UserId = who.Id, Kind = "ticket", Title = $"Assigned to you: {t.Subject}", Url = $"/ranger/tickets/{t.Id}" });
            }
            return null;
        });
    }

    [HttpPost("tickets/{id:int}/take-over")]
    public async Task<IActionResult> TakeOver(int id)
    {
        if (!await IsManager()) return Problem422("Only managers can take over a ticket.");
        return await Change(id, async t =>
        {
            var from = t.AssignedTo is { } a ? await db.Users.Where(u => u.Id == a).Select(u => u.Name).FirstAsync() : null;
            if (t.AssignedTo is { } prev && prev != Me.UserId && !await db.SupportTicketWatchers.AnyAsync(w => w.SupportTicketId == t.Id && w.UserId == prev))
                db.SupportTicketWatchers.Add(new SupportTicketWatcher { SupportTicketId = t.Id, UserId = prev, JoinedAt = DateTime.UtcNow });
            t.AssignedTo = Me.UserId;
            t.ManagerRequestedAt = null; t.ManagerRequestNote = null;
            System(t, from is null ? $"{Me.Name} took this ticket." : $"{Me.Name} took over this ticket from {from}.");
            return null;
        });
    }

    [HttpPost("tickets/{id:int}/join")]
    public async Task<IActionResult> Join(int id)
    {
        if (!await IsManager()) return Problem422("Only managers can join in.");
        return await Change(id, async t =>
        {
            if (await db.SupportTicketWatchers.AnyAsync(w => w.SupportTicketId == t.Id && w.UserId == Me.UserId)) return "You've already joined.";
            db.SupportTicketWatchers.Add(new SupportTicketWatcher { SupportTicketId = t.Id, UserId = Me.UserId, JoinedAt = DateTime.UtcNow });
            System(t, $"{Me.Name} joined in.");
            return null;
        });
    }

    [HttpPost("tickets/{id:int}/leave")]
    public Task<IActionResult> Leave(int id) => Change(id, async t =>
    {
        var w = await db.SupportTicketWatchers.AsTracking().FirstOrDefaultAsync(x => x.SupportTicketId == t.Id && x.UserId == Me.UserId);
        if (w is null) return "You haven't joined.";
        db.SupportTicketWatchers.Remove(w);
        System(t, $"{Me.Name} left.");
        return null;
    });

    public record AskRequest(string? Note);

    [HttpPost("tickets/{id:int}/ask-manager")]
    public Task<IActionResult> AskManager(int id, AskRequest r) => Change(id, async t =>
    {
        if (t.ManagerRequestedAt != null) return "A manager has already been asked.";
        t.ManagerRequestedAt = DateTime.UtcNow; t.ManagerRequestedBy = Me.UserId; t.ManagerRequestNote = r.Note;
        if (t.CustomerId is { } cid && !string.IsNullOrWhiteSpace(r.Note))
            db.Notes.Add(new Note { CustomerId = cid, UserId = Me.UserId, Author = Me.Name, Category = "Ticket", Action = "Manager Request", Priority = "Medium", Body = r.Note!, SupportTicketId = t.Id });
        var managers = await db.RangerAgents.Where(a => a.Active && (a.Level == "manager" || a.Level == "company_admin")).Select(a => new { a.UserId, a.User!.Company }).ToListAsync();
        foreach (var m in managers.Where(m => m.Company == Me.Company))
            db.AdminNotifications.Add(new AdminNotification { UserId = m.UserId, Kind = "ticket", Title = $"Manager requested: {t.Subject}", Body = r.Note, Url = $"/ranger/tickets/{t.Id}" });
        System(t, $"{Me.Name} asked for a manager.");
        return null;
    });

    [HttpPost("tickets/{id:int}/cancel-manager")]
    public Task<IActionResult> CancelManager(int id) => Change(id, t =>
    {
        t.ManagerRequestedAt = null; t.ManagerRequestNote = null;
        System(t, $"{Me.Name} cancelled the manager request.");
        return Task.FromResult<string?>(null);
    });

    /// <summary>Approval Ratings: 👍 out of rated tickets, in-office vs 3rd party, by week and per person.</summary>
    [HttpGet("satisfaction")]
    public async Task<IActionResult> Satisfaction([FromQuery] int weeks = 12)
    {
        var since = DateTime.UtcNow.AddDays(-7 * weeks);
        var rated = await db.SupportTickets.Where(t => t.Rating != null && t.RatedAt > since && t.AssignedTo != null)
            .Select(t => new { t.Rating, t.RatedAt, t.AssignedTo, name = t.AssignedToUser!.Name, company = t.AssignedToUser.Company, t.RatingComment, t.Id }).ToListAsync();
        if (Me.IsExternal) rated = rated.Where(r => r.company == Me.Company).ToList();
        static int Pct(IEnumerable<string?> r) { var l = r.ToList(); return l.Count == 0 ? 0 : (int)Math.Round(100.0 * l.Count(x => x == "good") / l.Count); }
        var byWeek = rated.GroupBy(r => r.RatedAt!.Value.Date.AddDays(-(int)r.RatedAt.Value.DayOfWeek)).OrderBy(g => g.Key)
            .Select(g => new { week = g.Key.ToString("yyyy-MM-dd"), office = Pct(g.Where(x => x.company == null).Select(x => x.Rating)), thirdParty = Pct(g.Where(x => x.company != null).Select(x => x.Rating)), n = g.Count() });
        var byPerson = rated.GroupBy(r => (r.AssignedTo, r.name, r.company)).Select(g => new { userId = g.Key.AssignedTo, g.Key.name, team = g.Key.company ?? "In-office", rated = g.Count(), approval = Pct(g.Select(x => x.Rating)) }).OrderByDescending(x => x.approval);
        return Ok(new
        {
            office = Pct(rated.Where(r => r.company == null).Select(r => r.Rating)), thirdParty = Pct(rated.Where(r => r.company != null).Select(r => r.Rating)), rated = rated.Count,
            byWeek, byPerson, recent = rated.OrderByDescending(r => r.RatedAt).Where(r => r.RatingComment != null).Take(15).Select(r => new { r.Id, r.Rating, r.RatingComment, r.name, r.RatedAt }),
        });
    }

    /// <summary>Employees: the people a manager looks after (3rd-party company admins see only their company).</summary>
    [HttpGet("employees")]
    public async Task<IActionResult> Employees()
    {
        var q = db.Users.Include(u => u.Role).Where(u => u.Active);
        if (Me.IsExternal) q = q.Where(u => u.Company == Me.Company);
        var users = await q.OrderBy(u => u.Name).ToListAsync();
        var agents = await db.RangerAgents.ToDictionaryAsync(a => a.UserId);
        var open = await db.SupportTickets.Where(t => t.AssignedTo != null && t.Status != "solved" && t.Status != "closed").GroupBy(t => t.AssignedTo).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(g => g.Key!.Value, g => g.n);
        return Ok(users.Select(u => new { u.Id, u.Name, u.Title, u.Email, u.Company, role = u.Role?.Name, onTeam = agents.ContainsKey(u.Id), level = agents.GetValueOrDefault(u.Id)?.Level, openTickets = open.GetValueOrDefault(u.Id), u.LastLoginAt }));
    }
}
