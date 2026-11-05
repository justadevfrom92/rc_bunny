using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Services;

namespace RcBunny.Controllers;

/// <summary>Corral: Customer Search and the account page, with everything you can do on it.</summary>
[ApiController]
[Route("api/customers")]
[RequirePermission("corral")]
public class CustomersController(AppDbContext db, IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    // ---- Customer Search --------------------------------------------------------------------------------------

    /// <summary>Search by name, email, account, ticket, ESIID, address, city, zip or phone digits; filter by type, status and exception.</summary>
    [HttpGet]
    public async Task<object> Search(string? q, string? type, string? status, string? exception, int limit = 25, int page = 1)
    {
        var query = db.Customers.AsQueryable();
        if (!string.IsNullOrEmpty(type)) query = query.Where(c => c.Type == type);
        if (!string.IsNullOrEmpty(status)) query = query.Where(c => c.Status == status);
        if (exception == "*") query = query.Where(c => c.Exception != null);
        else if (!string.IsNullOrEmpty(exception)) query = query.Where(c => c.Exception == exception);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            var digits = new string(q.Where(char.IsDigit).ToArray());
            var phoneLike = digits.Length >= 4 ? $"%{digits}%" : null;
            query = query.Where(c => EF.Functions.Like(c.Name, like) || EF.Functions.Like(c.Email!, like) || EF.Functions.Like(c.Account, like)
                || EF.Functions.Like(c.Ticket, like) || EF.Functions.Like(c.Esiid!, like) || EF.Functions.Like(c.Address, like)
                || EF.Functions.Like(c.City, like) || EF.Functions.Like(c.Zip, like)
                || (phoneLike != null && EF.Functions.Like(c.Phone!.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", ""), phoneLike)));
        }
        var total = await query.CountAsync();
        var size = limit is 10 or 25 or 50 or 100 ? limit : limit == 0 ? Math.Max(1, total) : 25;
        var me = await Me();
        return new
        {
            total, page, limit,
            rows = await Rows(query.OrderByDescending(c => c.CreatedAt).Skip((Math.Max(page, 1) - 1) * size).Take(size)),
            bookmarks = await Rows(db.Bookmarks.Where(b => b.UserId == me.Id).Select(b => b.Customer!)),
            stats = new
            {
                accounts = await db.Customers.CountAsync(),
                onFlow = await db.Customers.CountAsync(c => c.Status == "Good - On Flow"),
                pending = await db.Customers.CountAsync(c => c.Status.StartsWith("Pending") || c.Status == "Submitted"),
                exceptions = await db.Customers.CountAsync(c => c.Exception != null),
            },
            statuses = Corral.Statuses.Select(s => s.Status),
            exceptions = Corral.Exceptions,
        };
    }

    private static async Task<List<object>> Rows(IQueryable<Customer> q) => (await q
        .Select(c => new { c.Id, c.Account, c.Name, c.Type, c.Phone, c.Email, c.Address, c.City, c.Zip, c.Status, c.Exception, c.Balance, plan = c.Plan!.Name, market = c.Market!.Name, c.Source, c.CreatedAt })
        .ToListAsync()).Select(c => (object)new { c.Id, c.Account, c.Name, c.Type, c.Phone, c.Email, c.Address, c.City, c.Zip, c.Status, tone = Corral.StatusTone(c.Status), c.Exception, c.Balance, c.plan, c.market, c.Source, c.CreatedAt }).ToList();

    // ---- The account page -------------------------------------------------------------------------------------

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, bool allPayments = false, bool allMethods = false)
    {
        var c = await db.Customers.Include(x => x.Plan).Include(x => x.Market).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        var me = await Me();
        var bills = await db.Bills.Where(b => b.CustomerId == id).OrderByDescending(b => b.BilledOn).ToListAsync();
        var payments = await db.Payments.Include(p => p.PaymentMethod).Where(p => p.CustomerId == id).OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id).ToListAsync();
        var methods = await db.PaymentMethods.Where(m => m.CustomerId == id).OrderBy(m => m.CreatedAt).ToListAsync();
        var flags = await db.CustomerFlags.Where(f => f.CustomerId == id && f.RemovedAt == null).OrderByDescending(f => f.CreatedAt).ToListAsync();
        var products = await db.CustomerProducts.Where(p => p.CustomerId == id && p.RemovedAt == null).OrderBy(p => p.CreatedAt).ToListAsync();
        var terms = await db.PlanTerms.Include(t => t.Plan).Where(t => t.CustomerId == id).OrderByDescending(t => t.OrderedAt).ToListAsync();
        var stars = await db.StarEntries.Where(e => e.CustomerId == id).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync();
        var running = 0;
        var rewards = stars.Select(e => new { e.Id, date = e.CreatedAt, e.Reason, e.Stars, net = running += e.Stars }).Reverse().Take(12).ToList();
        var term = terms.FirstOrDefault(t => t.Status == "current") ?? terms.FirstOrDefault();
        var hasProtection = products.Any(p => p.Product.Contains("Protection") || p.Product.Contains("Warranty"));
        var methodTotals = payments.Where(p => p.Status == "Success" && p.PaymentMethodId != null).GroupBy(p => p.PaymentMethodId!.Value).ToDictionary(g => g.Key.ToString(), g => g.Sum(p => p.Amount));
        var summary = bills.Select(b => new { type = "bill", b.Id, date = b.BilledOn, billed = (decimal?)b.Amount, paid = (decimal?)null, note = b.Invoice })
            .Concat(payments.Where(p => p.Status == "Success").Select(p => new { type = "payment", p.Id, date = p.PaidOn, billed = (decimal?)null, paid = (decimal?)p.Amount, note = p.Confirmation }))
            .OrderByDescending(x => x.date).ThenBy(x => x.paid == null ? 1 : 0).ToList();
        var usage = bills.GroupBy(b => b.BilledOn.AddMonths(-1).Year).OrderByDescending(g => g.Key)
            .Select(g => new { year = g.Key, months = g.GroupBy(b => b.BilledOn.AddMonths(-1).Month).ToDictionary(m => m.Key.ToString(), m => m.Sum(b => b.Kwh)) });
        var shopOrders = await db.ShopOrders.Where(o => o.CustomerId == id).OrderByDescending(o => o.Id).Take(5).ToListAsync();

        return Ok(new
        {
            customer = c, plan = c.Plan?.Name, planTerm = c.Plan?.TermMonths, market = c.Market?.Name, statusTone = Corral.StatusTone(c.Status),
            authorizedUsers = c.AuthorizedUserList().Select(u => new { u.Name, u.Phone, u.Added }),
            bookmarked = await db.Bookmarks.AnyAsync(b => b.UserId == me.Id && b.CustomerId == id),
            banners = flags.Where(f => Corral.FlagBanners.ContainsKey(f.Flag)).Select(f => Corral.FlagBanners[f.Flag]).Distinct().Select(b => new { b.Title, b.Text }),
            crossSell = !hasProtection && c.Type == "Residential",
            flags, products,
            pending = await db.LedgerEntries.Where(e => e.CustomerId == id && e.Status == "pending").ToListAsync(),
            lastActions = await ActionNotes(id).Take(10).ToListAsync(),
            shopOrders = shopOrders.Select(o => new { o.Id, o.Number, o.CreatedAt, o.PlacedBy, items = OrderItemsText(o.Items), o.Total, o.Monthly, o.Status }),
            rewards, term = term == null ? null : new { term.OrderedAt, term.Rate2000, term.EnergyCharge, term.ContractStart, term.ContractEnd },
            files = await db.CustomerFiles.Where(f => f.CustomerId == id).OrderByDescending(f => f.CreatedAt).ToListAsync(),
            notes = await db.Notes.Where(n => n.CustomerId == id).OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToListAsync(),
            calls = await db.PhoneCalls.Include(p => p.User).Where(p => p.CustomerId == id).OrderByDescending(p => p.StartedAt)
                .Select(p => new { p.Id, p.StartedAt, p.Direction, p.Issue, p.DurationSec, answeredBy = p.User!.Name, p.AgentId, p.Resolution }).ToListAsync(),
            texts = await db.ContactLogs.Where(l => l.CustomerId == id && l.Channel == "SMS").OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).ToListAsync(),
            summary, usage, bills,
            payments = (allPayments ? payments : payments.Where(p => p.Status != "Failed")).Select(p => new
            {
                p.Id, p.Reference, p.PaidOn, p.PaidTime, p.Source, method = p.PaymentMethod == null ? "Unknown account" : p.PaymentMethod.Nickname ?? p.PaymentMethod.Type,
                last4 = p.PaymentMethod?.Last4, p.Kind, p.Status, p.Amount, p.Confirmation, p.ReversedAt,
            }),
            methods = allMethods ? methods : methods.Where(m => m.RemovedAt == null).ToList(), methodTotals,
            planTerms = terms.Select(t => new { t.Id, t.OrderedAt, t.ContractStart, t.ContractEnd, t.Status, t.Rate2000, t.EnergyCharge, plan = t.Plan?.Name, t.RateClass }),
            addresses = await db.ServiceAddresses.Where(a => a.CustomerId == id).OrderByDescending(a => a.OrderedAt).ToListAsync(),
            ercot = await db.ErcotTransactions.Where(t => t.CustomerId == id).OrderByDescending(t => t.TransDate).ThenByDescending(t => t.Id).ToListAsync(),
            queueLogs = await db.QueueLogs.Where(q => q.CustomerId == id).OrderByDescending(q => q.EnteredAt).ToListAsync(),
            warehouse = Warehouse(c, bills, payments, terms, products),
            logs = await LogCounts(id),
            options = new
            {
                statuses = Corral.Statuses.Select(s => s.Status), flags = Corral.Flags, products = Corral.Products,
                noteCategories = Corral.NoteCategories, priorities = Corral.Priorities, queues = Corral.OrderQueues,
                actions = Corral.Actions.Where(a => a.Permission is null || me.Permissions.Contains(a.Permission)).Select(a => new { a.Key, a.Label, a.Menu }),
                canRefund = me.Permissions.Contains("refunds"),
            },
        });
    }

    private static string OrderItemsText(string json) =>
        string.Join(", ", System.Text.Json.JsonDocument.Parse(json).RootElement.EnumerateArray().Select(i => $"{i.GetProperty("qty")}× {i.GetProperty("name")}"));

    /// <summary>The account as one flat reporting row (Warehouse), like ebunny's.</summary>
    private static Dictionary<string, object?> Warehouse(Customer c, List<Bill> bills, List<Payment> payments, List<PlanTerm> terms, List<CustomerProduct> products)
    {
        var current = terms.FirstOrDefault(t => t.Status == "current");
        var paid = payments.Where(p => p.Status == "Success").ToList();
        bool Has(string p) => products.Any(x => x.Product == p);
        string YN(bool b) => b ? "y" : "n";
        return new()
        {
            ["account_number"] = c.Account, ["account_status"] = c.Status, ["customer_type"] = c.Type, ["customer_name"] = c.Name,
            ["first_name"] = c.FirstName, ["last_name"] = c.LastName, ["tec_score"] = c.TecScore, ["phone_1"] = new string((c.Phone ?? "").Where(char.IsDigit).ToArray()),
            ["email"] = c.Email, ["billing_address"] = $"{c.BillingStreet ?? c.Address}, {c.BillingCity ?? c.City}, {c.BillingState ?? "TX"} {c.BillingZip ?? c.Zip}",
            ["service_address"] = $"{c.Address}, {c.City}, TX {c.Zip}", ["order_date"] = c.CreatedAt, ["requested_start_date"] = c.RequestedStart,
            ["actual_start_date"] = c.ActualStart, ["days_on_flow"] = c.ActualStart is { } s ? DateOnly.FromDateTime(DateTime.Today).DayNumber - s.DayNumber : null,
            ["market_name"] = c.Market?.Name, ["commodity"] = "e", ["uan1"] = c.Esiid, ["uan2"] = c.MeterNumber, ["uan3"] = c.LoadProfile, ["uan5"] = c.MeterType,
            ["load_zone"] = c.LoadZone, ["plan_name"] = c.Plan?.Name, ["plan_rate_class"] = c.RateClass, ["plan_energy_charge"] = current?.EnergyCharge,
            ["plan_base_charge"] = c.Plan?.MonthlyFee, ["plan_promo"] = c.PromoCode, ["plan_term"] = c.Plan?.TermMonths,
            ["plan_term_start"] = current?.ContractStart, ["plan_term_end"] = current?.ContractEnd, ["current_balance"] = c.Balance.ToString("0.00"),
            ["bills"] = bills.Count, ["bills_ontime"] = bills.Count(b => b.Ontime), ["bills_late"] = bills.Count(b => !b.Ontime),
            ["last_payment_date"] = paid.MaxBy(p => p.PaidOn)?.PaidOn, ["last_payment_amount"] = paid.MaxBy(p => p.PaidOn)?.Amount,
            ["last_bill_date"] = bills.MaxBy(b => b.BilledOn)?.BilledOn, ["avg_usage"] = bills.Count > 0 ? (int)Math.Round(bills.Average(b => b.Kwh)) : null,
            ["paperless"] = YN(c.Paperless), ["autopay"] = YN(c.Autopay), ["peak_perks"] = YN(c.PeakPerks), ["rewards"] = YN(Has("Rangler Rewards")),
            ["giddyup"] = YN(Has("Giddyup Guarantee")), ["freedom"] = YN(Has("Freedom Flex")), ["solar"] = YN(Has("Solar Buyback")),
            ["warranty"] = YN(products.Any(p => p.Product.Contains("Protection") || p.Product.Contains("Warranty"))), ["stars_balance"] = c.Stars,
            ["msid"] = c.Msid, ["channel"] = c.Channel, ["is_move"] = YN(c.MoveSwitch == "move"), ["is_switch"] = YN(c.MoveSwitch == "switch"),
            ["is_resi"] = YN(c.Type == "Residential"), ["is_web"] = YN(c.Source == "Website"), ["is_call"] = YN(c.Source == "Phone"),
            ["opt_out_marketing"] = YN(!c.MarketingOptIn), ["created"] = c.CreatedAt, ["modified"] = c.UpdatedAt,
        };
    }

    // ---- Status, bookmark, notes, marketing -------------------------------------------------------------------

    public record StatusInput(string Status);

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, StatusInput input)
    {
        if (!Corral.Statuses.Any(s => s.Status == input.Status)) return BadRequest(new { error = "Pick a status." });
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        if (c.Status != input.Status)
        {
            AddNote(id, (await Me()).Name, $"Status changed from {c.Status} to {input.Status}.");
            c.Status = input.Status;
            await db.SaveChangesAsync();
        }
        return Ok();
    }

    [HttpPost("{id:int}/bookmark")]
    public async Task<IActionResult> Bookmark(int id)
    {
        var me = await Me();
        var existing = await db.Bookmarks.FirstOrDefaultAsync(b => b.UserId == me.Id && b.CustomerId == id);
        if (existing != null) db.Bookmarks.Remove(existing); else db.Bookmarks.Add(new Bookmark { UserId = me.Id, CustomerId = id });
        await db.SaveChangesAsync();
        return Ok(new { bookmarked = existing is null });
    }

    public record NoteInput(string Body, string? Category, string? Action, string? Priority);

    [HttpPost("{id:int}/notes")]
    public async Task<IActionResult> AddNote(int id, NoteInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Body)) return BadRequest(new { error = "Write a note first." });
        var category = string.IsNullOrEmpty(input.Category) ? null : input.Category;
        if (category != null && !Corral.NoteCategories.ContainsKey(category)) return BadRequest(new { error = "Pick a category." });
        var action = string.IsNullOrEmpty(input.Action) ? null : input.Action;
        if (action != null && (category is null || !Corral.NoteCategories[category].Contains(action))) return BadRequest(new { error = "That action isn't in that category." });
        var priority = string.IsNullOrEmpty(input.Priority) ? null : input.Priority;
        if (priority != null && !Corral.Priorities.Contains(priority)) return BadRequest(new { error = "Pick a priority." });
        db.Notes.Add(new Note { CustomerId = id, Author = (await Me()).Name, Body = input.Body.Trim(), Category = category, Action = action, Priority = priority });
        await db.SaveChangesAsync();
        return Ok();
    }

    public record MarketingInput(string Channel, string Msid, string? PromoCode);

    [HttpPut("{id:int}/marketing")]
    public async Task<IActionResult> Marketing(int id, MarketingInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Channel) || string.IsNullOrWhiteSpace(input.Msid)) return BadRequest(new { error = "Enter the channel and MSID." });
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        (c.Channel, c.Msid, c.PromoCode) = (input.Channel.Trim(), input.Msid.Trim(), string.IsNullOrWhiteSpace(input.PromoCode) ? null : input.PromoCode.Trim());
        await db.SaveChangesAsync();
        return Ok();
    }

    public record ContactInput(string? Email, string? Phone, string? PhoneType, string Language);

    /// <summary>Edit the contact details (ebunny edits them from the account's data item page).</summary>
    [HttpPut("{id:int}/contact")]
    public async Task<IActionResult> Contact(int id, ContactInput input)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        (c.Email, c.Phone, c.PhoneType, c.Language) = (input.Email?.Trim(), input.Phone?.Trim(), input.PhoneType, input.Language == "Spanish" ? "Spanish" : "English");
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- Payments: reverse, payment methods, pending credits/debits -------------------------------------------

    /// <summary>Reverse a payment: needs the "refunds" permission on top of "corral".</summary>
    [HttpPost("payments/{paymentId:int}/reverse")]
    [RequirePermission("refunds")]
    public async Task<IActionResult> Reverse(int paymentId)
    {
        var payment = await db.Payments.FindAsync(paymentId);
        if (payment is null) return NotFound();
        if (payment.Status != "Success") return BadRequest(new { error = "Only successful payments can be reversed." });
        var me = await Me();
        (payment.Status, payment.ReversedAt, payment.ReversedBy) = ("Reversed", DateTime.UtcNow, me.Name);
        var c = await db.Customers.FindAsync(payment.CustomerId);
        c!.Balance += payment.Amount;
        AddNote(c.Id, me.Name, $"Payment {payment.Reference} for {payment.Amount:C} reversed.");
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("methods/{methodId:int}/autopay")]
    public async Task<IActionResult> SetAutopay(int methodId)
    {
        var method = await db.PaymentMethods.FindAsync(methodId);
        if (method is null || method.RemovedAt != null) return NotFound();
        foreach (var m in await db.PaymentMethods.Where(m => m.CustomerId == method.CustomerId && m.Id != methodId && m.Autopay).ToListAsync()) m.Autopay = false;
        method.Autopay = true;
        (await db.Customers.FindAsync(method.CustomerId))!.Autopay = true;
        await db.SaveChangesAsync();
        return Ok(new { message = $"AutoPay set to {method.Label()}." });
    }

    [HttpDelete("methods/{methodId:int}")]
    public async Task<IActionResult> RemoveMethod(int methodId)
    {
        var method = await db.PaymentMethods.FindAsync(methodId);
        if (method is null) return NotFound();
        (method.RemovedAt, method.Autopay) = (DateTime.UtcNow, false);
        await db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Delete a pending credit or debit (Balances panel).</summary>
    [HttpDelete("ledger/{entryId:int}")]
    public async Task<IActionResult> DeleteLedger(int entryId)
    {
        var entry = await db.LedgerEntries.FindAsync(entryId);
        if (entry is null) return NotFound();
        if (entry.Status != "pending") return BadRequest(new { error = "Only pending credits and debits can be deleted." });
        entry.Status = "deleted";
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- Flags and products -----------------------------------------------------------------------------------

    public record NameInput(string Name);

    [HttpPost("{id:int}/flags")]
    public async Task<IActionResult> AddFlag(int id, NameInput input)
    {
        if (!Corral.Flags.Contains(input.Name)) return BadRequest(new { error = "Pick a flag." });
        db.CustomerFlags.Add(new CustomerFlag { CustomerId = id, Flag = input.Name, Author = (await Me()).Name });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("flags/{flagId:int}")]
    public async Task<IActionResult> RemoveFlag(int flagId)
    {
        var flag = await db.CustomerFlags.FindAsync(flagId);
        if (flag is null) return NotFound();
        flag.RemovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{id:int}/products")]
    public async Task<IActionResult> AddProduct(int id, NameInput input)
    {
        if (!Corral.Products.Contains(input.Name)) return BadRequest(new { error = "Pick a product." });
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        if (!await Products.AddAsync(db, c, input.Name, (await Me()).Name)) return BadRequest(new { error = $"{input.Name} is already on the account." });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("products/{productId:int}")]
    public async Task<IActionResult> RemoveProduct(int productId)
    {
        var product = await db.CustomerProducts.Include(p => p.Customer).FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null) return NotFound();
        await Products.RemoveAsync(db, product.Customer!, product.Product);
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- Stars ------------------------------------------------------------------------------------------------

    [HttpGet("{id:int}/stars")]
    public async Task<IActionResult> Stars(int id)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        return Ok(new
        {
            c.Id, c.Account, c.Name, c.Stars,
            offers = await db.RewardOffers.Where(o => o.Active).OrderBy(o => o.Position).Select(o => new { o.Id, o.Name, o.Description, o.Stars, o.Effect }).ToListAsync(),
            // Star history, with this account's monthly drawing entries in among them
            history = (await db.StarEntries.Where(e => e.CustomerId == id).Select(e => new { record = "star_entry", e.Id, at = e.CreatedAt, e.Reason, e.Stars, drawing = false }).ToListAsync())
                .Concat((await db.DrawingEntries.Where(d => d.CustomerId == id).ToListAsync())
                    .Select(d => new { record = "drawing_entry", d.Id, at = d.CreatedAt, Reason = $"Entered the {d.Month} drawing", Stars = 0, drawing = true }))
                .OrderByDescending(e => e.at),
        });
    }

    public record RedeemInput(int OfferId);

    [HttpPost("{id:int}/stars")]
    public async Task<IActionResult> Redeem(int id, RedeemInput input)
    {
        var problem = await Rewards.RedeemAsync(db, id, input.OfferId, (await Me()).Name);
        return problem is null ? Ok() : BadRequest(new { error = problem.Replace("You have", "They have").Replace("You're", "They're") });
    }

    /// <summary>Set the star balance to the sum of the star history.</summary>
    [HttpPost("{id:int}/stars/recalculate")]
    public async Task<IActionResult> RecalculateStars(int id)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        c.Stars = await db.StarEntries.Where(e => e.CustomerId == id).SumAsync(e => e.Stars);
        await db.SaveChangesAsync();
        return Ok(new { message = $"Stars recalculated: {c.Stars:N0}" });
    }

    // ---- TECH: order-flow queues ------------------------------------------------------------------------------

    [HttpPost("{id:int}/queues")]
    public async Task<IActionResult> AddQueue(int id, NameInput input)
    {
        if (!Corral.OrderQueues.Contains(input.Name)) return BadRequest(new { error = "Pick a queue." });
        db.QueueLogs.Add(new QueueLog { CustomerId = id, Queue = input.Name, Who = (await Me()).Name });
        await db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Move an account from one queue to another, or out of it ("remove").</summary>
    [HttpPut("queue-logs/{logId:int}")]
    public async Task<IActionResult> MoveQueue(int logId, NameInput input)
    {
        var log = await db.QueueLogs.FindAsync(logId);
        if (log is null) return NotFound();
        if (input.Name != "remove" && !Corral.OrderQueues.Contains(input.Name)) return BadRequest(new { error = "Pick a queue." });
        log.ExitedAt = DateTime.UtcNow;
        if (input.Name != "remove") db.QueueLogs.Add(new QueueLog { CustomerId = log.CustomerId, Queue = input.Name, Who = (await Me()).Name });
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- Plan change log, address change log, ERCOT -----------------------------------------------------------

    public record PlanTermInput(string Do, decimal? EnergyCharge);

    /// <summary>Send a welcome packet, activate a plan, or edit its rate.</summary>
    [HttpPost("plan-terms/{termId:int}")]
    public async Task<IActionResult> PlanTermAction(int termId, PlanTermInput input)
    {
        var term = await db.PlanTerms.FindAsync(termId);
        if (term is null) return NotFound();
        var c = (await db.Customers.FindAsync(term.CustomerId))!;
        string message;
        switch (input.Do)
        {
            case "activate":
                foreach (var t in await db.PlanTerms.Where(t => t.CustomerId == c.Id && t.Status == "current").ToListAsync())
                    (t.Status, t.ContractEnd) = ("ended", DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
                (term.Status, term.ContractStart) = ("current", DateOnly.FromDateTime(DateTime.Today));
                (c.PlanId, c.RateClass) = (term.PlanId, term.RateClass);
                message = $"Activated {term.RateClass}";
                break;
            case "welcome" or "new-welcome":
                db.CustomerFiles.Add(new CustomerFile { CustomerId = c.Id, Name = $"welcome-packet-{c.Ticket}.pdf", Kind = "welcome" });
                db.ContactLogs.Add(new ContactLog { CustomerId = c.Id, Channel = "Email", Template = input.Do == "welcome" ? "Welcome Letter" : "Welcome Letter - Renewed", Status = "queued", Who = (await Me()).Name });
                message = "Welcome packet queued";
                break;
            case "edit-rate" when input.EnergyCharge is >= 0 and <= 1:
                term.EnergyCharge = input.EnergyCharge.Value;
                message = $"Energy charge set to {input.EnergyCharge}";
                break;
            default:
                return BadRequest(new { error = "Enter an energy charge between 0 and 1 (dollars per kWh)." });
        }
        await db.SaveChangesAsync();
        return Ok(new { message });
    }

    public record DatesInput(DateOnly? ServiceStart, DateOnly? ServiceEnd);

    [HttpPut("addresses/{addressId:int}")]
    public async Task<IActionResult> AddressDates(int addressId, DatesInput input)
    {
        if (input.ServiceStart is { } s && input.ServiceEnd is { } e && e < s) return BadRequest(new { error = "Service can't end before it starts." });
        var address = await db.ServiceAddresses.FindAsync(addressId);
        if (address is null) return NotFound();
        (address.ServiceStart, address.ServiceEnd) = (input.ServiceStart, input.ServiceEnd);
        await db.SaveChangesAsync();
        return Ok();
    }

    public record ErcotInput(string Do);

    [HttpPost("ercot/{txId:int}")]
    public async Task<IActionResult> ErcotAction(int txId, ErcotInput input)
    {
        if (input.Do is not ("unlink" or "cancel")) return BadRequest(new { error = "Pick unlink or cancel." });
        var tx = await db.ErcotTransactions.FindAsync(txId);
        if (tx is null) return NotFound();
        tx.Status = input.Do == "unlink" ? "unlinked" : "cancelled";
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- Files ------------------------------------------------------------------------------------------------

    private string Storage => Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage"));
    private static readonly string[] Allowed = [".pdf", ".png", ".jpg", ".jpeg", ".txt", ".csv", ".doc", ".docx"];

    [HttpPost("{id:int}/files")]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> Upload(int id, IFormFile file)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length is 0 or > 10_485_760 || !Allowed.Contains(ext)) return BadRequest(new { error = "Upload a PDF, image, text or Word file up to 10 MB." });
        var relative = Path.Combine("customer-files", c.Account, $"{Guid.NewGuid():N}{ext}");
        var full = Path.Combine(Storage, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using (var stream = System.IO.File.Create(full)) await file.CopyToAsync(stream);
        db.CustomerFiles.Add(new CustomerFile { CustomerId = id, Name = Path.GetFileName(file.FileName), Kind = "upload", Path = relative });
        await db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>A file's page: an upload (download link), or a generated document's text in English or Spanish.</summary>
    [HttpGet("files/{fileId:int}")]
    public async Task<IActionResult> FilePage(int fileId, string? lang)
    {
        var file = await db.CustomerFiles.Include(f => f.Customer).ThenInclude(c => c!.Plan).FirstOrDefaultAsync(f => f.Id == fileId);
        if (file is null) return NotFound();
        var c = file.Customer!;
        lang = lang is "en" or "es" ? lang : Documents.Preferred(c.Language);
        var doc = file.Path is null ? await Documents.RenderAsync(db, file, c, lang) : null;
        return Ok(new
        {
            file = new { file.Id, file.Name, file.Kind, file.CreatedAt, file.Regenerated, upload = file.Path != null },
            customer = new { c.Id, c.Account, c.Name, c.Email, c.Language },
            lang, languages = Documents.Languages,
            doc = doc == null ? null : new { doc.Title, doc.Paragraphs, rows = doc.Rows.Select(r => new { r.Label, r.Value }) },
            sent = await db.ContactLogs.Where(l => l.CustomerId == c.Id && l.Channel == "Email" && l.Template.StartsWith("Customer File: " + file.Name))
                .OrderByDescending(l => l.CreatedAt).Take(10).ToListAsync(),
        });
    }

    /// <summary>The file itself: the upload, or the generated document as a PDF (?download=true to save it).</summary>
    [HttpGet("files/{fileId:int}/download")]
    public async Task<IActionResult> Download(int fileId, string? lang, bool download = false)
    {
        var file = await db.CustomerFiles.Include(f => f.Customer).ThenInclude(c => c!.Plan).FirstOrDefaultAsync(f => f.Id == fileId);
        if (file is null) return NotFound();
        if (file.Path != null)
        {
            var full = Path.Combine(Storage, file.Path);
            return System.IO.File.Exists(full) ? PhysicalFile(full, "application/octet-stream", file.Name) : NotFound();
        }
        lang = lang is "en" or "es" ? lang : Documents.Preferred(file.Customer!.Language);
        var pdf = Documents.Pdf(await Documents.RenderAsync(db, file, file.Customer!, lang), file.Customer!);
        return download ? File(pdf, "application/pdf", Documents.PdfName(file, lang)) : File(pdf, "application/pdf");
    }

    /// <summary>Build the PDF again from the account as it is now.</summary>
    [HttpPost("files/{fileId:int}/regenerate")]
    public async Task<IActionResult> Regenerate(int fileId)
    {
        var file = await db.CustomerFiles.FindAsync(fileId);
        if (file is null || file.Path != null) return NotFound();
        file.Regenerated++;
        db.Record(file.CustomerId, "Billing & Files", "ItemFile_model", "CustomerFile", file.Id, "regenerated", $"{file.Name} PDF regenerated");
        await db.SaveChangesAsync();
        return Ok(new { message = $"{file.Name} regenerated." });
    }

    public record SendFileInput(string Lang, string Email, string? Note);

    /// <summary>Email a file to the customer. Mail isn't set up yet, so it's logged as not sent until it is.</summary>
    [HttpPost("files/{fileId:int}/send")]
    public async Task<IActionResult> SendFile(int fileId, SendFileInput input)
    {
        var file = await db.CustomerFiles.FindAsync(fileId);
        if (file is null) return NotFound();
        if (!System.Net.Mail.MailAddress.TryCreate(input.Email, out _)) return BadRequest(new { error = "Enter an email address." });
        var language = Documents.Languages.GetValueOrDefault(input.Lang, "English");
        var me = await Me();
        var ready = config.GetSection("Integrations:Smtp").GetChildren().Any(s => !string.IsNullOrEmpty(s.Value));
        db.ContactLogs.Add(new ContactLog
        {
            CustomerId = file.CustomerId, Channel = "Email", Template = $"Customer File: {file.Name} ({language})", Body = input.Note,
            Status = ready ? "queued" : "not sent", Who = me.Name, SentAt = ready ? DateTime.UtcNow : null,
        });
        db.Notes.Add(new Note { CustomerId = file.CustomerId, Author = me.Name, Category = "Contact", Action = "Contact - Send Email", Priority = "Low", Body = $"Emailed {file.Name} in {language} to {input.Email}." });
        db.Record(file.CustomerId, "Billing & Files", "ItemFile_model", "CustomerFile", file.Id, "emailed", $"{file.Name} emailed in {language} to {input.Email}");
        await db.SaveChangesAsync();
        return Ok(new { message = ready ? $"{file.Name} queued to {input.Email} in {language}." : $"{file.Name} logged, but not sent: email (SMTP) isn't set up." });
    }

    // ---- Account actions --------------------------------------------------------------------------------------

    /// <summary>An action's form: its fields, plus the account's payment methods and payments for the pickers.</summary>
    [HttpGet("{id:int}/actions/{key}")]
    public async Task<IActionResult> ActionForm(int id, string key)
    {
        var action = Corral.Actions.FirstOrDefault(a => a.Key == key);
        var c = await db.Customers.FindAsync(id);
        if (action is null || c is null) return NotFound();
        return Ok(new
        {
            action, customer = new { c.Id, c.Account, c.Name, c.Phone, c.Balance },
            methods = await db.PaymentMethods.Where(m => m.CustomerId == id && m.RemovedAt == null).Select(m => new { m.Id, m.Type, m.Last4, m.Nickname, m.Autopay }).ToListAsync(),
            payments = await db.Payments.Where(p => p.CustomerId == id).OrderByDescending(p => p.PaidOn).Select(p => new { p.Id, p.Reference, p.PaidOn, p.Amount, p.Method, p.Status }).ToListAsync(),
            templates = Corral.EmailTemplates,
        });
    }

    [HttpPost("{id:int}/actions/{key}")]
    public async Task<IActionResult> RunAction(int id, string key, Dictionary<string, string> input)
    {
        var action = Corral.Actions.FirstOrDefault(a => a.Key == key);
        var c = await db.Customers.FindAsync(id);
        if (action is null || c is null) return NotFound();
        var me = await Me();
        if (action.Permission != null && !me.Permissions.Contains(action.Permission)) return StatusCode(403, new { error = "You don't have the right to do that." });
        foreach (var f in action.Fields)
        {
            var v = input.GetValueOrDefault(f.Name)?.Trim() ?? "";
            var ok = f.Type switch
            {
                "money" => decimal.TryParse(v, out var m) && m is >= 0.01m and <= 100000,
                "number" => int.TryParse(v, out var n) && n is >= 1 and <= 100000,
                "date" => DateOnly.TryParse(v, out _),
                "method" or "payment" => int.TryParse(v, out _),
                "template" => Corral.EmailTemplates.Contains(v),
                "textarea" => v.Length is > 0 and <= 2000,
                _ => v.Length is > 0 and <= 200,
            };
            if (!ok) return BadRequest(new { error = $"Check \"{f.Label}\"." });
        }
        try
        {
            var result = await new AccountActions(db, c, me.Name, config).RunAsync(action, input);
            return Ok(new { message = result.Message, goTo = result.GoTo });
        }
        catch (ActionProblem e) { return BadRequest(new { error = e.Message }); }
    }

    // ---- Logs -------------------------------------------------------------------------------------------------

    /// <summary>The Log button on the Balances, Actions and Flags panels: every entry, newest first.</summary>
    [HttpGet("{id:int}/panel-log/{panel}")]
    public async Task<IActionResult> PanelLog(int id, string panel)
    {
        object rows = panel switch
        {
            "balances" => (await db.LedgerEntries.Where(e => e.CustomerId == id).ToListAsync())
                .Select(e => new { record = "ledger_entry", e.Id, at = e.CreatedAt, type = e.Kind == "credit" ? "Credit" : "Debit", amount = e.Kind == "credit" ? -e.Amount : e.Amount, description = e.Description, status = e.Status, by = e.RequestedBy })
                .Concat((await db.Payments.Where(p => p.CustomerId == id).ToListAsync())
                    .Select(p => new { record = "payment", p.Id, at = p.PaidOn.ToDateTime(p.PaidTime ?? TimeOnly.MinValue), type = "Payment", amount = -p.Amount, description = $"{p.Method} · {p.Reference}", status = p.Status, by = p.Source }))
                .OrderByDescending(r => r.at).ToList(),
            "actions" => await ActionNotes(id).ToListAsync(),
            "flags" => await db.CustomerFlags.Where(f => f.CustomerId == id).OrderByDescending(f => f.CreatedAt).ToListAsync(),
            _ => null!,
        };
        if (rows is null) return NotFound();
        var c = await db.Customers.FindAsync(id);
        return Ok(new { customer = new { c!.Id, c.Account, c.Name }, rows, panels = new Dictionary<string, string> { ["balances"] = "Balances", ["actions"] = "Actions", ["flags"] = "Flags" } });
    }

    private IQueryable<Note> ActionNotes(int id)
    {
        var labels = Corral.Actions.Select(a => a.Label).ToList();
        return db.Notes.Where(n => n.CustomerId == id && n.Action != null && labels.Contains(n.Action)).OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id);
    }

    [HttpGet("{id:int}/contact-log")]
    public async Task<IActionResult> ContactLog(int id)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        return Ok(new
        {
            customer = new { c.Id, c.Account, c.Name, c.Email, c.Phone, c.MarketingOptIn },
            logs = await db.ContactLogs.Where(l => l.CustomerId == id).OrderByDescending(l => l.CreatedAt).ToListAsync(),
            calls = await db.PhoneCalls.Include(p => p.User).Where(p => p.CustomerId == id).OrderByDescending(p => p.StartedAt)
                .Select(p => new { p.Id, p.StartedAt, p.Direction, p.Phone, p.Issue, p.DurationSec, answeredBy = p.User!.Name, p.Resolution }).ToListAsync(),
        });
    }

    [HttpPost("{id:int}/contact-log/marketing")]
    public async Task<IActionResult> ToggleMarketing(int id)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        c.MarketingOptIn = !c.MarketingOptIn;
        await db.SaveChangesAsync();
        return Ok(new { message = $"Marketing emails: opted {(c.MarketingOptIn ? "IN" : "OUT")}" });
    }

    [HttpGet("{id:int}/api-log")]
    public async Task<IActionResult> ApiLog(int id)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        return Ok(new { customer = new { c.Id, c.Account, c.Name }, logs = await db.ApiLogs.Where(l => l.CustomerId == id).OrderByDescending(l => l.CreatedAt).ToListAsync() });
    }

    /// <summary>The account's logs (History.Logs) that have entries: key, title, count.</summary>
    private async Task<List<object>> LogCounts(int id)
    {
        var counts = await db.History.Where(h => h.CustomerId == id).GroupBy(h => h.Group).Select(g => new { group = g.Key, n = g.Count() }).ToListAsync();
        return HistoryLogs.Logs.Select(l => new { l.Key, l.Title, count = counts.Where(c => l.Groups.Contains(c.group)).Sum(c => c.n) })
            .Where(l => l.count > 0).Cast<object>().ToList();
    }

    /// <summary>The account ticket (every history entry), one log (e.g. "Logs - Products"), or a bill's own ticket.</summary>
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id, string? log, int? bill)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        var query = db.History.Where(h => h.CustomerId == id);
        string title = $"Account - {c.Account}";
        if (log != null)
        {
            var l = HistoryLogs.Logs.FirstOrDefault(x => x.Key == log);
            if (l is null) return NotFound();
            query = query.Where(h => l.Groups.Contains(h.Group));
            title = l.Title;
        }
        if (bill is { } billId)
        {
            var b = await db.Bills.FirstOrDefaultAsync(x => x.Id == billId && x.CustomerId == id);
            if (b is null) return NotFound();
            query = query.Where(h => h.RecordType == "Bill" && h.RecordId == billId);
            title = $"Bill Details - {b.Invoice ?? b.Reference}";
        }
        var items = await query.OrderBy(h => h.CreatedAt).ThenBy(h => h.Id).ToListAsync();
        return Ok(new
        {
            customer = new { c.Id, c.Account, c.Name, c.Ticket, c.CreatedAt }, title,
            groups = items.GroupBy(h => h.Model).OrderBy(g => g.Key).Select(g => new { model = g.Key, items = g }),
            logs = log == null && bill == null ? await LogCounts(id) : null,
            queues = log == null && bill == null ? await db.QueueLogs.Where(q => q.CustomerId == id).OrderBy(q => q.EnteredAt).ToListAsync() : null,
        });
    }

    // ---- helpers ----------------------------------------------------------------------------------------------

    private void AddNote(int customerId, string who, string body) => db.Notes.Add(new Note { CustomerId = customerId, Author = who, Body = body });

    private async Task<Permissions.SignedIn> Me() => (await Permissions.CurrentAsync(HttpContext))!;
}

/// <summary>The account's logs, each its own page (like ebunny's "Logs - Products"): key, title, and the history groups in it.</summary>
public static class HistoryLogs
{
    public record Log(string Key, string Title, string[] Groups);

    public static readonly Log[] Logs =
    [
        new("attributes", "Logs - Account Attributes", ["Account Attributes"]),
        new("products", "Logs - Products", ["Products"]),
        new("payments", "Logs - Payments", ["Payments"]),
        new("files", "Logs - Files & Usage", ["Billing & Files", "Usage"]),
        new("ercot", "Logs - EDI Transactions", ["EDI Transactions"]),
        new("emails", "Logs - Emails", ["Emails"]),
        new("notes", "Logs - Notes", ["Notes"]),
        new("contact", "Logs - Phone Calls", ["Contact"]),
        new("logins", "Logs - Logins", ["Logins"]),
        new("queues", "Logs - Queues", ["Queues"]),
    ];
}
