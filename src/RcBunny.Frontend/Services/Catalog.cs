using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Data;

namespace RcBunny.Frontend.Services;

/// <summary>Markets and plan prices for the website, kept in memory for a minute so pages don't wait on the database.</summary>
public class Catalog(AppDbContext db, IMemoryCache cache)
{
    private static readonly TimeSpan KeepFor = TimeSpan.FromMinutes(1);

    public async Task<List<Market>> MarketsAsync() =>
        (await cache.GetOrCreateAsync("markets", e => { e.AbsoluteExpirationRelativeToNow = KeepFor; return db.Markets.OrderBy(m => m.Name).ToListAsync(); }))!;

    /// <summary>The market that serves a zip code, or null if we don't serve it.</summary>
    public async Task<Market?> MarketForZipAsync(string? zip)
    {
        if (!int.TryParse(zip, out var z)) return null;
        var ranges = (await cache.GetOrCreateAsync("zips", e => { e.AbsoluteExpirationRelativeToNow = KeepFor; return db.ZipRanges.ToListAsync(); }))!;
        var range = ranges.FirstOrDefault(r => z >= r.From && z <= r.To);
        return range is null ? null : (await MarketsAsync()).FirstOrDefault(m => m.Id == range.MarketId);
    }

    /// <summary>Prices shown when we don't know the visitor's zip yet (the Oncor area, like ebunny).</summary>
    public async Task<Market> DefaultMarketAsync()
    {
        var markets = await MarketsAsync();
        return markets.FirstOrDefault(m => m.Code == "TX-E-ONCOR") ?? markets.First();
    }

    public async Task<List<PlanPrice>> PricesAsync(Market market) =>
        (await cache.GetOrCreateAsync($"prices:{market.Id}", e => { e.AbsoluteExpirationRelativeToNow = KeepFor; return Pricing.PricesAsync(db, market); }))!;
}
