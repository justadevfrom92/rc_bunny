using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// Corral → CRM: leads (people and businesses that might sign up) move through a pipeline; every call, email, meeting and
/// follow-up is logged on the lead; brokers and referral partners send leads and earn commission on the ones we win.
/// </summary>
[ApiController]
[Route("api/crm")]
[RequirePermission("corral")]
public class CrmController(AppDbContext db) : ControllerBase
{
    /// <summary>Stages: key → (label, chance of winning %).</summary>
    public static readonly Dictionary<string, (string Label, int Chance)> Stages = new()
    {
        ["new"] = ("New", 10), ["contacted"] = ("Contacted", 20), ["qualified"] = ("Qualified", 40),
        ["proposal"] = ("Proposal Sent", 70), ["won"] = ("Won", 100), ["lost"] = ("Lost", 0),
    };
    public static readonly string[] Open = ["new", "contacted", "qualified", "proposal"];
    public static readonly string[] Kinds = ["Residential", "Small Business", "Commercial", "Multi-site"];
    public static readonly string[] Sources = ["Website", "Referral", "Broker", "Partner", "Event", "Outbound", "Renewal"];
    public static readonly string[] LostReasons = ["Price", "Stayed with current provider", "Contract not up yet", "No response", "Credit check", "Moved"];
    public static readonly Dictionary<string, string> ActivityTypes = new() { ["call"] = "Call", ["email"] = "Email", ["meeting"] = "Meeting", ["note"] = "Note", ["task"] = "Follow-up" };
    public static readonly string[] PartnerKinds = ["Broker", "Referral Partner", "Property Manager", "Home Builder", "Realtor"];
    public static readonly string[] PartnerStatuses = ["Active", "Paused", "Ended"];

    private static decimal Weighted(CrmLead l) => Math.Round(l.EstValue * Stages[l.Stage].Chance / 100m, 2);

    /// <summary>The lists the lead and partner forms need.</summary>
    [HttpGet("options")]
    public async Task<object> Options() => new
    {
        stages = Stages.Select(s => new { key = s.Key, s.Value.Label }), kinds = Kinds, sources = Sources, lostReasons = LostReasons,
        activityTypes = ActivityTypes, partnerKinds = PartnerKinds, partnerStatuses = PartnerStatuses,
        owners = (await db.Users.Include(u => u.Role).Where(u => u.Active).OrderBy(u => u.Name).ToListAsync())
            .Where(u => u.Role!.PermissionList().Contains("corral")).Select(u => new { u.Id, u.Name }),
        partners = await db.CrmPartners.Where(p => p.Status != "Ended").OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync(),
        markets = await db.Markets.OrderBy(m => m.Name).Select(m => new { m.Id, m.Name }).ToListAsync(),
    };

    // ---- Pipeline ---------------------------------------------------------------------------------------------

    [HttpGet("pipeline")]
    public async Task<object> Pipeline(bool mine = false)
    {
        var me = await Me();
        var open = await db.CrmLeads.Include(l => l.Owner).Include(l => l.Partner).Where(l => Open.Contains(l.Stage))
            .Where(l => !mine || l.OwnerId == me.Id).OrderBy(l => l.NextFollowUpOn).ToListAsync();
        var since90 = DateTime.UtcNow.AddDays(-90);
        var closed = await db.CrmLeads.Where(l => (l.Stage == "won" || l.Stage == "lost") && l.ClosedAt >= since90).ToListAsync();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var wonMonth = closed.Where(l => l.Stage == "won" && l.ClosedAt >= monthStart).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new
        {
            mine,
            stages = Open.Select(s => new
            {
                key = s, label = Stages[s].Label,
                leads = open.Where(l => l.Stage == s).Select(l => new { l.Id, l.Name, l.Company, l.Kind, l.EstValue, l.AnnualKwh, l.NextFollowUpOn, owner = l.Owner?.Name, partner = l.Partner?.Name, overdue = l.NextFollowUpOn < today }),
            }),
            stats = new
            {
                open = open.Count, value = open.Sum(l => l.EstValue), weighted = open.Sum(Weighted), wonMonth = wonMonth.Count, wonValue = wonMonth.Sum(l => l.EstValue),
                winRate = closed.Count == 0 ? 0 : (int)Math.Round(100.0 * closed.Count(l => l.Stage == "won") / closed.Count),
                overdue = open.Count(l => l.NextFollowUpOn < today),
            },
            bySource = (await db.CrmLeads.Where(l => l.CreatedAt >= since90).ToListAsync()).GroupBy(l => l.Source)
                .Select(g => new { source = g.Key, count = g.Count(), won = g.Count(l => l.Stage == "won") }).OrderByDescending(x => x.count),
            recentWins = await db.CrmLeads.Include(l => l.Owner).Where(l => l.Stage == "won").OrderByDescending(l => l.ClosedAt).Take(6)
                .Select(l => new { l.Id, l.Name, l.Company, l.EstValue, l.ClosedAt, owner = l.Owner!.Name }).ToListAsync(),
        };
    }

    // ---- Leads ------------------------------------------------------------------------------------------------

    [HttpGet("leads")]
    public async Task<object> Leads(string stage = "open", string? kind = null, string? source = null, bool mine = false, string? q = null, int page = 1)
    {
        var me = await Me();
        var query = db.CrmLeads.AsQueryable();
        query = stage == "open" ? query.Where(l => Open.Contains(l.Stage)) : stage == "all" ? query : query.Where(l => l.Stage == stage);
        if (!string.IsNullOrEmpty(kind)) query = query.Where(l => l.Kind == kind);
        if (!string.IsNullOrEmpty(source)) query = query.Where(l => l.Source == source);
        if (mine) query = query.Where(l => l.OwnerId == me.Id);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(l => EF.Functions.Like(l.Name, $"%{q}%") || EF.Functions.Like(l.Company!, $"%{q}%") || EF.Functions.Like(l.Email!, $"%{q}%") || EF.Functions.Like(l.Phone!, $"%{q}%"));
        var total = await query.CountAsync();
        return new
        {
            total, page,
            counts = await db.CrmLeads.GroupBy(l => l.Stage).Select(g => new { stage = g.Key, n = g.Count() }).ToListAsync(),
            leads = await query.OrderBy(l => l.Stage == "won" || l.Stage == "lost" ? 1 : 0).ThenBy(l => l.NextFollowUpOn).ThenByDescending(l => l.Id)
                .Skip((Math.Max(page, 1) - 1) * 30).Take(30)
                .Select(l => new { l.Id, l.Name, l.Company, l.Kind, l.Source, l.Stage, l.EstValue, l.AnnualKwh, l.NextFollowUpOn, owner = l.Owner!.Name, partner = l.Partner!.Name, l.CreatedAt })
                .ToListAsync(),
        };
    }

    [HttpGet("leads/{id:int}")]
    public async Task<IActionResult> Lead(int id)
    {
        var lead = await db.CrmLeads.Include(l => l.Owner).Include(l => l.Partner).Include(l => l.Market).Include(l => l.Customer).FirstOrDefaultAsync(l => l.Id == id);
        if (lead is null) return NotFound();
        var activities = await db.CrmActivities.Include(a => a.User).Where(a => a.LeadId == id).OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new { a.Id, a.Type, a.Subject, a.Body, a.DueOn, a.DoneAt, a.CreatedAt, user = a.User!.Name }).ToListAsync();
        return Ok(new
        {
            lead = new
            {
                lead.Id, lead.Name, lead.Company, lead.Title, lead.Kind, lead.Email, lead.Phone, lead.City, lead.MarketId, market = lead.Market?.Name, lead.Source,
                lead.PartnerId, partner = lead.Partner?.Name, lead.AnnualKwh, lead.EstValue, lead.Sites, lead.CurrentProvider, lead.ContractEndsOn, lead.Stage,
                stageLabel = Stages[lead.Stage].Label, lead.StageChangedAt, lead.LostReason, lead.OwnerId, owner = lead.Owner?.Name, lead.NextFollowUpOn, lead.Notes,
                lead.CreatedAt, lead.ClosedAt, weighted = Weighted(lead), commission = lead.Partner?.CommissionFor(lead),
                customer = lead.Customer == null ? null : new { lead.Customer.Id, lead.Customer.Account, lead.Customer.Name },
            },
            activities,
            openTasks = activities.Where(a => a.DueOn != null && a.DoneAt == null).OrderBy(a => a.DueOn),
            similar = string.IsNullOrEmpty(lead.Company) ? [] : await db.Customers.Where(c => EF.Functions.Like(c.Name, $"%{lead.Company}%")).Take(3).Select(c => new { c.Id, c.Account, c.Name, c.Status }).ToListAsync(),
        });
    }

    public record LeadInput(string Name, string? Company, string? Title, string Kind, string? Email, string? Phone, string? City, int? MarketId,
        string Source, int? PartnerId, int? AnnualKwh, decimal? EstValue, int? Sites, string? CurrentProvider, DateOnly? ContractEndsOn,
        int? OwnerId, DateOnly? NextFollowUpOn, string? Notes);

    [HttpPost("leads")]
    public Task<IActionResult> AddLead(LeadInput input) => SaveLead(null, input);

    [HttpPut("leads/{id:int}")]
    public Task<IActionResult> UpdateLead(int id, LeadInput input) => SaveLead(id, input);

    private async Task<IActionResult> SaveLead(int? id, LeadInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Enter the contact's name." });
        if (!Kinds.Contains(input.Kind) || !Sources.Contains(input.Source)) return BadRequest(new { error = "Pick the kind and source." });
        if (input.Kind != "Residential" && string.IsNullOrWhiteSpace(input.Company)) return BadRequest(new { error = "Enter the business name." });
        var partnered = input.Source is "Broker" or "Partner";
        if (partnered && input.PartnerId is null) return BadRequest(new { error = "Pick the broker or partner who sent this lead." });
        var lead = id is null ? new CrmLead() : await db.CrmLeads.FindAsync(id.Value);
        if (lead is null) return NotFound();
        lead.Name = input.Name.Trim(); lead.Company = input.Company?.Trim(); lead.Title = input.Title?.Trim(); lead.Kind = input.Kind;
        lead.Email = input.Email?.Trim(); lead.Phone = input.Phone?.Trim(); lead.City = input.City?.Trim(); lead.MarketId = input.MarketId;
        lead.Source = input.Source; lead.PartnerId = partnered ? input.PartnerId : null; lead.AnnualKwh = Math.Max(0, input.AnnualKwh ?? 0);
        // No yearly value given: estimate it from usage at about 14¢/kWh
        lead.EstValue = input.EstValue is > 0 ? input.EstValue.Value : Math.Round(lead.AnnualKwh * 0.14m, 2);
        lead.Sites = Math.Max(1, input.Sites ?? 1); lead.CurrentProvider = input.CurrentProvider?.Trim(); lead.ContractEndsOn = input.ContractEndsOn;
        lead.OwnerId = input.OwnerId; lead.NextFollowUpOn = input.NextFollowUpOn; lead.Notes = input.Notes?.Trim();
        if (id is null)
        {
            db.CrmLeads.Add(lead);
            await db.SaveChangesAsync();
            db.CrmActivities.Add(new CrmActivity { LeadId = lead.Id, UserId = (await Me()).Id, Type = "note", Subject = $"Lead added from {lead.Source}" });
        }
        await db.SaveChangesAsync();
        return Ok(new { lead.Id });
    }

    public record StageInput(string Stage, string? LostReason, string? Account);

    /// <summary>Move a lead to another stage. Lost needs a reason; Won can link the new account.</summary>
    [HttpPost("leads/{id:int}/stage")]
    public async Task<IActionResult> MoveStage(int id, StageInput input)
    {
        var lead = await db.CrmLeads.FindAsync(id);
        if (lead is null) return NotFound();
        if (!Stages.ContainsKey(input.Stage)) return BadRequest(new { error = "Pick a stage." });
        if (input.Stage == "lost" && string.IsNullOrWhiteSpace(input.LostReason)) return BadRequest(new { error = "Say why it was lost." });
        int? customerId = lead.CustomerId;
        if (input.Stage == "won" && !string.IsNullOrWhiteSpace(input.Account))
        {
            customerId = (await db.Customers.FirstOrDefaultAsync(c => c.Account == input.Account.Trim()))?.Id;
            if (customerId is null) return BadRequest(new { error = "No account with that number." });
        }
        if (input.Stage == lead.Stage) return Ok();
        var from = Stages[lead.Stage].Label;
        var closing = input.Stage is "won" or "lost";
        lead.Stage = input.Stage; lead.StageChangedAt = DateTime.UtcNow; lead.ClosedAt = closing ? DateTime.UtcNow : null;
        lead.LostReason = input.Stage == "lost" ? input.LostReason!.Trim() : null;
        lead.CustomerId = input.Stage == "won" ? customerId : null;
        if (closing)
        {
            lead.NextFollowUpOn = null;
            foreach (var t in await db.CrmActivities.Where(a => a.LeadId == id && a.DueOn != null && a.DoneAt == null).ToListAsync()) t.DoneAt = DateTime.UtcNow;
        }
        db.CrmActivities.Add(new CrmActivity { LeadId = id, UserId = (await Me()).Id, Type = "stage", Subject = $"{from} → {Stages[input.Stage].Label}", Body = lead.LostReason });
        await db.SaveChangesAsync();
        return Ok();
    }

    public record ActivityInput(string Type, string Subject, string? Body, DateOnly? DueOn);

    /// <summary>Log a call, email, meeting or note on a lead, or set a follow-up.</summary>
    [HttpPost("leads/{id:int}/activities")]
    public async Task<IActionResult> LogActivity(int id, ActivityInput input)
    {
        var lead = await db.CrmLeads.FindAsync(id);
        if (lead is null) return NotFound();
        var problem = CheckActivity(input);
        if (problem != null) return BadRequest(new { error = problem });
        var me = await Me();
        db.CrmActivities.Add(new CrmActivity { LeadId = id, UserId = me.Id, Type = input.Type, Subject = input.Subject.Trim(), Body = input.Body, DueOn = input.DueOn, DoneAt = input.Type == "task" ? null : DateTime.UtcNow });
        await db.SaveChangesAsync();
        if (input.Type == "task") await RefreshNextFollowUp(lead);
        else if (lead.Stage == "new" && input.Type is "call" or "email" or "meeting")
        {
            (lead.Stage, lead.StageChangedAt) = ("contacted", DateTime.UtcNow);
            db.CrmActivities.Add(new CrmActivity { LeadId = id, UserId = me.Id, Type = "stage", Subject = "New → Contacted" });
        }
        await db.SaveChangesAsync();
        return Ok();
    }

    private static string? CheckActivity(ActivityInput input) =>
        !ActivityTypes.ContainsKey(input.Type) ? "Pick what you're logging."
        : string.IsNullOrWhiteSpace(input.Subject) ? "Add a subject."
        : input.Type == "task" && input.DueOn is null ? "Pick when to follow up." : null;

    private async Task RefreshNextFollowUp(CrmLead lead) =>
        lead.NextFollowUpOn = await db.CrmActivities.Where(a => a.LeadId == lead.Id && a.DueOn != null && a.DoneAt == null).MinAsync(a => a.DueOn);

    // ---- Follow-ups -------------------------------------------------------------------------------------------

    /// <summary>Your follow-ups (or everyone's, when asked or when you have none): overdue, today, upcoming, and done in the last 14 days.</summary>
    [HttpGet("follow-ups")]
    public async Task<object> FollowUps(string? who)
    {
        var me = await Me();
        IQueryable<CrmActivity> Mine(IQueryable<CrmActivity> q) => q.Where(a => a.UserId == me.Id || (a.Lead != null && a.Lead.OwnerId == me.Id));
        var tasks = db.CrmActivities.Where(a => a.DueOn != null);
        var everyone = who == "all" || (who == null && !await Mine(tasks.Where(a => a.DoneAt == null)).AnyAsync());
        var scope = everyone ? tasks : Mine(tasks);
        var open = await scope.Where(a => a.DoneAt == null).OrderBy(a => a.DueOn).Select(Row).ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var since = DateTime.UtcNow.AddDays(-14);
        return new
        {
            everyone,
            overdue = open.Where(t => t.DueOn < today), today = open.Where(t => t.DueOn == today), upcoming = open.Where(t => t.DueOn > today),
            done = await scope.Where(a => a.DoneAt >= since).OrderByDescending(a => a.DoneAt).Take(20).Select(Row).ToListAsync(),
        };
    }

    private record TaskRow(int Id, string Subject, string? Body, DateOnly? DueOn, DateTime? DoneAt, int? LeadId, string? Lead, int? PartnerId, string? Partner, string? User);

    private static readonly System.Linq.Expressions.Expression<Func<CrmActivity, TaskRow>> Row = a =>
        new TaskRow(a.Id, a.Subject, a.Body, a.DueOn, a.DoneAt, a.LeadId, a.Lead!.Company ?? a.Lead.Name, a.PartnerId, a.Partner!.Name, a.User!.Name);

    /// <summary>Mark a follow-up done, or reopen it.</summary>
    [HttpPost("follow-ups/{id:int}")]
    public async Task<IActionResult> ToggleTask(int id)
    {
        var task = await db.CrmActivities.Include(a => a.Lead).FirstOrDefaultAsync(a => a.Id == id && a.DueOn != null);
        if (task is null) return NotFound();
        task.DoneAt = task.DoneAt is null ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();
        if (task.Lead is { } lead) { await RefreshNextFollowUp(lead); await db.SaveChangesAsync(); }
        return Ok(new { done = task.DoneAt != null });
    }

    // ---- Partners & brokers -----------------------------------------------------------------------------------

    [HttpGet("partners")]
    public async Task<object> Partners()
    {
        var partners = await db.CrmPartners.Include(p => p.Owner).ToListAsync();
        var leads = await db.CrmLeads.Where(l => l.PartnerId != null).ToListAsync();
        return partners.OrderBy(p => Array.IndexOf(PartnerStatuses, p.Status)).ThenBy(p => p.Name).Select(p => new
        {
            p.Id, p.Name, p.Kind, p.ContactName, p.Email, p.Phone, p.City, p.Status, p.CommissionBasis, p.CommissionRate, owner = p.Owner?.Name, p.AgreementEndsOn,
            leads = leads.Count(l => l.PartnerId == p.Id), won = leads.Count(l => l.PartnerId == p.Id && l.Stage == "won"),
            open = leads.Count(l => l.PartnerId == p.Id && l.IsOpen),
            earned = leads.Where(l => l.PartnerId == p.Id && l.Stage == "won").Sum(p.CommissionFor),
        });
    }

    [HttpGet("partners/{id:int}")]
    public async Task<IActionResult> Partner(int id)
    {
        var p = await db.CrmPartners.Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        var leads = await db.CrmLeads.Include(l => l.Owner).Where(l => l.PartnerId == id).OrderByDescending(l => l.CreatedAt).ToListAsync();
        var won = leads.Where(l => l.Stage == "won").ToList();
        return Ok(new
        {
            partner = new { p.Id, p.Name, p.Kind, p.ContactName, p.Email, p.Phone, p.City, p.CommissionBasis, p.CommissionRate, p.Status, p.OwnerId, owner = p.Owner?.Name, p.AgreementEndsOn, p.Notes, p.CreatedAt },
            leads = leads.Select(l => new { l.Id, l.Name, l.Company, l.Stage, l.EstValue, l.AnnualKwh, owner = l.Owner?.Name, l.CreatedAt, commission = l.Stage == "won" ? p.CommissionFor(l) : (decimal?)null }),
            won = won.Count, earned = won.Sum(p.CommissionFor), pipeline = leads.Where(l => l.IsOpen).Sum(l => l.EstValue),
            activities = await db.CrmActivities.Include(a => a.User).Where(a => a.PartnerId == id).OrderByDescending(a => a.CreatedAt)
                .Select(a => new { a.Id, a.Type, a.Subject, a.Body, a.DueOn, a.DoneAt, a.CreatedAt, user = a.User!.Name }).ToListAsync(),
        });
    }

    public record PartnerInput(string Name, string Kind, string? ContactName, string? Email, string? Phone, string? City, string CommissionBasis,
        decimal CommissionRate, string Status, int? OwnerId, DateOnly? AgreementEndsOn, string? Notes);

    [HttpPost("partners")]
    public Task<IActionResult> AddPartner(PartnerInput input) => SavePartner(null, input);

    [HttpPut("partners/{id:int}")]
    public Task<IActionResult> UpdatePartner(int id, PartnerInput input) => SavePartner(id, input);

    private async Task<IActionResult> SavePartner(int? id, PartnerInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Enter the name." });
        if (!PartnerKinds.Contains(input.Kind) || !PartnerStatuses.Contains(input.Status) || input.CommissionBasis is not ("kwh" or "flat"))
            return BadRequest(new { error = "Pick the kind, status and commission basis." });
        if (input.CommissionRate is < 0 or > 1) return BadRequest(new { error = "The commission rate is between 0 and 1." });
        var p = id is null ? new CrmPartner() : await db.CrmPartners.FindAsync(id.Value);
        if (p is null) return NotFound();
        (p.Name, p.Kind, p.ContactName, p.Email, p.Phone, p.City) = (input.Name.Trim(), input.Kind, input.ContactName?.Trim(), input.Email?.Trim(), input.Phone?.Trim(), input.City?.Trim());
        (p.CommissionBasis, p.CommissionRate, p.Status, p.OwnerId, p.AgreementEndsOn, p.Notes) = (input.CommissionBasis, input.CommissionRate, input.Status, input.OwnerId, input.AgreementEndsOn, input.Notes?.Trim());
        if (id is null) db.CrmPartners.Add(p);
        await db.SaveChangesAsync();
        return Ok(new { p.Id });
    }

    [HttpPost("partners/{id:int}/activities")]
    public async Task<IActionResult> LogPartnerActivity(int id, ActivityInput input)
    {
        if (!await db.CrmPartners.AnyAsync(p => p.Id == id)) return NotFound();
        var problem = CheckActivity(input);
        if (problem != null) return BadRequest(new { error = problem });
        db.CrmActivities.Add(new CrmActivity { PartnerId = id, UserId = (await Me()).Id, Type = input.Type, Subject = input.Subject.Trim(), Body = input.Body, DueOn = input.DueOn, DoneAt = input.Type == "task" ? null : DateTime.UtcNow });
        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<Permissions.SignedIn> Me() => (await Permissions.CurrentAsync(HttpContext))!;
}
