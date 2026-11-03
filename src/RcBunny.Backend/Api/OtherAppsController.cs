using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RcBunny.Backend.Access;
using RcBunny.Data;

namespace RcBunny.Backend.Api;

/// <summary>Ranger → Web Messages: the website's Contact Us inbox.</summary>
[Route("api/ranger/messages"), RequirePerm("ranger")]
public class RangerMessagesController(AppDbContext db) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] bool handled = false) => Ok(await db.ContactMessages
        .Where(m => handled ? m.HandledAt != null : m.HandledAt == null)
        .OrderByDescending(m => m.CreatedAt).Take(200).ToListAsync());

    [HttpPost("{id:int}/handled")]
    public async Task<IActionResult> MarkHandled(int id)
    {
        var m = await db.ContactMessages.AsTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        m.HandledAt = DateTime.UtcNow;
        m.HandledBy = Me.UserId;
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}

/// <summary>Astro → View Plans: each plan with its current rate per market (read-only for now).</summary>
[Route("api/astro/plans"), RequirePerm("astro")]
public class AstroPlansController(AppDbContext db, HybridCache cache) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => Ok(await cache.GetOrCreateAsync("astro:plans", async token =>
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var markets = await db.Markets.OrderBy(m => m.Id).Select(m => new { m.Id, m.Name, m.Short }).ToListAsync(token);
        var rates = (await db.Rates.Where(r => r.EffectiveOn <= today).ToListAsync(token))
            .GroupBy(r => (r.PlanId, r.MarketId)).ToDictionary(g => g.Key, g => g.OrderBy(r => r.EffectiveOn).ThenBy(r => r.Id).Last().Energy);
        var plans = await db.Plans.OrderBy(p => p.Type).ThenBy(p => p.Term).ThenBy(p => p.Name).ToListAsync(token);
        return new
        {
            markets,
            plans = plans.Select(p => new
            {
                p.Id, p.Type, p.Term, p.Name, p.Internal, p.Etf, p.Mrc, p.Green, p.Active, p.Tags,
                rates = markets.ToDictionary(m => m.Short, m => rates.TryGetValue((p.Id, m.Id), out var e) ? e : (decimal?)null),
            }),
        };
    }, new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) }, ["catalog"], ct));
}
