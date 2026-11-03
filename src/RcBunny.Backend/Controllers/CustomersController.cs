using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Corral: customer search, ESIID lookup and the account page.</summary>
[ApiController]
[Route("api/customers")]
[RequirePermission("corral")]
public class CustomersController(AppDbContext db) : ControllerBase
{
    /// <summary>Search by account number, name, email, phone, address or ESIID. 25 per page.</summary>
    [HttpGet]
    public async Task<object> Search(string? q, string? type, string? status, int page = 1)
    {
        var query = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Account, like) || EF.Functions.Like(c.Name, like) || EF.Functions.Like(c.Email!, like)
                || EF.Functions.Like(c.Phone!, like) || EF.Functions.Like(c.Address, like) || EF.Functions.Like(c.Esiid!, like));
        }
        if (!string.IsNullOrEmpty(type)) query = query.Where(c => c.Type == type);
        if (!string.IsNullOrEmpty(status)) query = query.Where(c => c.Status == status);

        var total = await query.CountAsync();
        var rows = await query.OrderBy(c => c.Name).Skip((Math.Max(page, 1) - 1) * 25).Take(25)
            .Select(c => new { c.Id, c.Account, c.Name, c.Type, c.City, c.Status, c.Balance })
            .ToListAsync();
        var statuses = await db.Customers.Select(c => c.Status).Distinct().OrderBy(s => s).ToListAsync();
        return new { total, page, rows, statuses };
    }

    /// <summary>ESIID Lookup: who has this meter, and what's been sent to ERCOT for it.</summary>
    [HttpGet("esiid/{esiid}")]
    public async Task<object> Esiid(string esiid)
    {
        esiid = esiid.Trim();
        return new
        {
            customers = await db.Customers.Where(c => c.Esiid == esiid).Select(c => new { c.Id, c.Account, c.Name, c.Address, c.City, c.Status }).ToListAsync(),
            transactions = await db.ErcotTransactions.Where(t => t.Esiid == esiid).OrderByDescending(t => t.SentAt).ToListAsync(),
        };
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var customer = await db.Customers.Include(c => c.Plan).Include(c => c.Market).FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null) return NotFound();
        return Ok(new
        {
            customer.Id, customer.Account, customer.Name, customer.Type, customer.Email, customer.Phone, customer.Address, customer.City, customer.Zip,
            customer.Esiid, customer.Status, customer.Balance, customer.DueDate, customer.Autopay, customer.CreatedAt,
            plan = customer.Plan?.Name, market = customer.Market?.Name,
            bills = await db.Bills.Where(b => b.CustomerId == id).OrderByDescending(b => b.BilledOn).ToListAsync(),
            payments = await db.Payments.Where(p => p.CustomerId == id).OrderByDescending(p => p.PaidOn).ToListAsync(),
            notes = await db.Notes.Where(n => n.CustomerId == id).OrderByDescending(n => n.CreatedAt).ToListAsync(),
            texts = await db.TextMessages.Where(t => t.CustomerId == id).OrderByDescending(t => t.CreatedAt).ToListAsync(),
            calls = await db.PhoneCalls.Where(c => c.CustomerId == id).OrderByDescending(c => c.CreatedAt).ToListAsync(),
            ercot = await db.ErcotTransactions.Where(t => t.CustomerId == id).OrderByDescending(t => t.SentAt).ToListAsync(),
            workItems = await db.WorkItems.Where(w => w.CustomerId == id).OrderByDescending(w => w.CreatedAt).ToListAsync(),
            productOrders = await db.ProductOrders.Include(o => o.Product).Where(o => o.CustomerId == id).OrderByDescending(o => o.CreatedAt)
                .Select(o => new { o.Id, o.Number, product = o.Product!.Name, o.Quantity, o.Total, o.Status, o.CreatedAt }).ToListAsync(),
        });
    }

    public record AccountUpdate(string? Email, string? Phone, string Status, string? Esiid);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, AccountUpdate update)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null) return NotFound();
        customer.Email = update.Email?.Trim();
        customer.Phone = update.Phone?.Trim();
        customer.Status = update.Status.Trim();
        customer.Esiid = string.IsNullOrWhiteSpace(update.Esiid) ? null : update.Esiid.Trim();
        await AddNote(id, "Updated the account details.");
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

    /// <summary>Reverse a payment: needs the "refunds" permission on top of "corral".</summary>
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
