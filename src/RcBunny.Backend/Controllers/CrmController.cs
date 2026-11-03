using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Corral → CRM: the sales pipeline, leads, follow-ups, and partners & brokers.</summary>
[ApiController]
[Route("api/crm")]
[RequirePermission("corral")]
public class CrmController(AppDbContext db) : ControllerBase
{
    public static readonly string[] Stages = ["new", "contacted", "qualified", "proposal", "won", "lost"];
    public static readonly string[] Kinds = ["Residential", "Small Business", "Commercial", "Multi-site"];
    public static readonly string[] Sources = ["Website", "Referral", "Broker", "Partner", "Event", "Outbound", "Renewal"];

    /// <summary>Open leads by stage, plus how the last 90 days went.</summary>
    [HttpGet("pipeline")]
    public async Task<object> Pipeline()
    {
        var leads = await db.Leads.Include(l => l.Owner).ToListAsync();
        var closed = leads.Where(l => l.ClosedAt > DateTime.UtcNow.AddDays(-90)).ToList();
        return new
        {
            stages = Stages[..4].Select(s => new
            {
                stage = s,
                leads = leads.Where(l => l.Stage == s).OrderBy(l => l.CreatedAt).Select(l => new { l.Id, l.Name, l.Company, l.Kind, l.MonthlyKwh, owner = l.Owner?.Name }),
            }),
            won = closed.Count(l => l.Stage == "won"),
            lost = closed.Count(l => l.Stage == "lost"),
            winRate = closed.Count == 0 ? 0 : 100 * closed.Count(l => l.Stage == "won") / closed.Count,
        };
    }

    [HttpGet("leads")]
    public async Task<object> Leads(string? stage, string? q)
    {
        var query = db.Leads.Include(l => l.Partner).Include(l => l.Owner).AsQueryable();
        if (stage == "open") query = query.Where(l => l.Stage != "won" && l.Stage != "lost");
        else if (!string.IsNullOrEmpty(stage)) query = query.Where(l => l.Stage == stage);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(l => EF.Functions.Like(l.Name, $"%{q}%") || EF.Functions.Like(l.Company!, $"%{q}%"));
        return new
        {
            leads = await query.OrderByDescending(l => l.CreatedAt)
                .Select(l => new { l.Id, l.Name, l.Company, l.Kind, l.Source, l.Stage, l.MonthlyKwh, partner = l.Partner!.Name, owner = l.Owner!.Name, l.CreatedAt })
                .ToListAsync(),
            stages = Stages, kinds = Kinds, sources = Sources,
            partners = await db.Partners.Where(p => p.Active).OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync(),
        };
    }

    /// <summary>The choices for the lead form.</summary>
    [HttpGet("options")]
    public async Task<object> Options() => new
    {
        stages = Stages, kinds = Kinds, sources = Sources,
        partners = await db.Partners.Where(p => p.Active).OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync(),
    };

    [HttpGet("leads/{id:int}")]
    public async Task<IActionResult> Lead(int id)
    {
        var lead = await db.Leads.FindAsync(id);
        if (lead is null) return NotFound();
        return Ok(new { lead, followUps = await db.FollowUps.Where(f => f.LeadId == id).OrderBy(f => f.DueOn).ToListAsync() });
    }

    public record LeadInput(string Name, string? Company, string? Email, string? Phone, string Kind, string Source, string Stage, int MonthlyKwh, int? PartnerId, string? LostReason, string? Notes);

    [HttpPost("leads")]
    public async Task<IActionResult> AddLead(LeadInput input)
    {
        var lead = new Lead { Owner = null, OwnerId = (await Permissions.CurrentAsync(HttpContext))!.Id };
        var problem = Apply(lead, input);
        if (problem != null) return BadRequest(new { error = problem });
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        return Ok(new { lead.Id });
    }

    [HttpPut("leads/{id:int}")]
    public async Task<IActionResult> UpdateLead(int id, LeadInput input)
    {
        var lead = await db.Leads.FindAsync(id);
        if (lead is null) return NotFound();
        var problem = Apply(lead, input);
        if (problem != null) return BadRequest(new { error = problem });
        await db.SaveChangesAsync();
        return Ok();
    }

    private static string? Apply(Lead lead, LeadInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return "Enter the lead's name.";
        if (!Stages.Contains(input.Stage)) return "Pick a stage.";
        var wasClosed = lead.Stage is "won" or "lost";
        lead.Name = input.Name.Trim(); lead.Company = input.Company?.Trim(); lead.Email = input.Email?.Trim(); lead.Phone = input.Phone?.Trim();
        lead.Kind = Kinds.Contains(input.Kind) ? input.Kind : Kinds[0];
        lead.Source = Sources.Contains(input.Source) ? input.Source : Sources[0];
        lead.Stage = input.Stage; lead.MonthlyKwh = Math.Max(0, input.MonthlyKwh); lead.PartnerId = input.PartnerId;
        lead.LostReason = input.Stage == "lost" ? input.LostReason : null; lead.Notes = input.Notes;
        var isClosed = lead.Stage is "won" or "lost";
        if (isClosed && !wasClosed) lead.ClosedAt = DateTime.UtcNow;
        if (!isClosed) lead.ClosedAt = null;
        return null;
    }

    /// <summary>Follow-ups that aren't done, oldest due first.</summary>
    [HttpGet("follow-ups")]
    public async Task<object> FollowUps() => await db.FollowUps.Include(f => f.Lead).Where(f => !f.Done).OrderBy(f => f.DueOn)
        .Select(f => new { f.Id, f.LeadId, lead = f.Lead!.Name, company = f.Lead.Company, f.DueOn, f.Task }).ToListAsync();

    public record NewFollowUp(int LeadId, DateOnly DueOn, string Task);

    [HttpPost("follow-ups")]
    public async Task<IActionResult> AddFollowUp(NewFollowUp input)
    {
        if (string.IsNullOrWhiteSpace(input.Task)) return BadRequest(new { error = "Say what needs doing." });
        if (!await db.Leads.AnyAsync(l => l.Id == input.LeadId)) return NotFound();
        db.FollowUps.Add(new FollowUp { LeadId = input.LeadId, DueOn = input.DueOn, Task = input.Task.Trim() });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("follow-ups/{id:int}/done")]
    public async Task<IActionResult> FollowUpDone(int id)
    {
        var followUp = await db.FollowUps.FindAsync(id);
        if (followUp is null) return NotFound();
        followUp.Done = true;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("partners")]
    public async Task<object> Partners() => await db.Partners.OrderBy(p => p.Name)
        .Select(p => new { p.Id, p.Name, p.Kind, p.Contact, p.Email, p.Phone, p.CommissionPercent, p.Active, leads = db.Leads.Count(l => l.PartnerId == p.Id) })
        .ToListAsync();

    public record PartnerInput(string Name, string Kind, string? Contact, string? Email, string? Phone, decimal CommissionPercent, bool Active);

    [HttpPost("partners")]
    public async Task<IActionResult> AddPartner(PartnerInput input) => await SavePartner(new Partner(), input, isNew: true);

    [HttpPut("partners/{id:int}")]
    public async Task<IActionResult> UpdatePartner(int id, PartnerInput input) =>
        await db.Partners.FindAsync(id) is { } partner ? await SavePartner(partner, input, isNew: false) : NotFound();

    private async Task<IActionResult> SavePartner(Partner partner, PartnerInput input, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Enter the name." });
        partner.Name = input.Name.Trim(); partner.Kind = input.Kind == "Partner" ? "Partner" : "Broker";
        partner.Contact = input.Contact?.Trim(); partner.Email = input.Email?.Trim(); partner.Phone = input.Phone?.Trim();
        partner.CommissionPercent = Math.Clamp(input.CommissionPercent, 0, 100); partner.Active = input.Active;
        if (isNew) db.Partners.Add(partner);
        await db.SaveChangesAsync();
        return Ok();
    }
}
