using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Services;

/// <summary>The website Shop's catalog rules and checkout, used when an employee orders for a customer by phone (Corral).</summary>
public static class Shop
{
    public static readonly Dictionary<string, string> Categories = new()
    {
        ["thermostats"] = "Smart Thermostats", ["ev"] = "EV Charging", ["lighting"] = "Lighting", ["plugs"] = "Smart Plugs & Power",
        ["protection"] = "Protection Plans", ["solar"] = "Solar & Backup", ["gear"] = "Rewards Gear",
    };

    public static readonly string[] Icons = ["thermo", "sensor", "ev", "cable", "bulb", "plug", "meter", "shield", "house", "sun", "battery", "tools", "cap", "cup", "bag"];

    /// <summary>Protection plans: added to the monthly bill (price ÷ 12) and put on the account as a product, instead of charged today.</summary>
    public static readonly Dictionary<string, string> Protection = new()
    {
        ["PR-500"] = "Surge Protection Program", ["PR-510"] = "Air Conditioning Protection Program",
        ["PR-520"] = "Electric System Protection Program", ["PR-530"] = "Electric Vehicle Charger Protection Program",
    };

    public const decimal TaxRate = 0.0825m;

    public record Line(string Sku, string Name, int Qty, decimal Price, decimal LineTotal, bool Monthly, int Stars, bool InStock);
    public record Summary(List<Line> Lines, decimal Subtotal, decimal Tax, decimal Total, decimal Monthly, int Stars, List<string> Unavailable);

    /// <summary>Totals for sku → quantity: due today (goods + tax) and added to the monthly bill (protection plans).</summary>
    public static async Task<Summary> SummarizeAsync(AppDbContext db, Dictionary<string, int> items)
    {
        var skus = items.Keys.ToList();
        var products = await db.ShopProducts.Where(p => skus.Contains(p.Sku) && p.Active).ToListAsync();
        var lines = products.Select(p =>
        {
            var monthly = Protection.ContainsKey(p.Sku);
            var qty = monthly ? 1 : Math.Clamp(items[p.Sku], 1, 10);
            return new Line(p.Sku, p.Name, qty, p.Price, Math.Round(monthly ? p.Price / 12 : p.Price * qty, 2), monthly, p.Stars * qty, p.InStock);
        }).ToList();
        var subtotal = Math.Round(lines.Where(l => !l.Monthly).Sum(l => l.LineTotal), 2);
        var tax = Math.Round(subtotal * TaxRate, 2);
        return new Summary(lines, subtotal, tax, subtotal + tax, Math.Round(lines.Where(l => l.Monthly).Sum(l => l.LineTotal), 2),
            lines.Sum(l => l.Stars), lines.Where(l => !l.InStock).Select(l => l.Name).ToList());
    }

    /// <summary>Places the order: protection plans go on the account, stars are credited, and a note goes on the account.</summary>
    public static async Task<ShopOrder> PlaceAsync(AppDbContext db, Customer c, Summary s, string payWith, int? methodId, string shipTo, string? placedBy)
    {
        var next = (await db.ShopOrders.MaxAsync(o => (int?)o.Id) ?? 0) + 1;
        var order = new ShopOrder
        {
            Number = $"SO-{100000 + next}", CustomerId = c.Id,
            Items = JsonSerializer.Serialize(s.Lines.Select(l => new { sku = l.Sku, name = l.Name, qty = l.Qty, price = l.Price, line = l.LineTotal, monthly = l.Monthly, stars = l.Stars })),
            Subtotal = s.Subtotal, Tax = s.Tax, Total = s.Total, Monthly = s.Monthly, Stars = s.Stars, PayWith = payWith,
            PaymentMethodId = payWith == "card" ? methodId : null, ShipTo = shipTo, PaymentStatus = payWith == "bill" ? "On next bill" : "Pending", PlacedBy = placedBy,
        };
        db.ShopOrders.Add(order);
        foreach (var l in s.Lines.Where(l => l.Monthly)) await Products.AddAsync(db, c, Protection[l.Sku], placedBy ?? "Customer (MyAccount)");
        if (s.Stars > 0)
        {
            db.StarEntries.Add(new StarEntry { CustomerId = c.Id, Stars = s.Stars, Reason = $"Shop order {order.Number}", By = placedBy ?? "My Account" });
            c.Stars += s.Stars;
        }
        db.Notes.Add(new Note
        {
            CustomerId = c.Id, Author = placedBy ?? "Customer (MyAccount)", Category = "Shop", Action = "Shop Order", Priority = "Low",
            Body = (placedBy != null ? $"Placed shop order {order.Number} for the customer by phone" : $"Placed shop order {order.Number}")
                + $" for {order.Total:C}" + (order.Monthly > 0 ? $" plus {order.Monthly:C}/mo in protection plans" : "") + ".",
        });
        await db.SaveChangesAsync();
        return order;
    }

    public static string ShipTo(Customer c, string? street = null, string? city = null, string? zip = null) =>
        street is { Length: > 0 }
            ? $"{c.Name}|{street}|{city}|TX|{zip}"
            : $"{c.Name}|{c.Address}{(c.Unit is { Length: > 0 } u ? " #" + u : "")}|{c.City}|TX|{c.Zip}";
}
