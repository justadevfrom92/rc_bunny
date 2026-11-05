using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Services;

namespace RcBunny.Controllers;

/// <summary>Corral's other screens: ESIID Lookup, SMS, Phone Calls, New Contact, ERCOT and the exception queues.</summary>
[ApiController]
[Route("api/corral")]
[RequirePermission("corral")]
public class CorralController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    // ---- ESIID Lookup -----------------------------------------------------------------------------------------

    /// <summary>Accounts at an address or ESIID (optionally in one zip), with what's been sent to ERCOT for each meter.</summary>
    [HttpGet("esiid")]
    public async Task<object> Esiid(string? q, string? zip)
    {
        if (string.IsNullOrWhiteSpace(q)) return new { results = Array.Empty<object>() };
        var like = $"%{q.Trim()}%";
        var results = await db.Customers.Include(c => c.Market)
            .Where(c => EF.Functions.Like(c.Address, like) || EF.Functions.Like(c.Esiid!, q.Trim() + "%"))
            .Where(c => string.IsNullOrEmpty(zip) || c.Zip == zip).OrderBy(c => c.Address).Take(100)
            .Select(c => new { c.Id, c.Account, c.Name, c.Address, c.Unit, c.City, c.Zip, c.Esiid, c.Status, c.Type, market = c.Market!.Code, c.MeterType, c.LoadZone })
            .ToListAsync();
        var esiids = results.Select(r => r.Esiid).Where(e => e != null).ToList();
        return new
        {
            results,
            transactions = await db.ErcotTransactions.Where(t => esiids.Contains(t.Esiid)).OrderByDescending(t => t.TransDate).Take(200).ToListAsync(),
        };
    }

    // ---- SMS --------------------------------------------------------------------------------------------------

    private bool SmsReady => config.GetSection("Integrations:Twilio").GetChildren().Any(s => !string.IsNullOrEmpty(s.Value));

    /// <summary>Texts in and out. "waiting" = customers whose latest text came from them (they're waiting on a reply).</summary>
    [HttpGet("sms")]
    public async Task<object> Sms(string? q, string? account, string? direction, string? status, DateOnly? start, DateOnly? end, bool waiting = false, int page = 1)
    {
        var latest = db.ContactLogs.Where(l => l.Channel == "SMS" && l.CustomerId != null).GroupBy(l => l.CustomerId).Select(g => g.Max(l => l.Id));
        var waitingIds = await db.ContactLogs.Where(l => latest.Contains(l.Id) && l.Direction == "in").Select(l => l.CustomerId).ToListAsync();
        var query = db.ContactLogs.Where(l => l.Channel == "SMS");
        if (!string.IsNullOrWhiteSpace(q))
        {
            var digits = Digits(q);
            query = query.Where(l => EF.Functions.Like(l.Body!, $"%{q}%") || EF.Functions.Like(l.Customer!.Name, $"%{q}%")
                || (digits != "" && EF.Functions.Like(l.Phone!.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", ""), $"%{digits}%")));
        }
        if (!string.IsNullOrWhiteSpace(account)) query = query.Where(l => l.Customer!.Account == account.Trim());
        if (direction is "in" or "out") query = query.Where(l => l.Direction == direction);
        if (!string.IsNullOrEmpty(status)) query = query.Where(l => l.Status == status);
        if (start is { } s) query = query.Where(l => l.CreatedAt >= s.ToDateTime(TimeOnly.MinValue));
        if (end is { } e) query = query.Where(l => l.CreatedAt <= e.ToDateTime(TimeOnly.MaxValue));
        if (waiting) query = query.Where(l => waitingIds.Contains(l.CustomerId));
        var total = await query.CountAsync();
        return new
        {
            total, page, ready = SmsReady, waitingIds,
            statuses = await db.ContactLogs.Where(l => l.Channel == "SMS").Select(l => l.Status).Distinct().ToListAsync(),
            rows = await query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).Skip((Math.Max(page, 1) - 1) * 50).Take(50)
                .Select(l => new { l.Id, l.CustomerId, account = l.Customer!.Account, customer = l.Customer.Name, customerPhone = l.Customer.Phone, l.Phone, l.Body, l.Direction, l.Status, l.Resolution, l.Who, l.CreatedAt })
                .ToListAsync(),
        };
    }

    /// <summary>One text, with the whole conversation with that customer.</summary>
    [HttpGet("sms/{id:int}")]
    public async Task<IActionResult> SmsMessage(int id)
    {
        var m = await db.ContactLogs.Include(l => l.Customer).FirstOrDefaultAsync(l => l.Id == id && l.Channel == "SMS");
        if (m is null) return NotFound();
        return Ok(new
        {
            message = new { m.Id, m.Body, m.Direction, m.Status, m.Resolution, m.ResolvedBy, m.Who, m.Phone, m.Template, m.SentAt, m.CreatedAt, m.CustomerId },
            customer = m.Customer == null ? null : new { m.Customer.Id, m.Customer.Account, m.Customer.Name, m.Customer.FirstName, m.Customer.Phone },
            thread = await db.ContactLogs.Where(l => l.Channel == "SMS" && l.CustomerId == m.CustomerId).OrderBy(l => l.CreatedAt).ThenBy(l => l.Id).ToListAsync(),
            resolutions = Corral.Resolutions.Select(r => r.Resolution),
        });
    }

    /// <summary>Send a Text: who it's to and their last few texts.</summary>
    [HttpGet("sms/new")]
    public async Task<object> SmsForm(string? account)
    {
        var c = string.IsNullOrEmpty(account) ? null : await db.Customers.FirstOrDefaultAsync(x => x.Account == account);
        return new
        {
            ready = SmsReady,
            customer = c == null ? null : new { c.Id, c.Account, c.Name, c.Phone },
            recent = c == null ? [] : (await db.ContactLogs.Where(l => l.Channel == "SMS" && l.CustomerId == c.Id).OrderByDescending(l => l.CreatedAt).Take(5).ToListAsync()).AsEnumerable().Reverse().ToList(),
        };
    }

    public record SendText(string Account, string Message);

    [HttpPost("sms")]
    public async Task<IActionResult> Send(SendText input)
    {
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Account == input.Account.Trim());
        if (c is null) return BadRequest(new { error = "No account with that number." });
        if (string.IsNullOrWhiteSpace(c.Phone)) return BadRequest(new { error = "This account has no phone number." });
        if (string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 480) return BadRequest(new { error = "Write a message (up to 480 characters)." });
        var me = await Me();
        var result = await new AccountActions(db, c, me.Name, config).RunAsync(Corral.Actions.First(a => a.Key == "send-text"), new() { ["message"] = input.Message.Trim() });
        return Ok(new { message = result.Message });
    }

    // ---- Phone calls and New Contact --------------------------------------------------------------------------

    [HttpGet("calls")]
    public async Task<object> Calls(DateOnly? start, DateOnly? end, string? account, string? phone, string? agent, int? user, string? direction, string? issue, int page = 1)
    {
        var query = db.PhoneCalls.AsQueryable();
        if (start is { } s) query = query.Where(c => c.StartedAt >= s.ToDateTime(TimeOnly.MinValue));
        if (end is { } e) query = query.Where(c => c.StartedAt <= e.ToDateTime(TimeOnly.MaxValue));
        if (!string.IsNullOrWhiteSpace(account)) query = query.Where(c => c.Customer!.Account == account.Trim());
        var digits = Digits(phone);
        if (digits != "") query = query.Where(c => EF.Functions.Like(c.Phone.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", ""), $"%{digits}%"));
        if (!string.IsNullOrWhiteSpace(agent)) query = query.Where(c => c.AgentId == agent.Trim());
        if (user is { } u) query = query.Where(c => c.UserId == u);
        if (direction is "inbound" or "outbound") query = query.Where(c => c.Direction == direction);
        if (!string.IsNullOrEmpty(issue)) query = query.Where(c => c.Issue == issue);
        var total = await query.CountAsync();
        return new
        {
            total, page,
            rows = await query.OrderByDescending(c => c.StartedAt).Skip((Math.Max(page, 1) - 1) * 25).Take(25)
                .Select(c => new { c.Id, c.StartedAt, c.Direction, c.Phone, c.Issue, c.DurationSec, c.CustomerId, account = c.Customer!.Account, customer = c.Customer.Name, answeredBy = c.User!.Name, c.AgentId, c.Resolution })
                .ToListAsync(),
            issues = await db.PhoneCalls.Where(c => c.Issue != null).Select(c => c.Issue).Distinct().OrderBy(i => i).ToListAsync(),
            users = await db.Users.Where(x => db.PhoneCalls.Any(c => c.UserId == x.Id)).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(),
        };
    }

    [HttpGet("calls/{id:int}")]
    public async Task<IActionResult> Transcript(int id)
    {
        var call = await db.PhoneCalls.Include(c => c.Customer).Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);
        if (call is null) return NotFound();
        return Ok(new
        {
            call = new { call.Id, call.StartedAt, call.Direction, call.Phone, call.Issue, call.DurationSec, call.AgentId, call.Transcript, call.Resolution, call.ResolvedBy, answeredBy = call.User?.Name },
            customer = call.Customer == null ? null : new { call.Customer.Id, call.Customer.Account, call.Customer.Name },
            resolutions = Corral.Resolutions.Select(r => r.Resolution),
        });
    }

    /// <summary>New Contact: log a call, email, text or chat that happened, and how it ended.</summary>
    [HttpGet("contacts/new")]
    public async Task<object> ContactForm(string? account)
    {
        var c = string.IsNullOrEmpty(account) ? null : await db.Customers.FirstOrDefaultAsync(x => x.Account == account);
        return new
        {
            customer = c == null ? null : new { c.Id, c.Account, c.Name, c.Phone, c.Email },
            channels = Corral.ContactChannels, resolutions = Corral.Resolutions.Select(r => r.Resolution),
            issues = await db.PhoneCalls.Where(x => x.Issue != null).Select(x => x.Issue).Distinct().OrderBy(i => i).ToListAsync(),
        };
    }

    public record NewContact(string Account, string Channel, string Direction, string? Issue, string Resolution, int? Minutes, string Body);

    [HttpPost("contacts")]
    public async Task<IActionResult> LogContact(NewContact input)
    {
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Account == input.Account.Trim());
        if (c is null) return BadRequest(new { error = "No account with that number." });
        if (!Corral.ContactChannels.ContainsKey(input.Channel) || input.Direction is not ("in" or "out")) return BadRequest(new { error = "Pick the kind of contact and its direction." });
        if (!Corral.Resolutions.Any(r => r.Resolution == input.Resolution)) return BadRequest(new { error = "Pick how it ended." });
        if (string.IsNullOrWhiteSpace(input.Body)) return BadRequest(new { error = "Say what happened." });
        var me = await Me();
        var minutes = Math.Clamp(input.Minutes ?? 0, 0, 600);
        string goTo;
        if (input.Channel is "call" or "chat")
        {
            var call = new PhoneCall
            {
                CustomerId = c.Id, UserId = me.Id, Phone = c.Phone ?? "", Direction = input.Direction == "in" ? "inbound" : "outbound",
                StartedAt = DateTime.UtcNow.AddMinutes(-minutes), DurationSec = minutes * 60, Issue = input.Channel == "chat" ? $"Chat: {input.Issue}" : input.Issue,
                Transcript = input.Body.Trim(), Resolution = input.Resolution, ResolvedBy = me.Name,
            };
            db.PhoneCalls.Add(call);
            await db.SaveChangesAsync();
            goTo = $"/corral/calls/{call.Id}";
        }
        else
        {
            var log = new ContactLog
            {
                CustomerId = c.Id, Channel = input.Channel == "email" ? "Email" : "SMS", Template = input.Issue is { Length: > 0 } i ? i : "Logged by hand",
                Body = input.Body.Trim(), Status = "logged", Direction = input.Direction, Phone = input.Channel == "sms" ? c.Phone : null,
                SentAt = DateTime.UtcNow, Who = me.Name, Resolution = input.Resolution, ResolvedBy = me.Name,
            };
            db.ContactLogs.Add(log);
            await db.SaveChangesAsync();
            goTo = input.Channel == "sms" ? $"/corral/sms/{log.Id}" : $"/corral/customers/{c.Id}/contact-log";
        }
        var label = Corral.ContactChannels[input.Channel];
        db.Notes.Add(new Note
        {
            CustomerId = c.Id, Author = me.Name, Category = "Contact", Action = label, Priority = "Low",
            Body = $"{label} ({(input.Direction == "in" ? "from" : "to")} the customer, {input.Resolution}){(string.IsNullOrEmpty(input.Issue) ? "" : " — " + input.Issue)}: {input.Body.Trim()}",
        });
        await db.SaveChangesAsync();
        return Ok(new { message = $"{label} logged as {input.Resolution}.", goTo });
    }

    public record ResolutionInput(string Resolution);

    /// <summary>Resolved, Incomplete… for a call ("call"), an email or text ("contact") or a website message ("message").</summary>
    [HttpPost("resolution/{type}/{id:int}")]
    public async Task<IActionResult> Resolution(string type, int id, ResolutionInput input)
    {
        if (!Corral.Resolutions.Any(r => r.Resolution == input.Resolution)) return BadRequest(new { error = "Pick a resolution." });
        var who = (await Me()).Name;
        switch (type)
        {
            case "call" when await db.PhoneCalls.FindAsync(id) is { } call: (call.Resolution, call.ResolvedBy) = (input.Resolution, who); break;
            case "contact" when await db.ContactLogs.FindAsync(id) is { } log: (log.Resolution, log.ResolvedBy) = (input.Resolution, who); break;
            case "message" when await db.ContactMessages.FindAsync(id) is { } msg:
                var me = await Me();
                (msg.Resolution, msg.ResolvedById, msg.ResolvedAt) = (input.Resolution, me.Id, DateTime.UtcNow);
                (msg.HandledAt, msg.HandledById) = input.Resolution == "Resolved" ? ((DateTime?)DateTime.UtcNow, (int?)me.Id) : (null, null);
                break;
            default: return NotFound();
        }
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- ERCOT ------------------------------------------------------------------------------------------------

    [HttpGet("ercot")]
    public async Task<object> Ercot(string? esiid, string? account, string? tracking, string? type, string? purpose, string? status, DateOnly? start, DateOnly? end, int page = 1)
    {
        var query = db.ErcotTransactions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(esiid)) query = query.Where(t => EF.Functions.Like(t.Esiid, $"%{esiid.Trim()}%"));
        if (!string.IsNullOrWhiteSpace(account)) query = query.Where(t => t.Customer!.Account == account.Trim());
        if (!string.IsNullOrWhiteSpace(tracking)) query = query.Where(t => EF.Functions.Like(t.Tracking!, $"%{tracking.Trim()}%"));
        if (!string.IsNullOrEmpty(type)) query = query.Where(t => t.TransType == type);
        if (purpose is "request" or "response") query = query.Where(t => t.Purpose == purpose);
        if (status is "linked" or "unlinked" or "cancelled") query = query.Where(t => t.Status == status);
        if (start is { } s) query = query.Where(t => t.TransDate >= s);
        if (end is { } e) query = query.Where(t => t.TransDate <= e);
        var total = await query.CountAsync();
        return new
        {
            total, page,
            types = await db.ErcotTransactions.Select(t => t.TransType).Distinct().OrderBy(t => t).ToListAsync(),
            counts = await db.ErcotTransactions.GroupBy(t => t.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(),
            rows = await query.OrderByDescending(t => t.TransDate).ThenByDescending(t => t.Id).Skip((Math.Max(page, 1) - 1) * 50).Take(50)
                .Select(t => new { t.Id, t.TransDate, t.Esiid, t.Purpose, t.ScheduledOn, t.TransType, t.Label, t.Tracking, t.Status, t.CustomerId, account = t.Customer!.Account, customer = t.Customer.Name })
                .ToListAsync(),
        };
    }

    // ---- Exception queues -------------------------------------------------------------------------------------

    [HttpGet("queues")]
    public async Task<object> Queues()
    {
        var counts = await db.WorkItems.Where(w => w.ResolvedAt == null).GroupBy(w => w.Queue).Select(g => new { queue = g.Key, count = g.Count() }).ToListAsync();
        return new
        {
            queues = Corral.WorkQueues.Select(q => new { key = q.Key, q.Value.Name, q.Value.About, count = counts.FirstOrDefault(c => c.queue == q.Key)?.count ?? 0 }),
            history = await Resolved(db.WorkItems),
        };
    }

    [HttpGet("queues/{key}")]
    public async Task<IActionResult> Queue(string key)
    {
        if (!Corral.WorkQueues.TryGetValue(key, out var queue)) return NotFound();
        return Ok(new
        {
            key, queue.Name, queue.About,
            items = await db.WorkItems.Where(w => w.Queue == key && w.ResolvedAt == null).OrderBy(w => w.CreatedAt)
                .Select(w => new { w.Id, w.Summary, w.CreatedAt, w.CustomerId, account = w.Customer!.Account, customer = w.Customer.Name, status = w.Customer.Status, plan = w.Customer.Plan!.Name })
                .ToListAsync(),
            history = await Resolved(db.WorkItems.Where(w => w.Queue == key)),
        });
    }

    private static Task<List<object>> Resolved(IQueryable<WorkItem> q) => q.Where(w => w.ResolvedAt != null).OrderByDescending(w => w.ResolvedAt).Take(25)
        .Select(w => (object)new { w.Id, w.Queue, w.Summary, w.CreatedAt, w.ResolvedAt, w.ResolvedBy, w.CustomerId, account = w.Customer!.Account, customer = w.Customer.Name })
        .ToListAsync();

    [HttpPost("work-items/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id)
    {
        var item = await db.WorkItems.FindAsync(id);
        if (item is null) return NotFound();
        (item.ResolvedAt, item.ResolvedBy) = (DateTime.UtcNow, (await Me()).Name);
        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<Permissions.SignedIn> Me() => (await Permissions.CurrentAsync(HttpContext))!;
}
