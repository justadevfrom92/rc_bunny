using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using RcBunny.Frontend.Services;

namespace RcBunny.Frontend.Pages;

public sealed record PlanCardModel(PlanView Plan, decimal? Price, bool Featured, string? Zip);

/// <summary>What a plan card island needs: the same numbers the server rendered, so it never fetches anything.</summary>
public sealed record PlanCardProps(int Id, string Name, string Internal, string Slug, int Term, string TermLabel, string EtfLabel,
    IReadOnlyList<string> Tags, IReadOnlyList<string> Perks, decimal? Price, bool Featured);

public static class PlanCards
{
    public static PlanCardProps Props(PlanView p, decimal? price, bool featured) =>
        new(p.Id, p.Name, p.Internal, p.Slug, p.Term, RcBunny.Data.Pricing.TermLabel(p.Term), RcBunny.Data.Pricing.EtfLabel(p.Etf),
            p.Tags, p.Perks, price is { } x ? Math.Round(x, 1) : null, featured);
}

[OutputCache(PolicyName = "Public")]
public class IndexModel(Catalog catalog) : PageModel
{
    public IReadOnlyList<PlanCardModel> Featured { get; private set; } = [];
    public object ZipProps { get; private set; } = new { };

    public async Task OnGetAsync()
    {
        var c = await catalog.GetAsync();
        Featured = c.Group("featured").Select(p => new PlanCardModel(p, c.Price(p.Internal, CatalogSnapshot.DefaultMarket), true, null)).ToList();
        ZipProps = new { ranges = c.ZipRanges.Select(z => new[] { z.From, z.To }) };
    }
}

[OutputCache(PolicyName = "Public")]
public class BusinessModel : PageModel
{
    public void OnGet() { }
}

[OutputCache(PolicyName = "Public")]
public class BuildYourOwnPlanModel(Catalog catalog) : PageModel
{
    public static readonly string[] Steps = ["Get Started", "Lower Your Bill", "Using Less", "Save More", "Protectin'"];
    public static readonly int[] Terms = [1, 6, 12, 18, 24, 30, 36];

    public CatalogSnapshot Catalog { get; private set; } = null!;
    public object Props { get; private set; } = new { };
    public decimal Base12 { get; private set; }

    public async Task OnGetAsync()
    {
        var c = Catalog = await catalog.GetAsync();
        var market = c.Markets.FirstOrDefault(m => m.Name == CatalogSnapshot.DefaultMarket);
        var region = market?.Region ?? "Generic";
        Base12 = c.Price("BYOP", CatalogSnapshot.DefaultMarket) ?? 14m;
        Props = new
        {
            steps = Steps,
            terms = Terms,
            base12 = Base12,
            baseMonthly = c.Plan("BYOP")?.Mrc ?? 4.95m,
            // discount per term for this region, relative to 12 months (the BYOP plan's own term)
            termAdj = Terms.ToDictionary(t => t.ToString(), t =>
                (c.TermMods.GetValueOrDefault(12)?.GetValueOrDefault(region) ?? 0) - (c.TermMods.GetValueOrDefault(t)?.GetValueOrDefault(region) ?? 0)),
            etfs = Terms.ToDictionary(t => t.ToString(), t => c.Etfs.GetValueOrDefault(t)),
            products = c.Byop,
            defaults = new[] { "autopay", "paperless", "rewards" },
        };
    }
}

[OutputCache(PolicyName = "Public")]
public class PlansIndexModel(Catalog catalog) : PageModel
{
    public static readonly (string Key, string Label)[] Filters =
        [("all", "All Plans"), ("month", "Month-to-Month"), ("12", "12 Months"), ("18", "18 Months"), ("24", "24 Months"), ("36", "36 Months"), ("green", "100% Green")];

    [BindProperty(SupportsGet = true)] public string? Zip { get; set; }
    [BindProperty(SupportsGet = true, Name = "filter")] public string? Filter { get; set; }

    public MarketView? Market { get; private set; }
    public IReadOnlyList<PlanCardModel> Cards { get; private set; } = [];
    public object Props { get; private set; } = new { };

    public async Task OnGetAsync()
    {
        var c = await catalog.GetAsync();
        Market = c.MarketForZip(Zip);
        var market = Market?.Name ?? CatalogSnapshot.DefaultMarket;
        var featured = c.Group("featured").Select(p => p.Internal).ToHashSet();
        // Same list as ebunny: the "resi" plan group (Astro → Plan Groups), in its order, minus plans not sold in the area.
        var all = c.Group("resi")
            .Select(p => new PlanCardModel(p, c.Price(p.Internal, market), featured.Contains(p.Internal), Market is null ? null : Zip))
            .Where(x => x.Price is not null || Market is null)
            .ToList();
        var filter = Filters.Any(f => f.Key == Filter) ? Filter! : "all";
        Filter = filter;
        Cards = all.Where(x => Matches(x.Plan, filter)).ToList();
        Props = new
        {
            filter,
            filters = Filters.Select(f => new { key = f.Key, label = f.Label }),
            zip = Market is null ? null : Zip,
            zipNote = Market is not null ? $"Showing plans for {Zip} · {Market.Description}"
                : string.IsNullOrEmpty(Zip) ? null : $"We don't serve {Zip} yet — showing prices for the Oncor area.",
            plans = all.Select(x => PlanCards.Props(x.Plan, x.Price, x.Featured)),
        };
    }

    public static bool Matches(PlanView p, string f) => f switch
    {
        "all" => true,
        "month" => p.Term == 1,
        _ when int.TryParse(f, out var t) => p.Term == t,
        _ => p.Tags.Contains(f),
    };
}

[OutputCache(PolicyName = "Public")]
public class PlanDetailModel(Catalog catalog) : PageModel
{
    public PlanView Plan { get; private set; } = null!;
    public IReadOnlyList<(MarketView Market, decimal? P500, decimal? P1000, decimal? P2000)> Prices { get; private set; } = [];
    public MarketView? ZipMarket { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, string? zip)
    {
        var c = await catalog.GetAsync();
        var plan = c.Plans.FirstOrDefault(p => p.Slug == slug);
        if (plan is null) return NotFound();
        Plan = plan;
        ZipMarket = c.MarketForZip(zip);
        Prices = c.Markets.Select(m => (m, c.Price(plan.Internal, m.Name, 500), c.Price(plan.Internal, m.Name, 1000), c.Price(plan.Internal, m.Name, 2000)))
            .Where(x => x.Item3 is not null).ToList();
        return Page();
    }
}
