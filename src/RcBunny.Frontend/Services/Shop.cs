using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Frontend.Services;

/// <summary>Shop products (Corral → Products), cached like the plan catalog.</summary>
public sealed class ShopCatalog(IServiceScopeFactory scopes, IMemoryCache cache)
{
    public async Task<IReadOnlyList<ShopProduct>> ProductsAsync() => (await cache.GetOrCreateAsync("shop:products", async e =>
    {
        e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ShopProducts.Where(p => p.Active).OrderBy(p => p.Position).ToListAsync();
    }))!;

    public static string Slug(ShopProduct p) => System.Text.RegularExpressions.Regex.Replace($"{p.Name}-{p.Sku}".ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    public static IReadOnlyDictionary<string, string> Categories =>
        AppConfig.At("shop.categories") is System.Text.Json.Nodes.JsonObject o ? o.ToDictionary(kv => kv.Key, kv => kv.Value is System.Text.Json.Nodes.JsonArray a ? a[0]!.ToString() : kv.Value!.ToString()) : new Dictionary<string, string>();
}

public sealed record CartLine(string Sku, int Qty);

/// <summary>The cart lives in an encrypted cookie: no sign-in needed to shop, and nothing to clean up server-side.</summary>
public sealed class Cart(IHttpContextAccessor http, IDataProtectionProvider dp)
{
    private const string Cookie = "rcb.cart";
    private readonly IDataProtector protector = dp.CreateProtector("cart");

    public List<CartLine> Lines()
    {
        var raw = http.HttpContext?.Request.Cookies[Cookie];
        if (raw is null) return [];
        try { return JsonSerializer.Deserialize<List<CartLine>>(protector.Unprotect(raw)) ?? []; }
        catch { return []; }
    }

    public void Save(List<CartLine> lines)
    {
        var ctx = http.HttpContext!;
        lines = lines.Where(l => l.Qty > 0).GroupBy(l => l.Sku).Select(g => new CartLine(g.Key, Math.Min(10, g.Sum(x => x.Qty)))).ToList();
        if (lines.Count == 0) ctx.Response.Cookies.Delete(Cookie);
        else ctx.Response.Cookies.Append(Cookie, protector.Protect(JsonSerializer.Serialize(lines)), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(30), IsEssential = true });
    }

    public int Count() => Lines().Sum(l => l.Qty);

    public const decimal TaxRate = 0.0825m;
    public static decimal Shipping(decimal subtotal) => subtotal is 0 or >= 99 ? 0 : 7.95m;
}
