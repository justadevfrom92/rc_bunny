using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Corral → Products and Product Orders.</summary>
[ApiController]
[Route("api/products")]
[RequirePermission("corral")]
public class ProductsController(AppDbContext db) : ControllerBase
{
    public static readonly string[] OrderStatuses = ["New", "Shipped", "Delivered", "Cancelled"];

    [HttpGet]
    public async Task<List<Product>> List() => await db.Products.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();

    public record ProductInput(string Sku, string Name, string Category, string? Description, decimal Price, int Stock, bool Active);

    [HttpPost]
    public async Task<IActionResult> Add(ProductInput input) => await Save(new Product(), input, isNew: true);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductInput input) =>
        await db.Products.FindAsync(id) is { } product ? await Save(product, input, isNew: false) : NotFound();

    private async Task<IActionResult> Save(Product product, ProductInput input, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(input.Sku) || string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Enter a SKU and a name." });
        if (input.Price < 0 || input.Stock < 0) return BadRequest(new { error = "Price and stock can't be negative." });
        var sku = input.Sku.Trim().ToUpper();
        if (await db.Products.AnyAsync(p => p.Sku == sku && p.Id != product.Id)) return BadRequest(new { error = "Another product has that SKU." });
        product.Sku = sku; product.Name = input.Name.Trim(); product.Category = input.Category.Trim(); product.Description = input.Description?.Trim();
        product.Price = input.Price; product.Stock = input.Stock; product.Active = input.Active;
        if (isNew) db.Products.Add(product);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("orders")]
    public async Task<object> Orders(string? status) => new
    {
        statuses = OrderStatuses,
        orders = await db.ProductOrders.Where(o => status == null || o.Status == status).OrderByDescending(o => o.CreatedAt)
            .Select(o => new { o.Id, o.Number, o.CustomerId, customer = o.Customer!.Name, account = o.Customer.Account, product = o.Product!.Name, o.Quantity, o.Total, o.Status, o.CreatedAt })
            .ToListAsync(),
    };

    [HttpPost("orders/{id:int}/status")]
    public async Task<IActionResult> SetOrderStatus(int id, [FromQuery] string status)
    {
        if (!OrderStatuses.Contains(status)) return BadRequest(new { error = "Pick a status." });
        var order = await db.ProductOrders.FindAsync(id);
        if (order is null) return NotFound();
        order.Status = status;
        await db.SaveChangesAsync();
        return Ok();
    }
}
