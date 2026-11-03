using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

[ApiController]
[Route("api/customers")]
[RequirePermission("customers")]
public class CustomersController(AppDbContext db) : ControllerBase
{
    /// <summary>Search by account number, name, email, phone or address. 25 per page.</summary>
    [HttpGet]
    public async Task<object> Search(string? q, int page = 1)
    {
        var query = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Account, like) || EF.Functions.Like(c.Name, like) || EF.Functions.Like(c.Email!, like)
                || EF.Functions.Like(c.Phone!, like) || EF.Functions.Like(c.Address, like));
        }
        var total = await query.CountAsync();
        var rows = await query.OrderBy(c => c.Name).Skip((Math.Max(page, 1) - 1) * 25).Take(25)
            .Select(c => new { c.Id, c.Account, c.Name, c.City, c.Status, c.Balance })
            .ToListAsync();
        return new { total, page, rows };
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var customer = await db.Customers.Include(c => c.Plan).Include(c => c.Market).FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null) return NotFound();
        return Ok(new
        {
            customer.Id, customer.Account, customer.Name, customer.Email, customer.Phone, customer.Address, customer.City, customer.Zip,
            customer.Status, customer.Balance, customer.DueDate, customer.Autopay, customer.CreatedAt,
            plan = customer.Plan?.Name, market = customer.Market?.Name,
            bills = await db.Bills.Where(b => b.CustomerId == id).OrderByDescending(b => b.BilledOn).ToListAsync(),
            payments = await db.Payments.Where(p => p.CustomerId == id).OrderByDescending(p => p.PaidOn).ToListAsync(),
            notes = await db.Notes.Where(n => n.CustomerId == id).OrderByDescending(n => n.CreatedAt).ToListAsync(),
        });
    }

    public record ContactUpdate(string? Email, string? Phone, string Status);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ContactUpdate update)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null) return NotFound();
        customer.Email = update.Email?.Trim();
        customer.Phone = update.Phone?.Trim();
        customer.Status = update.Status.Trim();
        await AddNote(id, "Updated contact details / status.");
        return Ok();
    }

    public record NewNote(string Body);

    [HttpPost("{id:int}/notes")]
    public async Task<IActionResult> AddNote(int id, NewNote note)
    {
        if (string.IsNullOrWhiteSpace(note.Body)) return BadRequest(new { error = "Write a note first." });
        await AddNote(id, note.Body.Trim());
        return Ok();
    }

    /// <summary>Reverse a payment: needs the "refunds" permission on top of "customers".</summary>
    [HttpPost("payments/{paymentId:int}/reverse")]
    [RequirePermission("refunds")]
    public async Task<IActionResult> Reverse(int paymentId)
    {
        var payment = await db.Payments.FindAsync(paymentId);
        if (payment is null) return NotFound();
        if (payment.Status == "Reversed") return BadRequest(new { error = "That payment is already reversed." });
        payment.Status = "Reversed";
        var customer = await db.Customers.FindAsync(payment.CustomerId);
        customer!.Balance += payment.Amount;
        await AddNote(customer.Id, $"Reversed the {payment.Amount:C} payment from {payment.PaidOn:MMM d, yyyy}.");
        return Ok();
    }

    private async Task AddNote(int customerId, string body)
    {
        var me = await Permissions.CurrentAsync(HttpContext);
        db.Notes.Add(new Note { CustomerId = customerId, Author = me?.Name ?? "", Body = body });
        await db.SaveChangesAsync();
    }
}
