using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Corral → Create Order (residential and business by phone), and Renew / Change Plan for an existing account.</summary>
[ApiController]
[Route("api/orders")]
[RequirePermission("corral")]
public class OrdersController(AppDbContext db) : ControllerBase
{
    public static readonly string[] EnrollmentTypes = ["Switch", "Move-In", "Self-Selected Switch", "Renewal"];
    public static readonly string[] Sources = ["Phone", "Website", "Referral", "Power to Choose"];

    /// <summary>The order form: markets, plans for residential or business, and the account being renewed (or one at the same ESIID).</summary>
    [HttpGet("form")]
    public async Task<IActionResult> Form(bool biz = false, string? renew = null, string? esiid = null)
    {
        Customer? renewing = null;
        if (!string.IsNullOrEmpty(renew) && (renewing = await db.Customers.FirstOrDefaultAsync(c => c.Account == renew)) is null) return NotFound();
        var prefill = renewing ?? (string.IsNullOrEmpty(esiid) ? null : await db.Customers.FirstOrDefaultAsync(c => c.Esiid == esiid));
        biz = biz || renewing?.Type == "Small Business";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rates = Pricing.CurrentRates(await db.Rates.ToListAsync(), today);
        var markets = await db.Markets.OrderBy(m => m.Name).ToListAsync();
        return Ok(new
        {
            biz, renew = renewing?.Account, enrollmentTypes = EnrollmentTypes, sources = Sources,
            prefill = prefill == null ? null : new { customerId = prefill.Id, businessName = prefill.Name, prefill.Name, prefill.Phone, prefill.Email, prefill.Address, prefill.City, prefill.Zip, prefill.Esiid, prefill.MarketId, prefill.PlanId, prefill.Autopay, prefill.Paperless, prefill.PeakPerks },
            markets = markets.Select(m => new { m.Id, m.Name }),
            plans = (await db.Plans.Where(p => p.Active && p.Type == (biz ? "Business" : "Residential")).OrderBy(p => p.TermMonths).ThenBy(p => p.Name).ToListAsync())
                .Select(p => new
                {
                    p.Id, p.Name, p.Code, p.TermMonths,
                    // price at 1,000 kWh in each market, where the plan has a rate there
                    prices = markets.ToDictionary(m => m.Id.ToString(), m => rates.Where(r => r.PlanId == p.Id && r.MarketId == m.Id).MaxBy(r => r.EffectiveOn) is { } r ? Pricing.PriceAt1000(r.EnergyCents, m, p) : (decimal?)null),
                }),
        });
    }

    public record OrderInput(string? Renew, bool Biz, string? BusinessName, string Name, string Phone, string Email, string Address, string City, string Zip,
        string? Esiid, int MarketId, int PlanId, string EnrollmentType, DateOnly? StartDate, bool Autopay, bool Paperless, bool PeakPerks, string Source);

    [HttpPost]
    public async Task<IActionResult> Create(OrderInput d)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == d.PlanId && p.Active);
        if (plan is null) return BadRequest(new { error = "Pick a plan." });
        var me = (await Permissions.CurrentAsync(HttpContext))!;

        if (!string.IsNullOrEmpty(d.Renew))
        {
            var c = await db.Customers.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Account == d.Renew);
            if (c is null) return NotFound();
            var old = c.Plan?.Name ?? "no plan";
            c.PlanId = plan.Id;
            db.Notes.Add(new Note { CustomerId = c.Id, Author = me.Name, Category = "Order", Action = "Renewal", Body = $"Plan changed from {old} to {plan.Name} ({plan.Code})." });
            db.PlanTerms.Add(await NewTerm(c, plan, d.MarketId, d.StartDate));
            db.WorkItems.Add(new WorkItem { CustomerId = c.Id, Queue = "unapplied-plan-changes", Summary = "Send plan change to billing" });
            await db.SaveChangesAsync();
            return Ok(new { c.Id, message = "Plan changed" });
        }

        if (string.IsNullOrWhiteSpace(d.Name) || string.IsNullOrWhiteSpace(d.Phone) || !System.Net.Mail.MailAddress.TryCreate(d.Email, out _))
            return BadRequest(new { error = "Enter the name, phone and a valid email." });
        if (d.Biz && string.IsNullOrWhiteSpace(d.BusinessName)) return BadRequest(new { error = "Enter the business name." });
        if (string.IsNullOrWhiteSpace(d.Address) || string.IsNullOrWhiteSpace(d.City) || d.Zip?.Length != 5 || !d.Zip.All(char.IsDigit))
            return BadRequest(new { error = "Enter the service address, city and 5-digit zip." });
        if (!await db.Markets.AnyAsync(m => m.Id == d.MarketId)) return BadRequest(new { error = "Pick the market." });
        if (!EnrollmentTypes.Contains(d.EnrollmentType) || !Sources.Contains(d.Source)) return BadRequest(new { error = "Pick the enrollment type and source." });
        if (d.StartDate < DateOnly.FromDateTime(DateTime.Today)) return BadRequest(new { error = "The start date can't be in the past." });

        var customer = await Enroll(d, plan, me.Name);
        return Ok(new { customer.Id, message = $"Order submitted for account {customer.Account}" });
    }

    /// <summary>Creates the account the way ebunny's Enrollment does: plan term, service address, queue, note, and a work item to send it on.</summary>
    private async Task<Customer> Enroll(OrderInput d, Plan plan, string who)
    {
        var last = await db.Customers.MaxAsync(c => c.Account);
        var name = d.Biz ? d.BusinessName!.Trim() : d.Name.Trim();
        var parts = d.Name.Trim().Split(' ', 2);
        var esiid = string.IsNullOrWhiteSpace(d.Esiid) ? null : d.Esiid.Trim();
        var c = new Customer
        {
            Account = (long.Parse(last ?? "1219000000") + 1373).ToString(), Ticket = DateTime.Now.ToString("yyyyMMddHHmmssfff") + Random.Shared.Next(100, 999),
            Name = name, FirstName = parts[0], LastName = parts.ElementAtOrDefault(1) ?? "", Type = d.Biz ? "Small Business" : "Residential",
            Phone = d.Phone.Trim(), Email = d.Email.Trim(), Address = d.Address.Trim(), City = d.City.Trim(), Zip = d.Zip,
            BillingStreet = d.Address.Trim(), BillingCity = d.City.Trim(), BillingState = "TX", BillingZip = d.Zip,
            MarketId = d.MarketId, Esiid = esiid, PlanId = plan.Id, Status = "Submitted", StatusInfo = "New", Exception = esiid is null ? "No ESIID" : null,
            Source = d.Source, MoveSwitch = d.EnrollmentType == "Move-In" ? "move" : "switch", RequestedStart = d.StartDate,
            Autopay = d.Autopay, Paperless = d.Paperless, PeakPerks = d.PeakPerks, Channel = d.Source, Msid = d.Source == "Website" ? "1001" : "2001",
            RateClass = $"{DateTime.Today:yyyyMMdd}_{(plan.TermMonths <= 1 ? "V" : "F")}_{plan.Code}",
        };
        db.Customers.Add(c);
        await db.SaveChangesAsync();
        db.PlanTerms.Add(await NewTerm(c, plan, d.MarketId, d.StartDate));
        db.ServiceAddresses.Add(new ServiceAddress { CustomerId = c.Id, Street = c.Address, City = c.City, Zip = c.Zip, Esiid = esiid, ServiceStart = d.StartDate });
        foreach (var (product, on) in new[] { ("AutoPay", d.Autopay), ("Paperless Billing", d.Paperless), ("Peak Perks", d.PeakPerks) })
            if (on) db.CustomerProducts.Add(new CustomerProduct { CustomerId = c.Id, Product = product, Author = who });
        db.QueueLogs.Add(new QueueLog { CustomerId = c.Id, Queue = "QueueCartSubmit", Who = who });
        db.Notes.Add(new Note { CustomerId = c.Id, Author = who, Category = "Order", Action = "New Enrollment",
            Body = $"Order taken by {who} via {d.Source}{(d.Biz ? $" (contact: {d.Name.Trim()})" : "")}." });
        db.WorkItems.Add(new WorkItem { CustomerId = c.Id, Queue = "unprocessed-orders", Summary = "Send enrollment to the utility" });
        await db.SaveChangesAsync();
        return c;
    }

    /// <summary>The price the customer signed up at: today's rate and delivery charges for their market.</summary>
    private async Task<PlanTerm> NewTerm(Customer c, Plan plan, int marketId, DateOnly? start)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rate = Pricing.CurrentRate(await db.Rates.Where(r => r.PlanId == plan.Id && r.MarketId == marketId).ToListAsync(), today);
        var market = await db.Markets.FindAsync(marketId);
        return new PlanTerm
        {
            CustomerId = c.Id, PlanId = plan.Id, RateClass = $"{DateTime.Today:yyyyMMdd}_{(plan.TermMonths <= 1 ? "V" : "F")}_{plan.Code}",
            Status = "not started", ContractStart = start, ContractEnd = start is { } s && plan.TermMonths > 1 ? s.AddMonths(plan.TermMonths) : null,
            EnergyCharge = rate == null ? 0 : Math.Round(rate.EnergyCents / 100, 8),
            Rate2000 = rate == null || market == null ? 0 : Math.Round(rate.EnergyCents + market.DeliveryCentsPerKwh + (market.DeliveryPerMonth + plan.MonthlyFee) * 100 / 2000, 1),
        };
    }
}
