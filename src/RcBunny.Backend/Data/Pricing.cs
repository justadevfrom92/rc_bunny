using Microsoft.EntityFrameworkCore;

namespace RcBunny.Data;

public record PlanPrice(Plan Plan, decimal EnergyCents, decimal PriceAt1000);

public static class Pricing
{
    /// <summary>
    /// The advertised price: cents per kWh at 1,000 kWh a month, including utility delivery charges and the plan's monthly fee.
    /// </summary>
    public static decimal PriceAt1000(decimal energyCents, Market market, Plan plan) =>
        Math.Round(energyCents + market.DeliveryCentsPerKwh + (market.DeliveryPerMonth + plan.MonthlyFee) * 100 / 1000, 1);

    /// <summary>
    /// The rate in effect today out of a plan's rates in one market: the latest one that has started. When none has
    /// started yet (a new plan, or a computer whose clock is behind the sample data) it's the earliest one.
    /// </summary>
    public static Rate? CurrentRate(IEnumerable<Rate> rates, DateOnly today) =>
        rates.Where(r => r.EffectiveOn <= today).MaxBy(r => r.EffectiveOn) ?? rates.MinBy(r => r.EffectiveOn);

    /// <summary>Today's rate for every plan and market that has one.</summary>
    public static List<Rate> CurrentRates(IEnumerable<Rate> rates, DateOnly today) =>
        rates.GroupBy(r => (r.PlanId, r.MarketId)).Select(g => CurrentRate(g, today)!).ToList();

    /// <summary>Every active plan with today's rate in a market (plans without a rate there are left out).</summary>
    public static async Task<List<PlanPrice>> PricesAsync(AppDbContext db, Market market)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var plans = await db.Plans.Where(p => p.Active).ToListAsync();
        var rates = await db.Rates.Where(r => r.MarketId == market.Id).ToListAsync();
        return plans
            .Select(p => (Plan: p, Rate: CurrentRate(rates.Where(r => r.PlanId == p.Id), today)))
            .Where(x => x.Rate != null)
            .Select(x => new PlanPrice(x.Plan, x.Rate!.EnergyCents, PriceAt1000(x.Rate.EnergyCents, market, x.Plan)))
            .OrderBy(x => x.Plan.TermMonths).ThenBy(x => x.PriceAt1000)
            .ToList();
    }

    public static string TermLabel(int months) => months <= 1 ? "Month to month" : $"{months} months";
}
