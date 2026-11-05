using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Services;

namespace RcBunny.Controllers;

/// <summary>Corral → Products: the website Shop's products, every shop order, and ordering for a customer on the phone.</summary>
[ApiController]
[Route("api/corral/shop")]
[RequirePermission("corral")]
public class ShopController(AppDbContext db) : ControllerBase
{
    public static readonly string[] OrderStatuses = ["Placed", "Shipped", "Delivered", "Canceled"];

    /// <summary>How many of each SKU have sold, from every order's items.</summary>
    private async Task<Dictionary<string, int>> Sold()
    {
        var all = await db.ShopOrders.Select(o => o.Items).ToListAsync();
        return all.SelectMany(i => JsonDocument.Parse(i).RootElement.EnumerateArray())
            .GroupBy(i => i.GetProperty("sku").GetString()!).ToDictionary(g => g.Key, g => g.Sum(i => i.GetProperty("qty").GetInt32()));
    }

    [HttpGet("products")]
    public async Task<object> Products(string? cat, int page = 1)
    {
        var query = db.ShopProducts.Where(p => cat == null || p.Category == cat);
        return new
        {
            categories = Shop.Categories, protection = Shop.Protection.Keys, icons = Shop.Icons, cat, page, total = await query.CountAsync(), sold = await Sold(),
            counts = await db.ShopProducts.GroupBy(p => p.Category).Select(g => new { cat = g.Key, n = g.Count() }).ToListAsync(),
            products = await query.OrderBy(p => p.Position).ThenBy(p => p.Id).Skip((Math.Max(page, 1) - 1) * 10).Take(10).ToListAsync(),
        };
    }

    [HttpGet("products/{id:int}")]
    public async Task<IActionResult> Product(int id)
    {
        var p = await db.ShopProducts.FindAsync(id);
        if (p is null) return NotFound();
        var orders = (await db.ShopOrders.Include(o => o.Customer).OrderByDescending(o => o.Id).ToListAsync())
            .Where(o => o.Items.Contains($"\"sku\":\"{p.Sku}\"")).Take(10)
            .Select(o => new
            {
                o.Id, o.Number, o.CreatedAt, o.Status, o.Total, o.CustomerId, customer = o.Customer?.Name, account = o.Customer?.Account,
                qty = System.Text.Json.JsonDocument.Parse(o.Items).RootElement.EnumerateArray().Where(i => i.GetProperty("sku").GetString() == p.Sku).Sum(i => i.GetProperty("qty").GetInt32()),
            });
        return Ok(new { product = p, categories = Shop.Categories, protection = Shop.Protection.Keys, icons = Shop.Icons, sold = (await Sold()).GetValueOrDefault(p.Sku), orders });
    }

    public record ProductInput(string Sku, string Name, string Category, decimal Price, decimal? Was, int Stars, string? Badge, string Icon, string? Blurb, bool InStock, bool Active);

    [HttpPost("products")]
    public Task<IActionResult> Add(ProductInput input) => Save(null, input);

    [HttpPut("products/{id:int}")]
    public Task<IActionResult> Update(int id, ProductInput input) => Save(id, input);

    private async Task<IActionResult> Save(int? id, ProductInput input)
    {
        var sku = input.Sku.Trim().ToUpper();
        if (sku.Length is 0 or > 20 || !sku.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')) return BadRequest(new { error = "The SKU is letters, numbers and dashes (up to 20)." });
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Name the product." });
        if (!Shop.Categories.ContainsKey(input.Category) || !Shop.Icons.Contains(input.Icon)) return BadRequest(new { error = "Pick a category and an icon." });
        if (input.Price < 0 || input.Stars < 0) return BadRequest(new { error = "Price and stars can't be negative." });
        if (input.Was is { } was && was <= input.Price) return BadRequest(new { error = "The \"was\" price has to be higher than the price (it shows as a sale)." });
        if (await db.ShopProducts.AnyAsync(p => p.Sku == sku && p.Id != id)) return BadRequest(new { error = "Another product has that SKU." });
        var p = id is null ? new ShopProduct { Position = (await db.ShopProducts.MaxAsync(x => (int?)x.Position) ?? 0) + 1 } : await db.ShopProducts.FindAsync(id.Value);
        if (p is null) return NotFound();
        (p.Sku, p.Name, p.Category, p.Price, p.Was, p.Stars, p.Badge, p.Icon, p.Blurb, p.InStock, p.Active) =
            (sku, input.Name.Trim(), input.Category, input.Price, input.Was, input.Stars, input.Badge?.Trim(), input.Icon, input.Blurb?.Trim(), input.InStock, input.Active);
        if (id is null) db.ShopProducts.Add(p);
        await db.SaveChangesAsync();
        return Ok(new { p.Id, message = $"{p.Name} saved. {(p.Active ? "It's in the website Shop." : "It's hidden from the website Shop.")}" });
    }

    [HttpGet("orders")]
    public async Task<object> Orders(string? status, int page = 1)
    {
        var query = db.ShopOrders.Where(o => status == null || o.Status == status);
        return new
        {
            statuses = OrderStatuses, page, total = await query.CountAsync(),
            orders = await query.OrderByDescending(o => o.Id).Skip((Math.Max(page, 1) - 1) * 10).Take(10)
                .Select(o => new { o.Id, o.Number, o.CreatedAt, o.PlacedBy, o.Items, o.Total, o.Monthly, o.Status, o.PaymentStatus, o.CustomerId, account = o.Customer!.Account, customer = o.Customer.Name })
                .ToListAsync(),
        };
    }

    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> Order(int id)
    {
        var o = await db.ShopOrders.Include(x => x.Customer).Include(x => x.PaymentMethod).FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return NotFound();
        return Ok(new { order = o, customer = new { o.Customer!.Id, o.Customer.Account, o.Customer.Name }, method = o.PaymentMethod?.Label(), statuses = OrderStatuses });
    }

    public record StatusInput(string Status);

    [HttpPut("orders/{id:int}")]
    public async Task<IActionResult> SetStatus(int id, StatusInput input)
    {
        if (!OrderStatuses.Contains(input.Status)) return BadRequest(new { error = "Pick a status." });
        var o = await db.ShopOrders.FindAsync(id);
        if (o is null) return NotFound();
        o.Status = input.Status;
        var me = (await Permissions.CurrentAsync(HttpContext))!;
        db.Notes.Add(new Note { CustomerId = o.CustomerId, Author = me.Name, Category = "Shop", Action = "Shop Order", Priority = "Low", Body = $"Shop order {o.Number} marked {input.Status}." });
        await db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Order for a customer by phone: products to pick from and the cards on the account.</summary>
    [HttpGet("order-for/{customerId:int}")]
    public async Task<IActionResult> OrderForm(int customerId)
    {
        var c = await db.Customers.FindAsync(customerId);
        if (c is null) return NotFound();
        return Ok(new
        {
            customer = new { c.Id, c.Account, c.Name, c.Address, c.City, c.Zip }, categories = Shop.Categories, protection = Shop.Protection.Keys,
            products = await db.ShopProducts.Where(p => p.Active).OrderBy(p => p.Position).ToListAsync(),
            methods = await db.PaymentMethods.Where(m => m.CustomerId == customerId && m.RemovedAt == null).OrderByDescending(m => m.Autopay).Select(m => new { m.Id, m.Type, m.Last4, m.Nickname }).ToListAsync(),
        });
    }

    public record CartInput(Dictionary<string, int> Qty);

    [HttpPost("order-for/{customerId:int}/summary")]
    public async Task<object> Summary(int customerId, CartInput input) => await Shop.SummarizeAsync(db, input.Qty.Where(q => q.Value > 0).ToDictionary());

    public record PlaceInput(Dictionary<string, int> Qty, string PayWith, int? Method, string Ship, string? Street, string? City, string? Zip, bool Agree);

    [HttpPost("order-for/{customerId:int}")]
    public async Task<IActionResult> Place(int customerId, PlaceInput input)
    {
        var c = await db.Customers.FindAsync(customerId);
        if (c is null) return NotFound();
        if (!input.Agree) return BadRequest(new { error = "Read the order terms to the customer and check that they agreed." });
        if (input.PayWith is not ("card" or "bill")) return BadRequest(new { error = "Pick how they're paying." });
        if (input.PayWith == "card" && !await db.PaymentMethods.AnyAsync(m => m.Id == input.Method && m.CustomerId == customerId && m.RemovedAt == null))
            return BadRequest(new { error = "Pick the card to pay with." });
        if (input.Ship == "other" && (string.IsNullOrWhiteSpace(input.Street) || string.IsNullOrWhiteSpace(input.City) || input.Zip?.Length != 5))
            return BadRequest(new { error = "Enter the street, city and 5-digit zip to ship to." });
        var summary = await Shop.SummarizeAsync(db, input.Qty.Where(q => q.Value > 0).ToDictionary());
        if (summary.Lines.Count == 0) return BadRequest(new { error = "Pick at least one product." });
        if (summary.Unavailable.Count > 0) return BadRequest(new { error = $"{string.Join(", ", summary.Unavailable)} is out of stock." });
        var me = (await Permissions.CurrentAsync(HttpContext))!;
        var order = await Shop.PlaceAsync(db, c, summary, input.PayWith, input.Method,
            input.Ship == "other" ? Shop.ShipTo(c, input.Street, input.City, input.Zip) : Shop.ShipTo(c), me.Name);
        return Ok(new { order.Id, message = $"Order {order.Number} placed for {c.Name}." });
    }
}
