using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// Ranger: the support tickets customers open from My Account (or send by email, or call about). The queue, each
/// ticket's activity (replies the customer sees, calls and notes they don't), and for managers Take Over and Join In.
/// Also the ticket team, approval ratings and everyone who has handled a ticket.
/// </summary>
[ApiController]
[Route("api/ranger")]
[RequirePermission("ranger")]
public class RangerController(AppDbContext db, IConfiguration config, IWebHostEnvironment env, IMemoryCache cache) : ControllerBase
{
    // ---- Dashboard --------------------------------------------------------------------------------------------

    [HttpGet("dashboard")]
    public async Task<object> Dashboard()
    {
        var me = await Me();
        var since = DateTime.UtcNow.AddDays(-30);
        var today = DateTime.Today.ToUniversalTime();
        var active = db.SupportTickets.Where(t => Ranger.Active.Contains(t.Status));
        var teams = new List<object>();
        foreach (var (team, label) in Ranger.Teams)
            teams.Add(new { team, label, approval = await Approval(TeamTickets(team).Where(t => t.RatedAt >= since)) });
        return new
        {
            counts = new
            {
                @new = await db.SupportTickets.CountAsync(t => t.Status == "new"),
                unassigned = await active.CountAsync(t => t.AssignedToId == null),
                open = await active.CountAsync(),
                solvedToday = await db.SupportTickets.CountAsync(t => t.SolvedAt >= today),
            },
            overall = await Approval(db.SupportTickets.Where(t => t.RatedAt >= since)),
            teams,
            open = await Rows(ByUrgency(active)),
            managerRequests = await active.Where(t => t.ManagerRequestedAt != null).OrderBy(t => t.ManagerRequestedAt)
                .Select(t => new { t.Id, number = "RT-" + (10000 + t.Id), t.Subject, t.Name, t.ManagerRequestedAt, requestedBy = t.ManagerRequestedBy!.Name, t.ManagerRequestNote })
                .ToListAsync(),
            manager = await IsManager(me),
            lists = Lists(),
        };
    }

    // ---- Tickets ----------------------------------------------------------------------------------------------

    /// <summary>
    /// The ticket lists (active, mine, unassigned, manager requests, watching, done, all) with their counts, and the
    /// search: words or a ticket number in the text, notes and/or people, plus filters. 25 to a page.
    /// </summary>
    [HttpGet("tickets")]
    public async Task<object> Tickets(string? view, string? q, string? @in, string? status, string? priority, string? topic, string? channel,
        DateOnly? from, DateOnly? to, string? team, int? owner, int? handledBy, int? customer, int page = 1)
    {
        var me = await Me();
        view ??= string.IsNullOrWhiteSpace(q) ? "active" : "all"; // a search looks through every ticket unless a list was picked
        var active = db.SupportTickets.Where(t => Ranger.Active.Contains(t.Status));
        var watching = db.SupportTickets.Where(t => db.TicketWatchers.Any(w => w.TicketId == t.Id && w.UserId == me.Id));
        var done = db.SupportTickets.Where(t => t.Status == "solved" || t.Status == "closed");

        var query = view switch
        {
            "active" => active,
            "mine" => active.Where(t => t.AssignedToId == me.Id),
            "unassigned" => active.Where(t => t.AssignedToId == null),
            "manager" => active.Where(t => t.ManagerRequestedAt != null),
            "watching" => watching,
            "done" => done,
            _ => db.SupportTickets,
        };
        if (customer != null) query = query.Where(t => t.CustomerId == customer);
        if (status != null) query = query.Where(t => t.Status == status);
        if (owner != null) query = query.Where(t => t.AssignedToId == owner);
        if (team != null) query = query.Where(t => db.RangerAgents.Any(a => a.Team == team && a.UserId == t.AssignedToId));
        if (!string.IsNullOrWhiteSpace(q)) query = Search(query, q.Trim(), (@in ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));
        if (priority != null) query = query.Where(t => t.Priority == priority);
        if (topic != null) query = query.Where(t => t.Topic == topic);
        if (channel != null) query = query.Where(t => t.Channel == channel);
        if (from != null) query = query.Where(t => t.CreatedAt >= from.Value.ToDateTime(TimeOnly.MinValue));
        if (to != null) query = query.Where(t => t.CreatedAt < to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (handledBy != null) query = HandledBy(query, handledBy.Value);

        var ordered = view is "done" or "all" ? query.OrderByDescending(t => t.UpdatedAt) : ByUrgency(query);
        return new
        {
            view, page, total = await query.CountAsync(),
            tickets = await Rows(ordered.Skip((page - 1) * 25).Take(25)),
            counts = new
            {
                active = await active.CountAsync(), mine = await active.CountAsync(t => t.AssignedToId == me.Id),
                unassigned = await active.CountAsync(t => t.AssignedToId == null), manager = await active.CountAsync(t => t.ManagerRequestedAt != null),
                watching = await watching.CountAsync(), done = await done.CountAsync(), all = await db.SupportTickets.CountAsync(),
            },
            // Everyone who has owned or answered a ticket, for the Owner and Handled By filters
            people = await db.Users.Where(u => db.SupportTickets.Any(t => t.AssignedToId == u.Id) || db.TicketMessages.Any(m => m.UserId == u.Id && m.Kind == "agent"))
                .OrderBy(u => u.Name).Select(u => new { u.Id, u.Name }).ToListAsync(),
            customerName = customer == null ? null : await db.Customers.Where(c => c.Id == customer).Select(c => c.Name).FirstOrDefaultAsync(),
            manager = await IsManager(me),
            lists = Lists(),
        };
    }

    [HttpGet("tickets/{id:int}")]
    public async Task<IActionResult> Ticket(int id)
    {
        var me = await Me();
        var t = await db.SupportTickets.Include(x => x.AssignedTo).Include(x => x.Customer).ThenInclude(c => c!.Plan).Include(x => x.ManagerRequestedBy)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var company = await CompanyOf(me);
        var watchers = await db.TicketWatchers.Where(w => w.TicketId == id).OrderBy(w => w.JoinedAt).Select(w => new { w.UserId, w.User!.Name, w.JoinedAt }).ToListAsync();
        var attachments = await db.TicketAttachments.Where(a => a.TicketId == id).OrderBy(a => a.CreatedAt).ToListAsync();
        var userIds = await db.TicketMessages.Where(m => m.TicketId == id && m.UserId != null).Select(m => m.UserId!.Value).Distinct().ToListAsync();
        var teamOf = await db.RangerAgents.Where(a => userIds.Contains(a.UserId) || a.UserId == t.AssignedToId).ToDictionaryAsync(a => a.UserId.ToString(), a => Ranger.Teams[a.Team]);
        var c = t.Customer;
        var uploaderIds = attachments.Where(a => a.UserId != null).Select(a => a.UserId!.Value).ToList();
        var names = await db.Users.Where(u => uploaderIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
        return Ok(new
        {
            t.Id, t.Number, t.Subject, t.Name, t.Email, t.Topic, topicLabel = Ranger.Topics.GetValueOrDefault(t.Topic, t.Topic), t.Channel,
            t.Status, t.Priority, t.CreatedAt, t.UpdatedAt, t.FirstResponseAt, t.SolvedAt, t.Rating, t.RatingComment, t.RatedAt,
            t.AssignedToId, assignedTo = t.AssignedTo?.Name, assignedCompany = t.AssignedTo?.Company,
            t.ManagerRequestedAt, managerRequestedBy = t.ManagerRequestedBy?.Name, t.ManagerRequestNote,
            customer = c == null ? null : new { c.Id, c.Account, c.Name, c.Email, c.Phone, c.Status, plan = c.Plan?.Name, c.Address, c.Balance },
            otherTickets = c == null ? 0 : await db.SupportTickets.CountAsync(x => x.CustomerId == c.Id),
            messages = (await db.TicketMessages.Where(m => m.TicketId == id).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync())
                .Select(m => new { m.Id, m.Kind, m.Author, m.UserId, m.Via, m.Direction, m.DurationSec, m.Contact, m.Body, m.CreatedAt, files = attachments.Where(a => a.MessageId == m.Id).Select(File) }),
            notes = await db.Notes.Where(n => n.SupportTicketId == id).OrderByDescending(n => n.CreatedAt).ToListAsync(),
            attachments = attachments.OrderByDescending(a => a.CreatedAt).Select(a => new { file = File(a), by = a.FromCustomer ? t.Name : names.GetValueOrDefault(a.UserId ?? 0, "Support") }),
            watchers, teamOf,
            me = me.Id, manager = await IsManager(me),
            agents = await db.RangerAgents.Where(a => a.Active && (company == null || a.User!.Company == company)).OrderBy(a => a.User!.Name)
                .Select(a => new { a.UserId, a.User!.Name, team = a.Team, a.Level }).ToListAsync(),
            history = c == null ? 0 : await db.SupportTickets.CountAsync(x => x.CustomerId == c.Id),
            noteCategories = Corral.NoteCategories, priorities = Corral.Priorities,
            lists = Lists(),
        });
    }

    public record ReplyInput(string? Body, string? Via, string? Direction, int? Minutes, string? Status);

    /// <summary>A reply to the customer (in My Account or by email), or a phone call logged with its direction and length; optionally a new status.</summary>
    [HttpPost("tickets/{id:int}/reply")]
    public async Task<IActionResult> Reply(int id, ReplyInput input)
    {
        var (t, me, problem) = await Workable(id);
        if (problem != null) return problem;
        if (string.IsNullOrWhiteSpace(input.Body)) return BadRequest(new { error = "Write the message first." });
        var via = input.Via is "email" or "phone" ? input.Via : "portal";
        if (input.Status is not (null or "" or "open" or "waiting" or "solved")) return BadRequest(new { error = "Pick a status." });
        var m = Ranger.Post(db, t!, "agent", me.Name, input.Body.Trim(), me.Id);
        m.Via = via;
        if (via == "phone")
        {
            m.Direction = input.Direction == "in" ? "in" : "out";
            m.DurationSec = Math.Clamp(input.Minutes ?? 0, 0, 600) * 60;
            m.Contact = await db.Customers.Where(c => c.Id == t!.CustomerId).Select(c => c.Phone).FirstOrDefaultAsync();
        }
        if (via == "email") m.Contact = t!.Email;
        t!.AssignedToId ??= me.Id; // whoever answers first owns it
        if (!string.IsNullOrEmpty(input.Status))
        {
            t.Status = input.Status;
            t.SolvedAt = input.Status == "solved" ? DateTime.UtcNow : null;
        }
        await db.SaveChangesAsync();
        var done = via switch { "email" => "Email sent", "phone" => "Phone call logged", _ => "Reply sent" };
        return Ok(new { message = done + (string.IsNullOrEmpty(input.Status) ? "" : " · " + Ranger.Statuses[input.Status].Label) });
    }

    public record NoteInput(string? Body, string? Category, string? Action, string? Priority);

    /// <summary>A note on the ticket: an account note (category, action, priority), staff only. Anyone on the team can add one.</summary>
    [HttpPost("tickets/{id:int}/notes")]
    public async Task<IActionResult> AddNote(int id, NoteInput input)
    {
        var t = await db.SupportTickets.FindAsync(id);
        if (t is null) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Body)) return BadRequest(new { error = "Write the note first." });
        if (input.Category is null || !Corral.NoteCategories.TryGetValue(input.Category, out var actions)) return BadRequest(new { error = "Pick a category." });
        if (input.Action is null || !actions.Contains(input.Action)) return BadRequest(new { error = "Pick an action from that category." });
        if (!Corral.Priorities.Contains(input.Priority)) return BadRequest(new { error = "Pick a priority." });
        AddNote(t, await Me(), input.Body.Trim(), input.Priority!, input.Category, input.Action);
        await db.SaveChangesAsync();
        return Ok(new { message = t.CustomerId != null ? "Note added (it's on the customer's account notes too)" : "Note added" });
    }

    public record TicketUpdate(string? Status, string? Priority, int? AssignedTo);

    /// <summary>Status, priority or owner. Anyone can pick a ticket up for themselves; handing one to someone else is for managers.</summary>
    [HttpPut("tickets/{id:int}")]
    public async Task<IActionResult> Update(int id, TicketUpdate input)
    {
        var t = await db.SupportTickets.FindAsync(id);
        if (t is null) return NotFound();
        var me = await Me();
        if (input.Status != null && !Ranger.Statuses.ContainsKey(input.Status)) return BadRequest(new { error = "Pick a status." });
        if (input.Priority != null && !Ranger.Priorities.ContainsKey(input.Priority)) return BadRequest(new { error = "Pick a priority." });
        if (input.AssignedTo is { } to && to != t.AssignedToId)
        {
            var agent = await db.RangerAgents.Include(a => a.User).FirstOrDefaultAsync(a => a.UserId == to && a.Active);
            if (agent is null) return BadRequest(new { error = "They're not on the ticket team." });
            if (to != me.Id && !await IsManager(me)) return Forbidden("Only managers can assign tickets to someone else.");
            if (await CompanyOf(me) is { } co && agent.User!.Company != co) return Forbidden($"You can only assign tickets to people from {co}.");
            t.AssignedToId = to;
            if (t.Status == "new") t.Status = "open";
            Ranger.Post(db, t, "system", me.Name, to == me.Id ? $"{me.Name} picked this up." : $"{me.Name} assigned this to {agent.User!.Name}.", me.Id);
        }
        if (input.Status != null && input.Status != t.Status)
        {
            Ranger.Post(db, t, "system", me.Name, $"{me.Name} set the status to {Ranger.Statuses[input.Status].Label}.", me.Id);
            t.Status = input.Status;
            t.SolvedAt = t.IsDone ? t.SolvedAt ?? DateTime.UtcNow : null;
        }
        if (input.Priority != null) t.Priority = input.Priority;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "Ticket updated" });
    }

    /// <summary>Managers: take the ticket over. It becomes yours; whoever had it stays on as a watcher.</summary>
    [HttpPost("tickets/{id:int}/take-over")]
    public async Task<IActionResult> TakeOver(int id)
    {
        var me = await Me();
        if (!await IsManager(me)) return Forbidden("Only managers can take tickets over.");
        var t = await db.SupportTickets.Include(x => x.AssignedTo).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (t.AssignedToId is { } had && had != me.Id) await Watch(t.Id, had);
        db.TicketWatchers.RemoveRange(db.TicketWatchers.Where(w => w.TicketId == id && w.UserId == me.Id));
        var from = t.AssignedTo?.Name;
        t.AssignedToId = me.Id;
        if (t.Status == "new") t.Status = "open";
        t.ManagerRequestedAt = null;
        Ranger.Post(db, t, "system", me.Name, $"{me.Name} took over this ticket{(from != null && from != me.Name ? " from " + from : "")}.", me.Id);
        await db.SaveChangesAsync();
        return Ok(new { message = $"You took over {t.Number}." });
    }

    /// <summary>Managers: join in (follow it and reply alongside whoever owns it).</summary>
    [HttpPost("tickets/{id:int}/join")]
    public async Task<IActionResult> Join(int id)
    {
        var me = await Me();
        if (!await IsManager(me)) return Forbidden("Only managers can join tickets.");
        var t = await db.SupportTickets.Include(x => x.AssignedTo).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        if (t.AssignedToId != me.Id && await Watch(t.Id, me.Id)) Ranger.Post(db, t, "system", me.Name, $"{me.Name} joined in.", me.Id);
        t.ManagerRequestedAt = null; // a manager is on it
        await db.SaveChangesAsync();
        return Ok(new { message = $"You joined {t.Number}. You can reply and add notes alongside {t.AssignedTo?.Name ?? "the team"}." });
    }

    [HttpPost("tickets/{id:int}/leave")]
    public async Task<IActionResult> Leave(int id)
    {
        var me = await Me();
        var t = await db.SupportTickets.FindAsync(id);
        if (t is null) return NotFound();
        db.TicketWatchers.RemoveRange(db.TicketWatchers.Where(w => w.TicketId == id && w.UserId == me.Id));
        Ranger.Post(db, t, "system", me.Name, $"{me.Name} left the ticket.", me.Id);
        await db.SaveChangesAsync();
        return Ok(new { message = $"You left {t.Number}." });
    }

    public record AskInput(string? Reason);

    /// <summary>Agents: ask a manager to help (it shows on the dashboard and under Manager Requests). The reason is saved as a note.</summary>
    [HttpPost("tickets/{id:int}/ask-manager")]
    public async Task<IActionResult> AskManager(int id, AskInput input)
    {
        var (t, me, problem) = await Workable(id);
        if (problem != null) return problem;
        var reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim()[..Math.Min(500, input.Reason.Trim().Length)];
        (t!.ManagerRequestedAt, t.ManagerRequestedById, t.ManagerRequestNote) = (DateTime.UtcNow, me.Id, reason);
        if (reason != null) AddNote(t, me, reason, "Medium", "Ticket", "Manager Request");
        await db.SaveChangesAsync();
        return Ok(new { message = $"A manager has been asked to help on {t.Number}." });
    }

    [HttpPost("tickets/{id:int}/cancel-manager")]
    public async Task<IActionResult> CancelManager(int id)
    {
        var (t, _, problem) = await Workable(id);
        if (problem != null) return problem;
        t!.ManagerRequestedAt = null;
        await db.SaveChangesAsync();
        return Ok(new { message = "Manager request cancelled." });
    }

    /// <summary>One attachment: who sent it, when, on which message, and the text of text files (images and PDFs preview from /file).</summary>
    [HttpGet("tickets/{id:int}/files/{fileId:int}")]
    public async Task<IActionResult> Attachment(int id, int fileId)
    {
        var a = await db.TicketAttachments.FirstOrDefaultAsync(x => x.Id == fileId && x.TicketId == id);
        if (a is null) return NotFound();
        var t = await db.SupportTickets.FindAsync(id);
        var message = await db.TicketMessages.FirstOrDefaultAsync(m => m.Id == a.MessageId);
        var full = Path.Combine(Storage, a.Path);
        string? text = null;
        if (a.IsText && System.IO.File.Exists(full))
        {
            text = await System.IO.File.ReadAllTextAsync(full);
            if (text.Length > 20000) text = text[..20000] + "…";
        }
        return Ok(new
        {
            ticket = new { t!.Id, t.Number, t.Subject, t.Name, t.CustomerId, account = await db.Customers.Where(c => c.Id == t.CustomerId).Select(c => c.Account).FirstOrDefaultAsync() },
            file = File(a), by = a.FromCustomer ? t.Name : await db.Users.Where(u => u.Id == a.UserId).Select(u => u.Name).FirstOrDefaultAsync(),
            message = message == null ? null : new { message.Id, message.Kind, message.Author, message.Via, message.Body, message.CreatedAt },
            text, exists = System.IO.File.Exists(full),
        });
    }

    /// <summary>The file itself: shown in the page (?inline=true, images and PDFs) or downloaded.</summary>
    [HttpGet("tickets/{id:int}/files/{fileId:int}/file")]
    public async Task<IActionResult> AttachmentFile(int id, int fileId, bool inline = false)
    {
        var a = await db.TicketAttachments.FirstOrDefaultAsync(x => x.Id == fileId && x.TicketId == id);
        var full = a == null ? null : Path.Combine(Storage, a.Path);
        if (a is null || !System.IO.File.Exists(full)) return NotFound();
        return inline && (a.IsImage || a.IsPdf) ? PhysicalFile(full, a.Mime) : PhysicalFile(full, a.Mime, a.Name);
    }

    // ---- Team -------------------------------------------------------------------------------------------------

    /// <summary>Everyone who works tickets, with their open tickets, solved and approval for the last 30/90/365 days.</summary>
    [HttpGet("team")]
    public async Task<object> Team(string? team, int days = 90)
    {
        var me = await Me();
        var company = await CompanyOf(me);
        var since = DateTime.UtcNow.AddDays(-Days(days));
        var agents = await db.RangerAgents.Include(a => a.User).Include(a => a.AddedBy)
            .Where(a => (team == null || a.Team == team) && (company == null || a.User!.Company == company)).ToListAsync();
        var ids = agents.Select(a => a.UserId).ToList();
        var tickets = await db.SupportTickets.Where(t => ids.Contains(t.AssignedToId!.Value))
            .Select(t => new { t.AssignedToId, t.Status, t.SolvedAt, t.RatedAt, t.Rating }).ToListAsync();
        return new
        {
            team, days = Days(days), company, manager = await IsManager(me),
            agents = agents.OrderByDescending(a => a.Active).ThenByDescending(a => a.Level).ThenBy(a => a.User!.Name).Select(a =>
            {
                var mine = tickets.Where(t => t.AssignedToId == a.UserId).ToList();
                var rated = mine.Count(t => t.RatedAt >= since);
                var good = mine.Count(t => t.RatedAt >= since && t.Rating == "good");
                return new
                {
                    a.Id, a.UserId, a.User!.Name, a.User.Email, a.User.Company, a.User.Title, a.Team, a.Level, a.Active, addedBy = a.AddedBy?.Name, a.CreatedAt,
                    open = mine.Count(t => Ranger.Active.Contains(t.Status)), solved = mine.Count(t => t.SolvedAt >= since),
                    approval = new { rated, good, pct = rated == 0 ? (double?)null : Math.Round(100.0 * good / rated, 1) },
                };
            }),
            lists = Lists(),
        };
    }

    /// <summary>The Add form: people already in the admin who aren't on the team yet.</summary>
    [HttpGet("team/add")]
    public async Task<object> AddForm()
    {
        var me = await Me();
        var company = await CompanyOf(me);
        return new
        {
            people = await db.Users.Where(u => u.Active && !db.RangerAgents.Any(a => a.UserId == u.Id) && (company == null || u.Company == company))
                .OrderBy(u => u.Name).Select(u => new { u.Id, u.Name, u.Email, role = u.Role!.Name, u.Company, u.Title }).ToListAsync(),
            manager = await IsManager(me), company, lists = Lists(),
        };
    }

    public record AgentInput(string? Who, int? UserId, string? Name, string? Email, string? Password, string? Company, string? Team, string? Level, string? Title);

    /// <summary>Add someone already in the admin, or a new person with a temporary password. Being on the team gives them Ranger.</summary>
    [HttpPost("team")]
    public async Task<IActionResult> AddAgent(AgentInput input)
    {
        var me = await Me();
        var manager = await IsManager(me);
        var company = await CompanyOf(me);
        var team = company != null ? "third_party" : input.Team; // a company admin adds people to their own company only
        var companyName = company ?? input.Company?.Trim();
        if (team is null || !Ranger.Teams.ContainsKey(team)) return BadRequest(new { error = "Pick a team." });
        if (input.Level is null || !Ranger.Levels.ContainsKey(input.Level)) return BadRequest(new { error = "Pick a level." });
        if (input.Level == "manager" && !manager) return Forbidden("Only a manager can add another manager.");
        if (input.Level == "company_admin" && (company != null || !manager || team != "third_party"))
            return Forbidden("Company admins are for 3rd-party companies and are added by an in-office manager or administrator.");

        User user;
        if (input.Who == "new")
        {
            if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Email)) return BadRequest(new { error = "Give their name and email." });
            var email = input.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Email == email)) return BadRequest(new { error = "Someone already has that email." });
            if ((input.Password ?? "").Length < 12) return BadRequest(new { error = "The temporary password needs at least 12 characters." });
            if (team == "third_party" && string.IsNullOrWhiteSpace(companyName)) return BadRequest(new { error = "Give the 3rd-party company they work for." });
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Customer Service") ?? await db.Roles.FirstAsync();
            user = new User
            {
                Name = input.Name.Trim(), Email = email, PasswordHash = Passwords.Hash(input.Password!), RoleId = role.Id,
                Company = team == "third_party" ? companyName : null, Title = string.IsNullOrWhiteSpace(input.Title) ? null : input.Title.Trim(),
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        else
        {
            var found = await db.Users.FindAsync(input.UserId ?? 0);
            if (found is null) return BadRequest(new { error = "Pick someone." });
            if (await db.RangerAgents.AnyAsync(a => a.UserId == found.Id)) return BadRequest(new { error = $"{found.Name} is already on the team." });
            if (company != null && found.Company != company) return Forbidden($"You can only add people from {company}.");
            user = found;
        }
        db.RangerAgents.Add(new RangerAgent { UserId = user.Id, Team = team, Level = input.Level, AddedById = me.Id });
        await db.SaveChangesAsync();
        cache.Remove(Permissions.CacheKey(user.Id));
        return Ok(new { message = $"{user.Name} can now work tickets" + (input.Who == "new" ? ". Share the temporary password with them securely." : "."), user.Id });
    }

    /// <summary>One team member: who they are, their numbers, open tickets, approval by week and what customers said.</summary>
    [HttpGet("team/{id:int}")]
    public async Task<IActionResult> Agent(int id)
    {
        var me = await Me();
        var a = await db.RangerAgents.Include(x => x.User).ThenInclude(u => u!.Role).Include(x => x.AddedBy).FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        var company = await CompanyOf(me);
        if (company != null && a.User!.Company != company) return Forbidden($"You can only see people from {company}.");
        var mine = db.SupportTickets.Where(t => t.AssignedToId == a.UserId);
        var approval = new Dictionary<string, object>();
        foreach (var d in new[] { 30, 90, 365 }) approval[d.ToString()] = await Approval(mine.Where(t => t.RatedAt >= DateTime.UtcNow.AddDays(-d)));
        var replied = await mine.Where(t => t.FirstResponseAt != null && t.CreatedAt >= DateTime.UtcNow.AddDays(-90)).Select(t => new { t.CreatedAt, t.FirstResponseAt }).ToListAsync();
        return Ok(new
        {
            agent = new { a.Id, a.UserId, a.User!.Name, a.User.Email, a.User.Company, a.User.Title, a.User.Department, role = a.User.Role!.Name, a.Team, a.Level, a.Active, addedBy = a.AddedBy?.Name, a.CreatedAt },
            approval,
            firstReplyMinutes = replied.Count == 0 ? (double?)null : Math.Round(replied.Average(t => (t.FirstResponseAt!.Value - t.CreatedAt).TotalMinutes)),
            weeks = await Weeks(mine),
            open = await Rows(mine.Where(t => Ranger.Active.Contains(t.Status)).OrderBy(t => t.CreatedAt)),
            solved = await mine.CountAsync(t => t.SolvedAt >= DateTime.UtcNow.AddDays(-90)),
            ratings = await mine.Where(t => t.RatedAt != null).OrderByDescending(t => t.RatedAt).Take(30)
                .Select(t => new { t.Id, number = "RT-" + (10000 + t.Id), t.Subject, t.Name, t.Rating, t.RatingComment, t.RatedAt }).ToListAsync(),
            canManage = await CanManage(me, a.User) && a.UserId != me.Id, company,
            lists = Lists(),
        });
    }

    public record AgentUpdate(string? Team, string? Level, bool? Active);

    /// <summary>Managers: change someone's team or level, or take them off tickets (their open tickets go back to the queue).</summary>
    [HttpPut("team/{id:int}")]
    public async Task<IActionResult> UpdateAgent(int id, AgentUpdate input)
    {
        var me = await Me();
        var a = await db.RangerAgents.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        if (!await CanManage(me, a.User!)) return Forbidden("Only a manager can change the team.");
        var team = input.Team;
        if (await CompanyOf(me) != null)
        {
            // A company admin can't move people out of their company or make another company admin
            team = null;
            if (input.Level == "company_admin" || a.UserId == me.Id) return Forbidden("You can't make that change.");
        }
        if (team != null && Ranger.Teams.ContainsKey(team)) a.Team = team;
        if (input.Level != null && Ranger.Levels.ContainsKey(input.Level)) a.Level = input.Level;
        if (input.Active is { } on)
        {
            a.Active = on;
            if (!on)
                await db.SupportTickets.Where(t => Ranger.Active.Contains(t.Status) && t.AssignedToId == a.UserId).ForEachAsync(t => t.AssignedToId = null);
        }
        await db.SaveChangesAsync();
        cache.Remove(Permissions.CacheKey(a.UserId));
        return Ok(new { message = $"{a.User!.Name} updated" });
    }

    // ---- Satisfaction -----------------------------------------------------------------------------------------

    /// <summary>Approval %: in-office vs 3rd party, each person, the weekly trend and what customers said.</summary>
    [HttpGet("satisfaction")]
    public async Task<object> Satisfaction(int days = 90, int page = 1)
    {
        var since = DateTime.UtcNow.AddDays(-Days(days));
        var rated = db.SupportTickets.Where(t => t.RatedAt >= since);
        var teams = new List<object>();
        foreach (var (team, label) in Ranger.Teams) teams.Add(new { team, label, approval = await Approval(rated.Where(t => db.RangerAgents.Any(a => a.Team == team && a.UserId == t.AssignedToId))) });
        var people = new List<(int Id, string Name, string Team, string? Company, ApprovalStats A)>();
        foreach (var a in await db.RangerAgents.Include(x => x.User).ToListAsync())
        {
            var ap = await Approval(rated.Where(t => t.AssignedToId == a.UserId));
            if (ap.Rated > 0) people.Add((a.Id, a.User!.Name, a.Team, a.User.Company, ap));
        }
        var weeks = new List<object>();
        foreach (var (from, until) in WeekRanges())
        {
            var row = new Dictionary<string, object> { ["week"] = from };
            foreach (var team in Ranger.Teams.Keys)
                row[team] = await Approval(db.SupportTickets.Where(t => t.RatedAt >= from && t.RatedAt < until && db.RangerAgents.Any(a => a.Team == team && a.UserId == t.AssignedToId)));
            weeks.Add(row);
        }
        var comments = rated.Where(t => t.RatingComment != null);
        return new
        {
            days = Days(days), overall = await Approval(rated), teams, weeks,
            people = people.OrderByDescending(p => p.A.Pct).ThenByDescending(p => p.A.Rated).Select(p => new { p.Id, p.Name, p.Team, p.Company, approval = p.A }),
            page, total = await comments.CountAsync(),
            comments = await comments.OrderByDescending(t => t.RatedAt).Skip((page - 1) * 10).Take(10)
                .Select(t => new { t.Id, number = "RT-" + (10000 + t.Id), t.Subject, t.Name, t.Rating, t.RatingComment, t.RatedAt, agent = t.AssignedTo!.Name }).ToListAsync(),
            lists = Lists(),
        };
    }

    // ---- Employees --------------------------------------------------------------------------------------------

    /// <summary>Everyone who has handled a ticket (owned, replied or noted): the ticket team and anyone else.</summary>
    [HttpGet("employees")]
    public async Task<object> Employees()
    {
        var company = await CompanyOf(await Me());
        var ids = await db.SupportTickets.Where(t => t.AssignedToId != null).Select(t => t.AssignedToId!.Value)
            .Union(db.TicketMessages.Where(m => (m.Kind == "agent" || m.Kind == "note") && m.UserId != null).Select(m => m.UserId!.Value))
            .Union(db.Notes.Where(n => n.SupportTicketId != null && n.UserId != null).Select(n => n.UserId!.Value)).ToListAsync();
        var people = await db.Users.Include(u => u.Role).Where(u => ids.Contains(u.Id) && (company == null || u.Company == company)).OrderBy(u => u.Name).ToListAsync();
        var agents = await db.RangerAgents.Where(a => ids.Contains(a.UserId)).ToDictionaryAsync(a => a.UserId);
        var rows = new List<object>();
        foreach (var u in people)
        {
            var handled = HandledBy(db.SupportTickets, u.Id);
            rows.Add(new
            {
                u.Id, u.Name, u.Email, u.Title, role = u.Role!.Name, u.Company, u.Active,
                agent = agents.TryGetValue(u.Id, out var a) ? new { a.Id, a.Team, a.Level, a.Active } : null,
                handled = await handled.CountAsync(),
                open = await db.SupportTickets.CountAsync(t => t.AssignedToId == u.Id && Ranger.Active.Contains(t.Status)),
                last = await handled.MaxAsync(t => (DateTime?)t.UpdatedAt),
            });
        }
        return new { people = rows, lists = Lists() };
    }

    /// <summary>One employee: who they are and every ticket they handled (owned, replied to or wrote a note on).</summary>
    [HttpGet("employees/{id:int}")]
    public async Task<IActionResult> Employee(int id, int page = 1)
    {
        var company = await CompanyOf(await Me());
        var u = await db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();
        if (company != null && u.Company != company) return Forbidden($"You can only see people from {company}.");
        var handled = HandledBy(db.SupportTickets, id);
        var owned = db.SupportTickets.Where(t => t.AssignedToId == id);
        var tickets = await Rows(handled.OrderByDescending(t => t.UpdatedAt).Skip((page - 1) * 10).Take(10));
        var ticketIds = tickets.Select(t => t.Id).ToList();
        var replies = await db.TicketMessages.Where(m => ticketIds.Contains(m.TicketId) && m.UserId == id && m.Kind == "agent").GroupBy(m => m.TicketId).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key.ToString(), x => x.n);
        var noteRows = await db.Notes.Where(n => ticketIds.Contains(n.SupportTicketId!.Value) && n.UserId == id).Select(n => n.SupportTicketId!.Value)
            .Concat(db.TicketMessages.Where(m => ticketIds.Contains(m.TicketId) && m.UserId == id && m.Kind == "note").Select(m => m.TicketId)).ToListAsync();
        return Ok(new
        {
            user = new { u.Id, u.Name, u.Email, u.Title, role = u.Role!.Name, u.Company, u.Active },
            agent = await db.RangerAgents.Where(a => a.UserId == id).Select(a => new { a.Id, a.Team, a.Level, a.Active }).FirstOrDefaultAsync(),
            stats = new
            {
                handled = await handled.CountAsync(), owned = await owned.CountAsync(), open = await owned.CountAsync(t => Ranger.Active.Contains(t.Status)),
                solved = await owned.CountAsync(t => t.SolvedAt >= DateTime.UtcNow.AddDays(-90)),
                replies = await db.TicketMessages.CountAsync(m => m.UserId == id && m.Kind == "agent"),
                approval = await Approval(owned.Where(t => t.RatedAt >= DateTime.UtcNow.AddDays(-90))),
            },
            page, total = await handled.CountAsync(), tickets, replies,
            notes = noteRows.GroupBy(x => x).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            lists = Lists(),
        });
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------

    public record ApprovalStats(int Rated, int Good, double? Pct);

    /// <summary>Good ratings out of all ratings for these tickets (Pct is null when nothing is rated).</summary>
    private static async Task<ApprovalStats> Approval(IQueryable<SupportTicket> tickets)
    {
        var ratings = await tickets.Where(t => t.Rating != null).Select(t => t.Rating).ToListAsync();
        var good = ratings.Count(r => r == "good");
        return new ApprovalStats(ratings.Count, good, ratings.Count == 0 ? null : Math.Round(100.0 * good / ratings.Count, 1));
    }

    /// <summary>Approval for each of the last 12 weeks.</summary>
    private static async Task<List<object>> Weeks(IQueryable<SupportTicket> tickets)
    {
        var weeks = new List<object>();
        foreach (var (from, until) in WeekRanges()) weeks.Add(new { week = from, approval = await Approval(tickets.Where(t => t.RatedAt >= from && t.RatedAt < until)) });
        return weeks;
    }

    /// <summary>The last 12 weeks (Monday to Monday), oldest first.</summary>
    private static IEnumerable<(DateTime From, DateTime Until)> WeekRanges()
    {
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        for (var w = 11; w >= 0; w--) yield return (monday.AddDays(-7 * w).ToUniversalTime(), monday.AddDays(-7 * w + 7).ToUniversalTime());
    }

    private static int Days(int days) => days is 30 or 365 ? days : 90;

    /// <summary>Urgent first, then high…, then new before open, then the longest waiting.</summary>
    private static IOrderedQueryable<SupportTicket> ByUrgency(IQueryable<SupportTicket> q) => q
        .OrderBy(t => t.Priority == "urgent" ? 0 : t.Priority == "high" ? 1 : t.Priority == "normal" ? 2 : 3)
        .ThenBy(t => t.Status == "new" ? 0 : t.Status == "open" ? 1 : 2)
        .ThenBy(t => t.CreatedAt);

    public record Row(int Id, string Number, string Subject, string Name, int? CustomerId, string? Account, string Topic, string Channel, string Status, string Priority,
        int? AssignedToId, string? AssignedTo, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ManagerRequestedAt, string? Rating);

    private static Task<List<Row>> Rows(IQueryable<SupportTicket> q) => q.Select(t => new Row(t.Id, "RT-" + (10000 + t.Id), t.Subject, t.Name, t.CustomerId,
        t.Customer!.Account, t.Topic, t.Channel, t.Status, t.Priority, t.AssignedToId, t.AssignedTo!.Name, t.CreatedAt, t.UpdatedAt, t.ManagerRequestedAt, t.Rating)).ToListAsync();

    /// <summary>Tickets worked by one team (by who they're assigned to).</summary>
    private IQueryable<SupportTicket> TeamTickets(string team) => db.SupportTickets.Where(t => db.RangerAgents.Any(a => a.Team == team && a.UserId == t.AssignedToId));

    /// <summary>Tickets a person handled: owned, replied to, or wrote a note on.</summary>
    private IQueryable<SupportTicket> HandledBy(IQueryable<SupportTicket> q, int userId) => q.Where(t => t.AssignedToId == userId
        || db.TicketMessages.Any(m => m.TicketId == t.Id && m.UserId == userId && (m.Kind == "agent" || m.Kind == "note"))
        || db.Notes.Any(n => n.SupportTicketId == t.Id && n.UserId == userId));

    /// <summary>
    /// Search a ticket's number, subject and messages ("text"), its notes ("notes"), and the customer and owner
    /// ("people": name, email, phone, account). With nothing picked it looks everywhere.
    /// </summary>
    private IQueryable<SupportTicket> Search(IQueryable<SupportTicket> q, string term, string[] where)
    {
        if (where.Length == 0) where = ["text", "notes", "people"];
        var like = $"%{term}%";
        var digits = new string(term.Where(char.IsDigit).ToArray());
        var number = System.Text.RegularExpressions.Regex.Match(term, @"^(?:RT-?)?(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        int[] ids = number.Success && int.TryParse(number.Groups[1].Value, out var n) ? [n, n - 10000] : [];
        bool text = where.Contains("text"), notes = where.Contains("notes"), people = where.Contains("people");
        var phone = digits.Length >= 3 ? $"%{digits}%" : null;
        return q.Where(t => ids.Contains(t.Id)
            || (text && (EF.Functions.Like(t.Subject, like) || EF.Functions.Like(t.Topic, like)
                || db.TicketMessages.Any(m => m.TicketId == t.Id && m.Kind != "note" && EF.Functions.Like(m.Body, like))))
            || (notes && (db.TicketMessages.Any(m => m.TicketId == t.Id && m.Kind == "note" && EF.Functions.Like(m.Body, like))
                || db.Notes.Any(x => x.SupportTicketId == t.Id && EF.Functions.Like(x.Body, like))))
            || (people && (EF.Functions.Like(t.Name, like) || EF.Functions.Like(t.Email, like)
                || (t.Customer != null && (EF.Functions.Like(t.Customer.Name, like) || EF.Functions.Like(t.Customer.Email!, like) || EF.Functions.Like(t.Customer.Account, like)
                    || (phone != null && EF.Functions.Like(t.Customer.Phone!.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Replace(".", ""), phone))))
                || (t.AssignedTo != null && EF.Functions.Like(t.AssignedTo.Name, like)))));
    }

    /// <summary>An internal note: an account note on the customer, linked to the ticket (or a note in the ticket's activity when there's no account).</summary>
    private void AddNote(SupportTicket t, Permissions.SignedIn me, string body, string priority, string category, string action)
    {
        if (t.CustomerId is { } customerId)
            db.Notes.Add(new Note { CustomerId = customerId, UserId = me.Id, Author = me.Name, Category = category, Action = action, Priority = priority, Body = $"{t.Number}: {body}", SupportTicketId = t.Id });
        else
            db.TicketMessages.Add(new TicketMessage { TicketId = t.Id, Kind = "note", Author = me.Name, UserId = me.Id, Body = body });
        t.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Add someone as a watcher. False if they already were.</summary>
    private async Task<bool> Watch(int ticketId, int userId)
    {
        if (await db.TicketWatchers.AnyAsync(w => w.TicketId == ticketId && w.UserId == userId)) return false;
        db.TicketWatchers.Add(new TicketWatcher { TicketId = ticketId, UserId = userId });
        return true;
    }

    /// <summary>The ticket, if I may work it: the owner, anyone who joined, a manager, or anyone picking up an unassigned ticket.</summary>
    private async Task<(SupportTicket?, Permissions.SignedIn, IActionResult?)> Workable(int id)
    {
        var me = await Me();
        var t = await db.SupportTickets.Include(x => x.AssignedTo).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return (null, me, NotFound());
        var may = t.AssignedToId == null || t.AssignedToId == me.Id || await IsManager(me) || await db.TicketWatchers.AnyAsync(w => w.TicketId == id && w.UserId == me.Id);
        return may ? (t, me, null) : (t, me, Forbidden($"This ticket belongs to {t.AssignedTo?.Name}. Ask a manager to join you in."));
    }

    /// <summary>Managers: Sheriff admins, and active team managers and company admins.</summary>
    private async Task<bool> IsManager(Permissions.SignedIn me) =>
        me.Permissions.Contains("sheriff") || await db.RangerAgents.AnyAsync(a => a.UserId == me.Id && a.Active && (a.Level == "manager" || a.Level == "company_admin"));

    /// <summary>A company admin's company: they only see and manage their own company's people. Null for everyone else.</summary>
    private async Task<string?> CompanyOf(Permissions.SignedIn me)
    {
        if (me.Permissions.Contains("sheriff")) return null;
        var company = await db.Users.Where(u => u.Id == me.Id).Select(u => u.Company).FirstOrDefaultAsync();
        if (company is null) return null;
        return await db.RangerAgents.AnyAsync(a => a.UserId == me.Id && a.Active && a.Level == "company_admin") ? company : null;
    }

    private async Task<bool> CanManage(Permissions.SignedIn me, User other) =>
        await CompanyOf(me) is { } company ? other.Company == company : await IsManager(me);

    private static object File(TicketAttachment a) => new { a.Id, a.Name, a.Mime, a.Size, a.FromCustomer, a.MessageId, a.CreatedAt, a.IsImage, a.IsPdf, a.IsText };

    /// <summary>Ranger's lists for the pages: statuses, priorities, topics, channels, teams and levels.</summary>
    private static object Lists() => new
    {
        statuses = Ranger.Statuses.ToDictionary(x => x.Key, x => new { label = x.Value.Label, tone = x.Value.Tone }),
        priorities = Ranger.Priorities.ToDictionary(x => x.Key, x => new { label = x.Value.Label, tone = x.Value.Tone }),
        topics = Ranger.Topics, channels = Ranger.Channels, teams = Ranger.Teams, levels = Ranger.Levels, via = Ranger.Via,
    };

    private string Storage => Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage"));

    private ObjectResult Forbidden(string error) => StatusCode(403, new { error });

    private async Task<Permissions.SignedIn> Me() => (await Permissions.CurrentAsync(HttpContext))!;
}
