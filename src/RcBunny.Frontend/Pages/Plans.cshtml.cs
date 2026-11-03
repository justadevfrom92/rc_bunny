using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using RcBunny.Data;
using RcBunny.Frontend.Services;

namespace RcBunny.Frontend.Pages;

[OutputCache(PolicyName = "Public")]
public class PlansModel(Catalog catalog) : PageModel
{
    public List<PlanPrice> Plans { get; private set; } = [];
    public string? Zip { get; private set; }
    public bool Business { get; private set; }
    public string Note { get; private set; } = "";

    public async Task OnGetAsync(string? zip, string? type)
    {
        Zip = zip;
        Business = type == "business";
        var market = await catalog.MarketForZipAsync(zip);
        if (market is not null) Note = $"Showing prices for {zip} · {market.Name}";
        else if (!string.IsNullOrEmpty(zip)) Note = $"We don't serve {zip} yet. Showing prices for the Oncor area.";
        else Note = "Enter your zip code for prices at your address.";

        market ??= await catalog.DefaultMarketAsync();
        Plans = (await catalog.PricesAsync(market)).Where(p => p.Plan.Type == (Business ? "Business" : "Residential")).ToList();
    }
}
