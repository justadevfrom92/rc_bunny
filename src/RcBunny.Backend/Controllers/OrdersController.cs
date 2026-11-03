using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Corral → Create Order: sign up a new residential or business customer.</summary>
[ApiController]
[Route("api/orders")]
[RequirePermission("corral")]
public class OrdersController(AppDbContext db) : ControllerBase
{
    /// <summary>The plans that can be sold at a zip code, with their prices.</summary>
    [HttpGet("plans")]
    public async Task<IActionResult> Plans(string zip, string type = "Residential")
    {
        var market = await MarketForZip(zip);
        if (market is null) return BadRequest(new { error = $"We don't serve {zip}." });
        var prices = await Pricing.PricesAsync(db, market);
        return Ok(new
        {
            market = market.Name,
            plans = prices.Where(p => p.Plan.Type == (type == "Business" ? "Business" : "Residential"))
                .Select(p => new { p.Plan.Id, p.Plan.Name, p.Plan.TermMonths, price = p.PriceAt1000 }),
        });
    }

    public record NewOrder(string Type, string Name, string? Email, string? Phone, string Address, string City, string Zip, string? Esiid, int PlanId);

    [HttpPost]
    public async Task<IActionResult> Create(NewOrder order)
    {
        if (string.IsNullOrWhiteSpace(order.Name) || string.IsNullOrWhiteSpace(order.Address) || string.IsNullOrWhiteSpace(order.City))
            return BadRequest(new { error = "Enter the name and service address." });
        var market = await MarketForZip(order.Zip);
        if (market is null) return BadRequest(new { error = $"We don't serve {order.Zip}." });
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == order.PlanId && p.Active);
        if (plan is null) return BadRequest(new { error = "Pick a plan." });

        var lastAccount = await db.Customers.MaxAsync(c => c.Account);
        var me = await Permissions.CurrentAsync(HttpContext);
        var esiid = string.IsNullOrWhiteSpace(order.Esiid) ? null : order.Esiid.Trim();
        var customer = new Customer
        {
            Account = (long.Parse(lastAccount) + 1).ToString(), Name = order.Name.Trim(), Email = order.Email?.Trim(), Phone = order.Phone?.Trim(),
            Address = order.Address.Trim(), City = order.City.Trim(), Zip = order.Zip.Trim(), MarketId = market.Id, PlanId = plan.Id,
            Type = order.Type == "Business" ? "Small Business" : "Residential", Esiid = esiid, Status = "Pending Enrollment",
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        // With an ESIID the switch request goes to ERCOT; without one the account waits in the "No ESIID" queue.
        if (esiid != null) db.ErcotTransactions.Add(new ErcotTransaction { CustomerId = customer.Id, Esiid = esiid, Type = "Switch" });
        else db.WorkItems.Add(new WorkItem { CustomerId = customer.Id, Queue = "No ESIID", Summary = $"New order without an ESIID: {customer.Name}" });
        db.Notes.Add(new Note { CustomerId = customer.Id, Author = me!.Name, Body = $"Order created in Corral: {plan.Name}." });
        await db.SaveChangesAsync();
        return Ok(new { customer.Id, customer.Account });
    }

    private async Task<Market?> MarketForZip(string zip)
    {
        if (!int.TryParse(zip, out var z)) return null;
        var range = await db.ZipRanges.FirstOrDefaultAsync(r => z >= r.From && z <= r.To);
        return range is null ? null : await db.Markets.FindAsync(range.MarketId);
    }
}
