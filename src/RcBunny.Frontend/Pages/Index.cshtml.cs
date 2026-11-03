using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using RcBunny.Data;
using RcBunny.Frontend.Services;

namespace RcBunny.Frontend.Pages;

[OutputCache(PolicyName = "Public")]
public class IndexModel(Catalog catalog) : PageModel
{
    public List<PlanPrice> Featured { get; private set; } = [];

    /// <summary>The cheapest residential plan for each of the 12, 24 and 36 month terms.</summary>
    public async Task OnGetAsync()
    {
        var prices = await catalog.PricesAsync(await catalog.DefaultMarketAsync());
        Featured = prices.Where(p => p.Plan.Type == "Residential" && p.Plan.TermMonths is 12 or 24 or 36)
            .GroupBy(p => p.Plan.TermMonths).Select(g => g.MinBy(p => p.PriceAt1000)!).ToList();
    }
}
