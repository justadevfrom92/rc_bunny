using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// Caboose (finance). Anyone with the caboose permission can record payments and ask for credits, debits and refunds.
/// Approving them needs the "refunds" permission too, and nobody can approve their own request.
/// </summary>
[ApiController]
[Route("api/caboose")]
[RequirePermission("caboose")]
public class CabooseController(AppDbContext db) : ControllerBase
{
    public static readonly string[] AgingBuckets = ["Current", "1–30 days", "31–60 days", "61–90 days", "Over 90 days"];

    // ---- Dashboard --------------------------------------------------------------------------------------------

    [HttpGet("dashboard")]
    public async Task<object> Dashboard()
    {
        var since = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var paid = await db.Payments.Where(p => p.Status == "Success" && p.PaidOn >= since).ToListAsync();
        return new
        {
            paid30 = paid.Sum(p => p.Amount),
            payments30 = paid.Count,
            byMethod = paid.GroupBy(p => p.Method.StartsWith("Card") ? "Card" : p.Method).Select(g => new { method = g.Key, total = g.Sum(p => p.Amount), count = g.Count() }),
            pendingLedger = await db.LedgerEntries.CountAsync(e => e.Status == "pending"),
            openRefunds = await db.Refunds.CountAsync(r => r.Status == "requested" || r.Status == "approved"),
            depositsDue = (await db.Customers.Where(c => c.DepositDue > 0).Select(c => c.DepositDue).ToListAsync()).Sum(),
            depositsHeld = (await db.Customers.Where(c => c.DepositHeld > 0).Select(c => c.DepositHeld).ToListAsync()).Sum(),
            aging = (await Aging()).summary,
        };
    }

    // ---- Payments ---------------------------------------------------------------------------------------------

    [HttpGet("payments")]
    public async Task<object> Payments(DateOnly? start, DateOnly? end, string? status, string? account)
    {
        var from = start ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var to = end ?? DateOnly.FromDateTime(DateTime.Today);
        var query = db.Payments.Where(p => p.PaidOn >= from && p.PaidOn <= to);
        if (!string.IsNullOrEmpty(status)) query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(account)) query = query.Where(p => db.Customers.Any(c => c.Id == p.CustomerId && c.Account == account.Trim()));
        var rows = await query.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id).Take(500)
            .Join(db.Customers, p => p.CustomerId, c => c.Id, (p, c) => new { p.Id, p.PaidOn, p.Amount, p.Method, p.Kind, p.Source, p.Status, customerId = c.Id, c.Account, customer = c.Name })
            .ToListAsync();
        return new { start = from, end = to, rows, total = rows.Where(r => r.Status == "Success").Sum(r => r.Amount) };
    }

    public record NewPayment(string Account, decimal Amount, string Method, string Kind);

    /// <summary>Record a payment taken by phone, mail or in person. A deposit payment goes toward the deposit, not the balance.</summary>
    [HttpPost("payments")]
    public async Task<IActionResult> RecordPayment(NewPayment input)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Account == input.Account.Trim());
        if (customer is null) return BadRequest(new { error = "No account with that number." });
        if (input.Amount is <= 0 or > 10000) return BadRequest(new { error = "Enter an amount between $0.01 and $10,000." });
        var deposit = input.Kind == "Deposit";
        db.Payments.Add(new Payment
        {
            CustomerId = customer.Id, PaidOn = DateOnly.FromDateTime(DateTime.Today), Amount = input.Amount,
            Method = string.IsNullOrWhiteSpace(input.Method) ? "Check" : input.Method.Trim(), Kind = deposit ? "Deposit" : "Balance Payment", Source = "Finance",
        });
        if (deposit) { customer.DepositHeld += input.Amount; customer.DepositDue = Math.Max(0, customer.DepositDue - input.Amount); }
        else customer.Balance -= input.Amount;
        await Note(customer.Id, $"Finance recorded a {input.Amount:C} {(deposit ? "deposit" : "payment")} ({input.Method}).");
        return Ok();
    }

    // ---- Credits & Debits -------------------------------------------------------------------------------------

    [HttpGet("ledger")]
    public async Task<object> Ledger() => new
    {
        pending = await LedgerRows(db.LedgerEntries.Where(e => e.Status == "pending").OrderBy(e => e.CreatedAt)),
        recent = await LedgerRows(db.LedgerEntries.Where(e => e.Status != "pending").OrderByDescending(e => e.DecidedAt).Take(25)),
    };

    private Task<List<object>> LedgerRows(IQueryable<LedgerEntry> query) => query
        .Select(e => (object)new { e.Id, e.CustomerId, account = e.Customer!.Account, customer = e.Customer.Name, e.Kind, e.Amount, e.Description, e.Status, e.RequestedBy, e.DecidedBy, e.DecidedAt, e.DecisionNote, e.CreatedAt })
        .ToListAsync();

    public record NewLedgerEntry(string Account, string Kind, decimal Amount, string Reason);

    [HttpPost("ledger")]
    public async Task<IActionResult> RequestLedger(NewLedgerEntry input)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Account == input.Account.Trim());
        if (customer is null) return BadRequest(new { error = "No account with that number." });
        if (input.Amount is <= 0 or > 5000) return BadRequest(new { error = "Enter an amount between $0.01 and $5,000." });
        if (string.IsNullOrWhiteSpace(input.Reason)) return BadRequest(new { error = "Say why." });
        db.LedgerEntries.Add(new LedgerEntry { CustomerId = customer.Id, Kind = input.Kind == "debit" ? "debit" : "credit", Amount = input.Amount, Description = input.Reason.Trim(), RequestedBy = await MyName() });
        await db.SaveChangesAsync();
        return Ok();
    }

    public record Decision(string Do, string? Note, string? CheckNumber);

    /// <summary>Apply (changes the balance) or reject a credit or debit.</summary>
    [HttpPost("ledger/{id:int}/decide")]
    [RequirePermission("refunds")]
    public async Task<IActionResult> DecideLedger(int id, Decision decision)
    {
        var entry = await db.LedgerEntries.Include(e => e.Customer).FirstOrDefaultAsync(e => e.Id == id);
        if (entry is null) return NotFound();
        if (entry.Status != "pending") return BadRequest(new { error = "That's already been decided." });
        var me = await MyName();
        if (entry.RequestedBy == me) return BadRequest(new { error = "Someone else has to approve your own request." });
        var apply = decision.Do == "apply";
        entry.Status = apply ? "applied" : "rejected";
        entry.DecidedBy = me; entry.DecidedAt = DateTime.UtcNow; entry.DecisionNote = decision.Note?.Trim();
        if (apply) entry.Customer!.Balance += entry.Kind == "debit" ? entry.Amount : -entry.Amount;
        await Note(entry.CustomerId, $"{(entry.Kind == "credit" ? "Credit" : "Debit")} of {entry.Amount:C} {(apply ? "applied" : "rejected")}: {entry.Description}");
        return Ok();
    }

    // ---- Refunds ----------------------------------------------------------------------------------------------

    [HttpGet("refunds")]
    public async Task<object> Refunds() => new
    {
        open = await RefundRows(db.Refunds.Where(r => r.Status == "requested" || r.Status == "approved").OrderBy(r => r.CreatedAt)),
        done = await RefundRows(db.Refunds.Where(r => r.Status == "paid" || r.Status == "rejected").OrderByDescending(r => r.DecidedAt).Take(25)),
        // Accounts with a credit balance and no refund on the way: candidates for a refund
        credits = await db.Customers.Where(c => c.Balance < 0 && !db.Refunds.Any(r => r.CustomerId == c.Id && (r.Status == "requested" || r.Status == "approved")))
            .OrderBy(c => c.Balance).Take(25).Select(c => new { c.Id, c.Account, c.Name, c.Balance, c.Status }).ToListAsync(),
    };

    private Task<List<object>> RefundRows(IQueryable<Refund> query) => query
        .Select(r => (object)new { r.Id, r.CustomerId, account = r.Customer!.Account, customer = r.Customer.Name, r.Kind, r.Amount, r.Reason, r.Status, r.RequestedBy, r.DecidedBy, r.DecisionNote, r.CheckNumber, r.PaidAt, r.CreatedAt })
        .ToListAsync();

    public record NewRefund(string Account, string Kind, decimal Amount, string Reason);

    /// <summary>Ask for a refund. It can't be more than the credit balance (or the deposit held).</summary>
    [HttpPost("refunds")]
    public async Task<IActionResult> RequestRefund(NewRefund input)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Account == input.Account.Trim());
        if (customer is null) return BadRequest(new { error = "No account with that number." });
        var deposit = input.Kind == "deposit";
        var limit = deposit ? customer.DepositHeld : Math.Max(0, -customer.Balance);
        if (input.Amount <= 0 || input.Amount > limit)
            return BadRequest(new { error = $"The most that can be refunded is {limit:C} ({(deposit ? "deposit held" : "credit balance")})." });
        db.Refunds.Add(new Refund { CustomerId = customer.Id, Kind = deposit ? "deposit" : "balance", Amount = input.Amount, Reason = input.Reason?.Trim() ?? "", RequestedBy = await MyName() });
        await Note(customer.Id, $"Refund of {input.Amount:C} requested ({(deposit ? "deposit" : "credit balance")}).");
        return Ok();
    }

    /// <summary>approve / reject a requested refund, or mark an approved one paid (that's when the money leaves the account).</summary>
    [HttpPost("refunds/{id:int}/decide")]
    [RequirePermission("refunds")]
    public async Task<IActionResult> DecideRefund(int id, Decision decision)
    {
        var refund = await db.Refunds.Include(r => r.Customer).FirstOrDefaultAsync(r => r.Id == id);
        if (refund is null) return NotFound();
        var me = await MyName();
        var customer = refund.Customer!;
        switch (decision.Do)
        {
            case "approve" or "reject" when refund.Status == "requested":
                if (refund.RequestedBy == me) return BadRequest(new { error = "Someone else has to approve your own request." });
                refund.Status = decision.Do == "approve" ? "approved" : "rejected";
                refund.DecidedBy = me; refund.DecidedAt = DateTime.UtcNow; refund.DecisionNote = decision.Note?.Trim();
                await Note(customer.Id, $"Refund of {refund.Amount:C} {refund.Status}.");
                break;
            case "paid" when refund.Status == "approved":
                refund.Status = "paid"; refund.PaidAt = DateTime.UtcNow; refund.CheckNumber = decision.CheckNumber?.Trim();
                if (refund.Kind == "deposit") customer.DepositHeld = Math.Max(0, customer.DepositHeld - refund.Amount);
                else customer.Balance += refund.Amount;
                await Note(customer.Id, $"Refund of {refund.Amount:C} paid{(refund.CheckNumber is { Length: > 0 } n ? $", check {n}" : "")}.");
                break;
            default:
                return BadRequest(new { error = "That refund can't do that right now." });
        }
        return Ok();
    }

    // ---- Deposits and aging -----------------------------------------------------------------------------------

    [HttpGet("deposits")]
    public async Task<object> Deposits() => new
    {
        due = await db.Customers.Where(c => c.DepositDue > 0).OrderBy(c => c.Name).Select(c => new { c.Id, c.Account, c.Name, c.Status, amount = c.DepositDue }).ToListAsync(),
        held = await db.Customers.Where(c => c.DepositHeld > 0).OrderBy(c => c.Name).Select(c => new { c.Id, c.Account, c.Name, c.Status, amount = c.DepositHeld }).ToListAsync(),
    };

    /// <summary>Receivables aging: who owes money, grouped by how many days past the due date.</summary>
    [HttpGet("aging")]
    public async Task<object> AgingReport()
    {
        var (accounts, summary) = await Aging();
        return new { accounts, summary };
    }

    private async Task<(List<object> accounts, List<object> summary)> Aging()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var owing = await db.Customers.Where(c => c.Balance > 0).Select(c => new { c.Id, c.Account, c.Name, c.Status, c.Balance, c.DueDate }).ToListAsync();
        var rows = owing.Select(c =>
        {
            var days = c.DueDate is { } d && d < today ? today.DayNumber - d.DayNumber : 0;
            var bucket = days switch { 0 => AgingBuckets[0], <= 30 => AgingBuckets[1], <= 60 => AgingBuckets[2], <= 90 => AgingBuckets[3], _ => AgingBuckets[4] };
            return new { c.Id, c.Account, c.Name, c.Status, c.Balance, c.DueDate, daysPastDue = days, bucket };
        }).OrderByDescending(r => r.daysPastDue).ThenByDescending(r => r.Balance).ToList();
        var summary = AgingBuckets.Select(b => (object)new { bucket = b, accounts = rows.Count(r => r.bucket == b), total = rows.Where(r => r.bucket == b).Sum(r => r.Balance) }).ToList();
        return (rows.Cast<object>().ToList(), summary);
    }

    // ---- Journal ----------------------------------------------------------------------------------------------

    public record JournalLine(DateOnly Date, string Debit, string Credit, decimal Amount, string Memo);

    /// <summary>The double-entry journal for the accounting system, for a date range. ?format=csv downloads it.</summary>
    [HttpGet("journal")]
    public async Task<IActionResult> Journal(DateOnly? start, DateOnly? end, string? format)
    {
        var from = start ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        var to = end ?? DateOnly.FromDateTime(DateTime.Today);
        var lines = new List<JournalLine>();

        foreach (var g in (await db.Payments.Where(p => p.Status == "Success" && p.PaidOn >= from && p.PaidOn <= to).ToListAsync()).GroupBy(p => (p.PaidOn, p.Kind)))
            lines.Add(g.Key.Kind == "Deposit"
                ? new(g.Key.PaidOn, "Cash", "Customer Deposits", g.Sum(p => p.Amount), $"{g.Count()} deposits received")
                : new(g.Key.PaidOn, "Cash", "Accounts Receivable", g.Sum(p => p.Amount), $"{g.Count()} payments received"));

        var fromTime = from.ToDateTime(TimeOnly.MinValue);
        var toTime = to.ToDateTime(TimeOnly.MaxValue);
        foreach (var g in (await db.LedgerEntries.Where(e => e.Status == "applied" && e.DecidedAt >= fromTime && e.DecidedAt <= toTime).ToListAsync()).GroupBy(e => (Date: DateOnly.FromDateTime(e.DecidedAt!.Value), e.Kind)))
            lines.Add(g.Key.Kind == "credit"
                ? new(g.Key.Date, "Bill Credits", "Accounts Receivable", g.Sum(e => e.Amount), $"{g.Count()} credits applied")
                : new(g.Key.Date, "Accounts Receivable", "Other Revenue", g.Sum(e => e.Amount), $"{g.Count()} debits applied"));

        foreach (var g in (await db.Refunds.Where(r => r.Status == "paid" && r.PaidAt >= fromTime && r.PaidAt <= toTime).ToListAsync()).GroupBy(r => (Date: DateOnly.FromDateTime(r.PaidAt!.Value), r.Kind)))
            lines.Add(g.Key.Kind == "deposit"
                ? new(g.Key.Date, "Customer Deposits", "Cash", g.Sum(r => r.Amount), $"{g.Count()} deposit refunds paid")
                : new(g.Key.Date, "Accounts Receivable", "Cash", g.Sum(r => r.Amount), $"{g.Count()} refunds paid"));

        lines = lines.OrderBy(l => l.Date).ThenBy(l => l.Debit).ToList();
        if (format != "csv") return Ok(new { start = from, end = to, lines, total = lines.Sum(l => l.Amount) });

        var csv = new StringBuilder("Date,Debit,Credit,Amount,Memo\n");
        foreach (var l in lines) csv.Append($"{l.Date:yyyy-MM-dd},{l.Debit},{l.Credit},{l.Amount:0.00},\"{l.Memo}\"\n");
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"journal-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.csv");
    }

    // ---- MSIDs and promo codes --------------------------------------------------------------------------------

    [HttpGet("channels")]
    public async Task<object> Channels() => new
    {
        msids = await db.Msids.OrderBy(m => m.Code).Select(m => new { m.Id, m.Code, m.Name, m.Channel, m.Active, customers = db.Customers.Count(c => c.Msid == m.Code) }).ToListAsync(),
        promoCodes = await db.PromoCodes.OrderBy(p => p.Code).Select(p => new { p.Id, p.Code, p.Description, p.Credit, p.StartsOn, p.EndsOn, p.Active, customers = db.Customers.Count(c => c.PromoCode == p.Code) }).ToListAsync(),
    };

    public record MsidInput(string Code, string Name, string Channel, bool Active);

    [HttpPost("msids")]
    public async Task<IActionResult> AddMsid(MsidInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Enter a code and a name." });
        if (await db.Msids.AnyAsync(m => m.Code == input.Code.Trim())) return BadRequest(new { error = "That MSID already exists." });
        db.Msids.Add(new Msid { Code = input.Code.Trim(), Name = input.Name.Trim(), Channel = input.Channel.Trim(), Active = input.Active });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("msids/{id:int}")]
    public async Task<IActionResult> UpdateMsid(int id, MsidInput input)
    {
        var msid = await db.Msids.FindAsync(id);
        if (msid is null) return NotFound();
        msid.Name = input.Name.Trim(); msid.Channel = input.Channel.Trim(); msid.Active = input.Active;
        await db.SaveChangesAsync();
        return Ok();
    }

    public record PromoInput(string Code, string Description, decimal Credit, DateOnly? StartsOn, DateOnly? EndsOn, bool Active);

    [HttpPost("promo-codes")]
    public async Task<IActionResult> AddPromo(PromoInput input)
    {
        var code = input.Code.Trim().ToUpper();
        if (code.Length == 0) return BadRequest(new { error = "Enter a code." });
        if (await db.PromoCodes.AnyAsync(p => p.Code == code)) return BadRequest(new { error = "That promo code already exists." });
        if (input.StartsOn > input.EndsOn) return BadRequest(new { error = "The end date is before the start date." });
        db.PromoCodes.Add(new PromoCode { Code = code, Description = input.Description.Trim(), Credit = Math.Max(0, input.Credit), StartsOn = input.StartsOn, EndsOn = input.EndsOn, Active = input.Active });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("promo-codes/{id:int}")]
    public async Task<IActionResult> UpdatePromo(int id, PromoInput input)
    {
        var promo = await db.PromoCodes.FindAsync(id);
        if (promo is null) return NotFound();
        if (input.StartsOn > input.EndsOn) return BadRequest(new { error = "The end date is before the start date." });
        promo.Description = input.Description.Trim(); promo.Credit = Math.Max(0, input.Credit);
        promo.StartsOn = input.StartsOn; promo.EndsOn = input.EndsOn; promo.Active = input.Active;
        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<string> MyName() => (await Permissions.CurrentAsync(HttpContext))!.Name;

    private async Task Note(int customerId, string body)
    {
        db.Notes.Add(new Note { CustomerId = customerId, Author = await MyName(), Body = body });
        await db.SaveChangesAsync();
    }
}
