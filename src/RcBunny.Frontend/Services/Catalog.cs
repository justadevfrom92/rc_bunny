using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Frontend.Services;

public sealed record PlanView(int Id, string Type, int Term, string Name, string Internal, string Slug, string Etf, decimal Mrc,
    int Green, IReadOnlyList<string> Tags, IReadOnlyList<string> Perks);

public sealed record MarketView(int Id, string Name, string Short, string Description, string Region, string? Phone);

public sealed record FeeView(decimal PerKwh, decimal PerBill);

public sealed record ByopView(string Key, string Name, string Type, string Step, string? Description, decimal Adj, decimal Monthly);

/// <summary>
/// Everything the website shows about plans and prices, read in one go and kept in memory.
/// Plain immutable data, so every request on the server shares one copy.
/// </summary>
public sealed class CatalogSnapshot
{
    public const string DefaultMarket = "TX-E-ONCOR";

    public required IReadOnlyList<MarketView> Markets { get; init; }
    public required IReadOnlyList<(int From, int To, string Market)> ZipRanges { get; init; }
    public required IReadOnlyDictionary<string, FeeView> Fees { get; init; }               // market name → current fee
    public required IReadOnlyList<PlanView> Plans { get; init; }                            // active plans only
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Groups { get; init; } // slug → plan codes
    public required IReadOnlyDictionary<(string Plan, string Market), decimal> Rates { get; init; }
    public required IReadOnlyDictionary<int, IReadOnlyDictionary<string, decimal>> TermMods { get; init; }
    public required IReadOnlyDictionary<int, int> Etfs { get; init; }
    public required IReadOnlyList<ByopView> Byop { get; init; }
    public DateTime LoadedAt { get; init; } = DateTime.UtcNow;

    public PlanView? Plan(string code) => Plans.FirstOrDefault(p => p.Internal == code);

    public IReadOnlyList<PlanView> Group(string slug) =>
        Groups.TryGetValue(slug, out var codes) ? codes.Select(Plan).OfType<PlanView>().ToList() : [];

    public MarketView? MarketForZip(string? zip)
    {
        if (!int.TryParse(zip, out var z)) return null;
        var r = ZipRanges.FirstOrDefault(x => z >= x.From && z <= x.To);
        return r.Market is null ? null : Markets.FirstOrDefault(m => m.Name == r.Market);
    }

    /// <summary>Average ¢/kWh at a usage level (EFL style), or null when the plan isn't sold in that market.</summary>
    public decimal? Price(string planCode, string market, int kwh = 1000)
    {
        var plan = Plan(planCode);
        if (plan is null || !Rates.TryGetValue((planCode, market), out var energy) || !Fees.TryGetValue(market, out var fee)) return null;
        return Pricing.AveragePrice(energy, fee.PerKwh, fee.PerBill, plan.Mrc, kwh);
    }
}

/// <summary>
/// Loads the catalog snapshot at most every 5 minutes (configurable). Rate/fee rows carry effective dates,
/// so "current" is recomputed on each load; a price saved in the admin shows on the website within that window.
/// </summary>
public sealed class Catalog(IServiceScopeFactory scopes, IMemoryCache cache, IConfiguration config)
{
    private const string Key = "catalog:snapshot";
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<CatalogSnapshot> GetAsync()
    {
        if (cache.TryGetValue(Key, out CatalogSnapshot? snap)) return snap!;
        await Gate.WaitAsync();   // one database load at a time, even when the cache expires under traffic
        try
        {
            if (cache.TryGetValue(Key, out snap)) return snap!;
            snap = await LoadAsync();
            cache.Set(Key, snap, TimeSpan.FromSeconds(config.GetValue("Website:CatalogCacheSeconds", 300)));
            return snap;
        }
        finally
        {
            Gate.Release();
        }
    }

    public void Invalidate() => cache.Remove(Key);

    private async Task<CatalogSnapshot> LoadAsync()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var markets = await db.Markets.OrderBy(m => m.Id).ToListAsync();
        var marketName = markets.ToDictionary(m => m.Id, m => m.Name);
        var plans = await db.Plans.OrderBy(p => p.Id).ToListAsync();
        var planCode = plans.ToDictionary(p => p.Id, p => p.Internal);

        static TValue Latest<T, TValue>(IEnumerable<T> rows, Func<T, DateOnly> date, Func<T, int> id, Func<T, TValue> pick) =>
            pick(rows.OrderBy(date).ThenBy(id).Last());

        var fees = (await db.TdspFees.Where(f => f.EffectiveOn <= today).ToListAsync())
            .GroupBy(f => f.MarketId)
            .ToDictionary(g => marketName[g.Key], g => Latest(g, f => f.EffectiveOn, f => f.Id, f => new FeeView(f.PerKwh, f.PerBill)));
        var rates = (await db.Rates.Where(r => r.EffectiveOn <= today).ToListAsync())
            .GroupBy(r => (r.PlanId, r.MarketId))
            .ToDictionary(g => (planCode[g.Key.PlanId], marketName[g.Key.MarketId]), g => Latest(g, r => r.EffectiveOn, r => r.Id, r => r.Energy));

        var groups = await db.PlanGroups.Include(g => g.Plans).ToListAsync();
        var mods = await db.TermDiscounts.ToListAsync();

        return new CatalogSnapshot
        {
            Markets = markets.Select(m => new MarketView(m.Id, m.Name, m.Short, m.Description, m.Region, m.Phone)).ToList(),
            ZipRanges = (await db.ZipRanges.ToListAsync()).Select(z => (z.ZipFrom, z.ZipTo, marketName[z.MarketId])).ToList(),
            Fees = fees,
            Plans = plans.Where(p => p.Active)
                .Select(p => new PlanView(p.Id, p.Type, p.Term, p.Name, p.Internal, p.Slug, p.Etf, p.Mrc, p.Green, p.Tags, p.Perks)).ToList(),
            Groups = groups.ToDictionary(g => g.Slug, g => (IReadOnlyList<string>)g.Plans.OrderBy(x => x.Position).Select(x => planCode[x.PlanId]).ToList()),
            Rates = rates,
            TermMods = mods.GroupBy(m => m.Term).ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, decimal>)g.ToDictionary(m => m.Region, m => m.Discount)),
            Etfs = await db.TermEtfs.ToDictionaryAsync(e => e.Term, e => e.Amount),
            Byop = (await db.ByopProducts.Where(p => p.Active).OrderBy(p => p.Position).ToListAsync())
                .Select(p => new ByopView(p.Key, p.Name, p.Type, p.Step, p.Description, p.RateAdj, p.Monthly)).ToList(),
        };
    }
}
