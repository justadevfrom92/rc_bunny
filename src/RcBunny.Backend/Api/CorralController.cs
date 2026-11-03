using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RcBunny.Backend.Access;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Api;

/// <summary>Corral: customer search, the account page, notes, status, bookmarks, payment reversal.</summary>
[Route("api/corral"), RequirePerm("corral")]
public class CorralController(AppDbContext db, HybridCache cache) : ApiControllerBase
{
    // Cache tags. Any write to a customer removes "customers" (lists/stats) and that customer's own tag.
    public const string ListTag = "customers";
    public static string CustomerTag(int id) => $"customer:{id}";

    private static readonly HybridCacheEntryOptions ListCache = new() { Expiration = TimeSpan.FromSeconds(60), LocalCacheExpiration = TimeSpan.FromSeconds(30) };
    private static readonly HybridCacheEntryOptions DetailCache = new() { Expiration = TimeSpan.FromMinutes(10), LocalCacheExpiration = TimeSpan.FromMinutes(2) };

    public record SearchQuery(string? Q, string? Type, string? Status, string? Exception, int Page = 1, int Limit = 25);

    public record CustomerRow(int Id, string Account, string Name, string Type, string? Phone, string? Email, string Address,
        string City, string Zip, string? Esiid, string? Market, string? Plan, string? PlanCode, string Status, string? Exception, decimal Balance, DateTime CreatedAt);

    public record SearchResult(IReadOnlyList<CustomerRow> Rows, int Total, int Page, int Limit);

    [HttpGet("customers")]
    public async Task<IActionResult> Search([FromQuery] SearchQuery query, CancellationToken ct)
    {
        var q = query;
        var limit = q.Limit is 10 or 25 or 50 or 100 ? q.Limit : 25;
        var page = Math.Max(1, q.Page);
        var key = $"corral:search:{q.Q?.Trim().ToLowerInvariant()}|{q.Type}|{q.Status}|{q.Exception}|{page}|{limit}";

        var result = await cache.GetOrCreateAsync(key, async token =>
        {
            var query = Filter(db.Customers, q);
            var total = await query.CountAsync(token);
            var rows = await query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
                .Skip((page - 1) * limit).Take(limit).Select(Row).ToListAsync(token);
            return new SearchResult(rows, total, page, limit);
        }, ListCache, [ListTag], ct);

        return Ok(result);
    }

    private static readonly System.Linq.Expressions.Expression<Func<Customer, CustomerRow>> Row = c => new CustomerRow(
        c.Id, c.Account, c.Name, c.Type, c.Phone, c.Email, c.Address, c.City, c.Zip, c.Esiid,
        c.Market != null ? c.Market.Name : null, c.Plan != null ? c.Plan.Name : null, c.Plan != null ? c.Plan.Internal : null,
        c.Status, c.Exception, c.Balance, c.CreatedAt);

    private static IQueryable<Customer> Filter(IQueryable<Customer> query, SearchQuery f)
    {
        if (!string.IsNullOrWhiteSpace(f.Type)) query = query.Where(c => c.Type == f.Type);
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(c => c.Status == f.Status);
        if (f.Exception == "*") query = query.Where(c => c.Exception != null);
        else if (!string.IsNullOrWhiteSpace(f.Exception)) query = query.Where(c => c.Exception == f.Exception);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var s = f.Q.Trim();
            // LIKE is case-insensitive on SQL Server (default collation) and on SQLite; escape the user's wildcards.
            var like = "%" + s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var digits = new string(s.Where(char.IsDigit).ToArray());
            var phoneLike = "%" + digits + "%";
            var byPhone = digits.Length >= 4;
            query = query.Where(c =>
                EF.Functions.Like(c.Name, like, "\\") || EF.Functions.Like(c.Email!, like, "\\") || EF.Functions.Like(c.Account, like, "\\") ||
                EF.Functions.Like(c.Ticket, like, "\\") || EF.Functions.Like(c.Esiid!, like, "\\") || EF.Functions.Like(c.Address, like, "\\") ||
                EF.Functions.Like(c.City, like, "\\") || EF.Functions.Like(c.Zip, like, "\\") ||
                (byPhone && EF.Functions.Like(c.Phone!.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", ""), phoneLike)));
        }
        return query;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) => Ok(await cache.GetOrCreateAsync("corral:stats", async token => new
    {
        accounts = await db.Customers.CountAsync(token),
        onFlow = await db.Customers.CountAsync(c => c.Status == "Good - On Flow", token),
        pending = await db.Customers.CountAsync(c => c.Status.StartsWith("Pending") || c.Status == "Submitted", token),
        exceptions = await db.Customers.CountAsync(c => c.Exception != null, token),
    }, ListCache, [ListTag], ct));

    [HttpGet("bookmarks")]
    public async Task<IActionResult> Bookmarks(CancellationToken ct) => Ok(await cache.GetOrCreateAsync($"corral:bookmarks:{Me.UserId}", async token =>
        await db.Bookmarks.Where(b => b.UserId == Me.UserId).OrderByDescending(b => b.CreatedAt)
            .Select(b => b.Customer!).Select(Row).ToListAsync(token),
        ListCache, [ListTag, $"bookmarks:{Me.UserId}"], ct));

    [HttpGet("customers/{id:int}")]
    public async Task<IActionResult> Show(int id, CancellationToken ct)
    {
        var detail = await cache.GetOrCreateAsync($"corral:customer:{id}", async token => await LoadDetail(id, token), DetailCache, [CustomerTag(id)], ct);
        if (detail is null) return NotFound();
        var bookmarked = await db.Bookmarks.AnyAsync(b => b.UserId == Me.UserId && b.CustomerId == id, ct);
        // Rights are added per request (not cached with the data), so two people never share an answer to "may I?".
        return Ok(new { customer = detail, bookmarked, can = new { reverse = Can("refunds") } });
    }

    private async Task<object?> LoadDetail(int id, CancellationToken ct)
    {
        var c = await db.Customers.Include(x => x.Plan).Include(x => x.Market).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        var payments = await db.Payments.Where(p => p.CustomerId == id).OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id)
            .Select(p => new { p.Id, p.Reference, p.PaidOn, p.Amount, p.Method, p.Source, p.Status, p.Kind, p.Confirmation, p.ReversedAt }).ToListAsync(ct);
        var bills = await db.Bills.Where(b => b.CustomerId == id).OrderByDescending(b => b.BilledOn)
            .Select(b => new { b.Id, b.Reference, b.BilledOn, b.DueOn, b.Kwh, b.Amount, b.Invoice }).ToListAsync(ct);
        var notes = await db.Notes.Where(n => n.CustomerId == id).OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Select(n => new { n.Id, n.Author, n.Body, n.Category, n.Action, n.Priority, n.Disposition, n.CreatedAt }).ToListAsync(ct);
        return new
        {
            c.Id, c.Account, c.Ticket, c.Name, c.FirstName, c.LastName, c.Type, c.Phone, c.Email, c.Address, c.City, c.Zip, c.Esiid,
            market = c.Market == null ? null : new { c.Market.Name, c.Market.Description },
            plan = c.Plan == null ? null : new { c.Plan.Id, c.Plan.Name, c.Plan.Internal, c.Plan.Term },
            c.Status, c.Exception, c.Source, c.EnrollmentType, c.StartDate, c.DueDate, c.Balance, c.DepositDue,
            c.Autopay, c.Paperless, c.PeakPerks, c.MarketingOptIn, c.Stars, c.Language, c.CreatedAt,
            payments, bills, notes,
        };
    }

    [HttpPost("customers/{id:int}/bookmark")]
    public async Task<IActionResult> ToggleBookmark(int id)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == id)) return NotFound();
        var existing = await db.Bookmarks.AsTracking().FirstOrDefaultAsync(b => b.UserId == Me.UserId && b.CustomerId == id);
        if (existing is null) db.Bookmarks.Add(new Bookmark { UserId = Me.UserId, CustomerId = id });
        else db.Bookmarks.Remove(existing);
        await db.SaveChangesAsync();
        await cache.RemoveByTagAsync($"bookmarks:{Me.UserId}");
        return Ok(new { bookmarked = existing is null });
    }

    public record NoteRequest(string Body, string? Category, string? Action, string? Priority);

    [HttpPost("customers/{id:int}/notes")]
    public async Task<IActionResult> AddNote(int id, NoteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Body) || req.Body.Length > 5000) return Problem422("Write a note (up to 5,000 characters).", "body");
        if (req.Category is not null && !CorralConfig.NoteCategories.ContainsKey(req.Category)) return Problem422("Pick a category from the list.", "category");
        if (req.Action is not null && (req.Category is null || !CorralConfig.NoteCategories[req.Category].Contains(req.Action))) return Problem422("Pick an action for that category.", "action");
        if (req.Priority is not null && !CorralConfig.Priorities.Contains(req.Priority)) return Problem422("Pick a priority.", "priority");
        if (!await db.Customers.AnyAsync(c => c.Id == id)) return NotFound();

        db.Notes.Add(new Note
        {
            CustomerId = id, UserId = Me.UserId, Author = Me.Name, Body = req.Body.Trim(),
            Category = req.Category, Action = req.Action, Priority = req.Priority, Disposition = req.Action,
        });
        await db.SaveChangesAsync();
        await cache.RemoveByTagAsync(CustomerTag(id));
        return Ok(new { ok = true });
    }

    public record StatusRequest(string Status);

    [HttpPut("customers/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, StatusRequest req)
    {
        if (!CorralConfig.Statuses.ContainsKey(req.Status)) return Problem422("Pick a status from the list.", "status");
        var c = await db.Customers.AsTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (c.Status != req.Status)
        {
            db.Notes.Add(new Note { CustomerId = id, UserId = Me.UserId, Author = Me.Name, Body = $"Status changed from {c.Status} to {req.Status}." });
            c.Status = req.Status;
            await db.SaveChangesAsync();
            await cache.RemoveByTagAsync([CustomerTag(id), ListTag]);
        }
        return Ok(new { status = c.Status });
    }

    /// <summary>Needs the "refunds" right. Marks the payment reversed and puts the amount back on the balance.</summary>
    [HttpPost("payments/{id:int}/reverse"), RequirePerm("refunds")]
    public async Task<IActionResult> ReversePayment(int id)
    {
        var p = await db.Payments.AsTracking().Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        if (p.Status != "Success") return Problem422("Only successful payments can be reversed.");

        // One SaveChanges = one transaction: the reversal, the balance and the note land together or not at all.
        p.Status = "Reversed";
        p.ReversedAt = DateTime.UtcNow;
        p.ReversedBy = Me.UserId;
        p.Customer!.Balance += p.Amount;
        db.Notes.Add(new Note
        {
            CustomerId = p.CustomerId, UserId = Me.UserId, Author = Me.Name, Disposition = "Payment arrangement",
            Body = $"Payment {p.Reference} for ${p.Amount:N2} reversed.",
        });
        Audit(db, "payment_reversed", p.Customer.Account, $"{p.Reference} ${p.Amount:N2}");
        await db.SaveChangesAsync();
        await cache.RemoveByTagAsync([CustomerTag(p.CustomerId), ListTag]);
        return Ok(new { ok = true });
    }

    [HttpGet("esiid")]
    public async Task<IActionResult> Esiid([FromQuery] string esiid, CancellationToken ct)
    {
        var s = new string((esiid ?? "").Where(char.IsDigit).ToArray());
        if (s.Length < 6) return Problem422("Enter at least 6 digits of the ESIID.", "esiid");
        var rows = await db.Customers.Where(c => c.Esiid != null && c.Esiid.Contains(s)).OrderBy(c => c.Esiid).Take(50).Select(Row).ToListAsync(ct);
        return Ok(rows);
    }
}
