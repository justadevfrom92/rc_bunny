using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

[ApiController]
[Route("api/plans")]
[RequirePermission("plans")]
public class PlansController(AppDbContext db) : ControllerBase
{
    /// <summary>Every plan with today's energy rate in each market.</summary>
    [HttpGet]
    public async Task<object> List()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var markets = await db.Markets.OrderBy(m => m.Name).Select(m => new { m.Id, m.Name }).ToListAsync();
        var rates = Pricing.CurrentRates(await db.Rates.ToListAsync(), today);
        var plans = (await db.Plans.OrderBy(p => p.Type).ThenBy(p => p.TermMonths).ThenBy(p => p.Name).ToListAsync())
            .Select(p => new
            {
                p.Id, p.Name, p.Code, p.Type, p.TermMonths, p.Active,
                rates = markets.ToDictionary(m => m.Id.ToString(),
                    m => rates.Where(r => r.PlanId == p.Id && r.MarketId == m.Id).MaxBy(r => r.EffectiveOn)?.EnergyCents),
            });
        return new { markets, plans };
    }

    public record NewRate(int MarketId, decimal EnergyCents, DateOnly EffectiveOn);

    /// <summary>Add a rate. A future date means it starts that day; the website picks it up within a minute.</summary>
    [HttpPost("{id:int}/rates")]
    public async Task<IActionResult> AddRate(int id, NewRate rate)
    {
        if (rate.EnergyCents is <= 0 or > 100) return BadRequest(new { error = "Enter a rate between 0 and 100 cents." });
        if (!await db.Plans.AnyAsync(p => p.Id == id) || !await db.Markets.AnyAsync(m => m.Id == rate.MarketId)) return NotFound();
        db.Rates.Add(new Rate { PlanId = id, MarketId = rate.MarketId, EnergyCents = rate.EnergyCents, EffectiveOn = rate.EffectiveOn });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active)
    {
        var plan = await db.Plans.FindAsync(id);
        if (plan is null) return NotFound();
        plan.Active = active;
        await db.SaveChangesAsync();
        return Ok();
    }
}
