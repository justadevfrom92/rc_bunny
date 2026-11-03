using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;
using RcBunny.Data.Entities;
using RcBunny.Frontend.Services;

namespace RcBunny.Frontend.Pages;

/// <summary>Any other address: a page built in Lando (or 404). Built-in pages listed in cms.files go to their Razor pages.</summary>
[OutputCache(PolicyName = "Public")]
public class CmsPageModel(Cms cms) : PageModel
{
    public CmsPage Content { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string? path)
    {
        path = (path ?? "").Trim('/');
        if (path.EndsWith(".html")) path = path[..^5];
        var found = await cms.FindAsync(path) ?? await cms.FindAsync(path == "" ? "/" : path);
        if (found is null) return NotFound();
        if (found.Page.Redirect is { Length: > 0 } r) return RedirectPermanent(r.StartsWith("http") || r.StartsWith('/') ? r : "/" + r);
        if (found.Page.File is { } f)
            return Redirect(f switch { "index.html" => "/", "plans.html" => "/plans", "build-your-own-plan.html" => "/build-your-own-plan", "business.html" => "/business", "contact-us.html" => "/contact-us", _ => "/" });
        Content = found;
        return Page();
    }
}

[OutputCache(PolicyName = "Public")]
public class ShopModel(ShopCatalog shop) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Cat { get; set; }
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "featured";
    public IReadOnlyList<ShopProduct> Products { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var all = await shop.ProductsAsync();
        IEnumerable<ShopProduct> q = all.Where(p => Cat is null || p.Cat == Cat);
        q = Sort switch { "price" => q.OrderBy(p => p.Price), "price-desc" => q.OrderByDescending(p => p.Price), "rating" => q.OrderByDescending(p => p.Rating), "stars" => q.OrderByDescending(p => p.Stars), _ => q.OrderBy(p => p.Position) };
        Products = q.ToList();
    }
}

[OutputCache(PolicyName = "Public")]
public class ShopProductModel(ShopCatalog shop) : PageModel
{
    public ShopProduct Product { get; private set; } = null!;
    public string? Details { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var p = (await shop.ProductsAsync()).FirstOrDefault(x => ShopCatalog.Slug(x) == slug);
        if (p is null) return NotFound();
        Product = p;
        Details = AppConfig.At($"shop.details.{p.Cat}")?.ToString();
        return Page();
    }
}

[IgnoreAntiforgeryToken]
public class CartModel(ShopCatalog shop, Cart cart) : PageModel
{
    public List<(ShopProduct Product, int Qty)> Lines { get; private set; } = [];
    public decimal Subtotal => Lines.Sum(l => l.Product.Price * l.Qty);

    public async Task OnGetAsync()
    {
        var products = await shop.ProductsAsync();
        Lines = cart.Lines().Select(l => (products.FirstOrDefault(p => p.Sku == l.Sku), l.Qty)).Where(x => x.Item1 != null).Select(x => (x.Item1!, x.Qty)).ToList();
    }

    [EnableRateLimiting("forms")]
    public IActionResult OnPostAdd(string sku, int qty = 1)
    {
        var lines = cart.Lines(); lines.Add(new CartLine(sku, Math.Clamp(qty, 1, 10))); cart.Save(lines);
        return RedirectToPage();
    }

    public IActionResult OnPostUpdate(string sku, int qty)
    {
        cart.Save(cart.Lines().Select(l => l.Sku == sku ? l with { Qty = Math.Clamp(qty, 0, 10) } : l).ToList());
        return RedirectToPage();
    }
}

/// <summary>/survey/{slug}: questions under their category headings. A signed link (?c=&amp;sig=) ties the answer to the account.</summary>
[IgnoreAntiforgeryToken]
public class SurveyModel(AppDbContext db) : PageModel
{
    public Survey Survey { get; private set; } = null!;
    public List<SurveyQuestion> Questions { get; private set; } = [];
    public Dictionary<int, string> Categories { get; private set; } = [];
    public bool Done { get; private set; }
    public string? Error { get; private set; }
    public string? Closed { get; private set; }

    private async Task<bool> Load(string slug)
    {
        var s = await db.Surveys.FirstOrDefaultAsync(x => x.Slug == slug);
        if (s is null || s.Status == "draft") return false;
        Survey = s;
        Questions = await db.SurveyQuestions.Where(q => q.SurveyId == s.Id).OrderBy(q => q.Position).ToListAsync();
        Categories = await db.SurveyCategories.ToDictionaryAsync(c => c.Id, c => c.Name);
        var today = DateOnly.FromDateTime(DateTime.Today);
        Closed = s.Status == "closed" || s.ClosesOn < today ? "This survey is closed. Thank you!" : s.OpensOn > today ? $"This survey opens on {s.OpensOn:MMMM d}." : null;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(string slug) => await Load(slug) ? Page() : NotFound();

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync(string slug, int? c, string? sig)
    {
        if (!await Load(slug)) return NotFound();
        if (Closed != null) return Page();
        var answers = new JsonObject();
        foreach (var q in Questions)
        {
            var key = $"q{q.Id}";
            var vals = Request.Form[key].Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).ToList();
            if (vals.Count == 0) { if (q.Required) Error = "Please answer the questions marked *."; continue; }
            answers[q.Id.ToString()] = q.Type == "multi" ? new JsonArray(vals.Select(v => (JsonNode?)v).ToArray()) : vals[0][..Math.Min(2000, vals[0].Length)];
        }
        if (Error != null) return Page();
        int? customer = c is { } id && SurveyLinks.Verify(slug, id, sig) ? id : Services.SiteEndpoints.CustomerId(HttpContext);
        db.SurveyResponses.Add(new SurveyResponse { SurveyId = Survey.Id, CustomerId = customer, Answers = answers, Source = sig != null ? "email" : customer != null ? "my-account" : "website" });
        await db.SaveChangesAsync();
        Done = true;
        return Page();
    }
}
