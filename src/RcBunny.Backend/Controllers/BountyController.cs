using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Bounty (rewards): offers, earning rules, redemptions to send, members, and the monthly drawing.</summary>
[ApiController]
[Route("api/bounty")]
[RequirePermission("bounty")]
public class BountyController(AppDbContext db) : ControllerBase
{
    public static readonly string[] Effects = ["credit", "gift", "drawing", "product"];
    private static string ThisMonth => DateTime.UtcNow.ToString("yyyy-MM");

    /// <summary>Rewards dashboard (ebunny's bounty/dashboard).</summary>
    [HttpGet("dashboard")]
    public async Task<object> Dashboard()
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var month = await db.StarEntries.Where(e => e.CreatedAt >= monthStart).Select(e => e.Stars).ToListAsync();
        return new
        {
            members = await db.Customers.CountAsync(c => c.Stars > 0),
            outstanding = await db.Customers.SumAsync(c => c.Stars),
            earned = month.Where(s => s > 0).Sum(),
            redeemed = -month.Where(s => s < 0).Sum(),
            giftsPending = await db.StarEntries.CountAsync(e => e.Fulfillment == "pending"),
            popular = await db.StarEntries.Where(e => e.OfferId != null).GroupBy(e => e.Offer!.Name)
                .Select(g => new { name = g.Key, n = g.Count() }).OrderByDescending(x => x.n).Take(10).ToListAsync(),
            top = await db.Customers.Where(c => c.Stars > 0).OrderByDescending(c => c.Stars).Take(10).Select(c => new { c.Id, c.Account, c.Name, c.Stars }).ToListAsync(),
        };
    }

    // ---- Offers and earning rules -----------------------------------------------------------------------------

    [HttpGet("offers")]
    public async Task<object> Offers() => new
    {
        effects = Effects,
        offers = await db.RewardOffers.OrderBy(o => o.Position).ThenBy(o => o.Stars)
            .Select(o => new { o.Id, o.Name, o.Description, o.Stars, o.Effect, o.Value, o.Active, o.Position, redeemed = db.StarEntries.Count(e => e.OfferId == o.Id) })
            .ToListAsync(),
    };

    public record OfferInput(string Name, string Description, int Stars, string Effect, string? Value, bool Active, int Position);

    [HttpPost("offers")]
    public async Task<IActionResult> AddOffer(OfferInput input) => await SaveOffer(new RewardOffer(), input, isNew: true);

    [HttpPut("offers/{id:int}")]
    public async Task<IActionResult> UpdateOffer(int id, OfferInput input) =>
        await db.RewardOffers.FindAsync(id) is { } offer ? await SaveOffer(offer, input, isNew: false) : NotFound();

    private async Task<IActionResult> SaveOffer(RewardOffer offer, OfferInput input, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Name the offer." });
        if (input.Stars <= 0) return BadRequest(new { error = "An offer has to cost at least 1 star." });
        if (!Effects.Contains(input.Effect)) return BadRequest(new { error = "Pick what the offer does." });
        if (input.Effect == "credit" && !decimal.TryParse(input.Value, out _)) return BadRequest(new { error = "For a bill credit, enter the dollar amount." });
        offer.Name = input.Name.Trim(); offer.Description = input.Description?.Trim() ?? ""; offer.Stars = input.Stars;
        offer.Effect = input.Effect; offer.Value = input.Value?.Trim() ?? ""; offer.Active = input.Active; offer.Position = input.Position;
        if (isNew) db.RewardOffers.Add(offer);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("rules")]
    public async Task<object> Rules() => new
    {
        rules = await db.RewardRules.OrderBy(r => r.Id).ToListAsync(),
        last = await db.JobRuns.Where(r => r.Job == "Reward stars").OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(),
    };

    /// <summary>Award Stars Now: run the nightly "Reward stars" job straight away.</summary>
    [HttpPost("award")]
    public async Task<IActionResult> Award()
    {
        var run = await RcBunny.Jobs.Scheduler.RunAsync(HttpContext.RequestServices, RcBunny.Jobs.Scheduler.All.First(j => j.Name == "Reward stars"), await MyName());
        return Ok(new { message = run.Message });
    }

    public record RuleInput(string Name, int Stars, bool Active);

    [HttpPost("rules")]
    public async Task<IActionResult> AddRule(RuleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Stars <= 0) return BadRequest(new { error = "Enter a name and how many stars it earns." });
        db.RewardRules.Add(new RewardRule { Name = input.Name.Trim(), Stars = input.Stars, Active = input.Active });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("rules/{id:int}")]
    public async Task<IActionResult> UpdateRule(int id, RuleInput input)
    {
        var rule = await db.RewardRules.FindAsync(id);
        if (rule is null) return NotFound();
        if (input.Stars <= 0) return BadRequest(new { error = "A rule has to earn at least 1 star." });
        rule.Name = input.Name.Trim(); rule.Stars = input.Stars; rule.Active = input.Active;
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- Redemptions ------------------------------------------------------------------------------------------

    [HttpGet("redemptions")]
    public async Task<object> Redemptions(bool pending = true) => await db.StarEntries
        .Where(e => e.OfferId != null && (!pending || e.Fulfillment == "pending")).OrderByDescending(e => e.CreatedAt).Take(200)
        .Select(e => new { e.Id, e.CustomerId, account = e.Customer!.Account, customer = e.Customer.Name, address = e.Customer.Address + ", " + e.Customer.City, email = e.Customer.Email,
            offer = e.Offer!.Name, effect = e.Offer.Effect, value = e.Offer.Value, stars = -e.Stars, e.Fulfillment, e.By, e.CreatedAt })
        .ToListAsync();

    /// <summary>Mark a gift card or product as sent.</summary>
    [HttpPost("redemptions/{id:int}/sent")]
    public async Task<IActionResult> MarkSent(int id)
    {
        var entry = await db.StarEntries.Include(e => e.Offer).FirstOrDefaultAsync(e => e.Id == id);
        if (entry is null) return NotFound();
        if (entry.Fulfillment != "pending") return BadRequest(new { error = "That one isn't waiting to be sent." });
        entry.Fulfillment = "sent";
        db.Notes.Add(new Note { CustomerId = entry.CustomerId, Author = await MyName(), Body = $"Sent their reward: {entry.Offer!.Name}." });
        await db.SaveChangesAsync();
        return Ok();
    }

    // ---- Members ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Members (ebunny's bounty/members): search by account, name or email; the top members; and one member (?account=)
    /// with their star history (and drawing entries) to adjust.
    /// </summary>
    [HttpGet("members")]
    public async Task<object> Members(string? q, string? account)
    {
        q = q?.Trim() ?? "";
        var results = q == "" ? [] : await db.Customers
            .Where(c => EF.Functions.Like(c.Account, $"%{q}%") || EF.Functions.Like(c.Name, $"%{q}%") || EF.Functions.Like(c.Email!, $"%{q}%"))
            .OrderBy(c => c.Name).Take(25).Select(c => new { c.Id, c.Account, c.Name, c.Status, c.Stars }).ToListAsync();
        var member = string.IsNullOrEmpty(account) ? null : await db.Customers.FirstOrDefaultAsync(c => c.Account == account);
        return new
        {
            q, results,
            top = await db.Customers.Where(c => c.Stars > 0).OrderByDescending(c => c.Stars).Take(10).Select(c => new { c.Id, c.Account, c.Name, c.Status, c.Stars }).ToListAsync(),
            member = member == null ? null : new { member.Id, member.Account, member.Name, member.Stars },
            history = member == null ? [] : (await db.StarEntries.Where(e => e.CustomerId == member.Id).Select(e => new { record = "star_entry", e.Id, at = e.CreatedAt, e.Reason, e.Stars, drawing = false }).ToListAsync())
                .Concat((await db.DrawingEntries.Where(d => d.CustomerId == member.Id).ToListAsync())
                    .Select(d => new { record = "drawing_entry", d.Id, at = d.CreatedAt, Reason = $"Entered the {d.Month} drawing", Stars = 0, drawing = true }))
                .OrderByDescending(e => e.at).ToList(),
        };
    }

    public record Adjustment(int Stars, string Reason, int? RuleId);

    /// <summary>Give or take stars: by an earning rule (e.g. "Refer a friend"), or by hand with a reason.</summary>
    [HttpPost("members/{id:int}/adjust")]
    public async Task<IActionResult> Adjust(int id, Adjustment input)
    {
        var c = await db.Customers.FindAsync(id);
        if (c is null) return NotFound();
        var stars = input.Stars;
        var reason = input.Reason?.Trim() ?? "";
        if (input.RuleId is { } ruleId)
        {
            var rule = await db.RewardRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.Active);
            if (rule is null) return BadRequest(new { error = "Pick a rule." });
            (stars, reason) = (rule.Stars, rule.Name);
        }
        if (stars == 0 || reason.Length == 0) return BadRequest(new { error = "Enter the stars and a reason." });
        if (c.Stars + stars < 0) return BadRequest(new { error = $"They only have {c.Stars:N0} stars." });
        c.Stars += stars;
        db.StarEntries.Add(new StarEntry { CustomerId = id, Stars = stars, Reason = reason, By = await MyName() });
        await db.SaveChangesAsync();
        return Ok();
    }

    public record RedeemInput(int OfferId);

    // ---- Monthly drawing --------------------------------------------------------------------------------------

    [HttpGet("drawing")]
    public async Task<object> Drawing(string? month)
    {
        month ??= ThisMonth;
        return new
        {
            month, thisMonth = ThisMonth,
            months = Enumerable.Range(0, 12).Select(i => DateTime.UtcNow.AddMonths(-i).ToString("yyyy-MM")),
            past = await db.DrawingWinners.Where(w => w.Month != month).OrderByDescending(w => w.Month).Take(12)
                .Select(w => new { w.Id, w.Month, w.Prize, w.CustomerId, account = w.Customer!.Account, w.DrawnAt }).ToListAsync(),
            entries = await db.DrawingEntries.Where(e => e.Month == month).OrderBy(e => e.CreatedAt)
                .Select(e => new { e.Id, e.CustomerId, account = e.Customer!.Account, customer = e.Customer.Name, e.CreatedAt }).ToListAsync(),
            winner = await db.DrawingWinners.Where(w => w.Month == month).Select(w => new { w.Id, w.CustomerId, account = w.Customer!.Account, customer = w.Customer.Name, w.Prize, w.DrawnBy, w.DrawnAt }).FirstOrDefaultAsync(),
        };
    }

    public record DrawInput(string Prize);

    /// <summary>Pick this month's winner at random from the entries. One winner per month.</summary>
    [HttpPost("drawing/draw")]
    public async Task<IActionResult> Draw(DrawInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Prize)) return BadRequest(new { error = "Say what the prize is." });
        if (await db.DrawingWinners.AnyAsync(w => w.Month == ThisMonth)) return BadRequest(new { error = "This month already has a winner." });
        var entries = await db.DrawingEntries.Where(e => e.Month == ThisMonth).Select(e => e.CustomerId).ToListAsync();
        if (entries.Count == 0) return BadRequest(new { error = "Nobody has entered this month's drawing yet." });
        var winnerId = entries[Random.Shared.Next(entries.Count)];
        var me = await MyName();
        db.DrawingWinners.Add(new DrawingWinner { Month = ThisMonth, CustomerId = winnerId, Prize = input.Prize.Trim(), DrawnBy = me });
        db.Notes.Add(new Note { CustomerId = winnerId, Author = me, Body = $"Won the {ThisMonth} monthly drawing: {input.Prize.Trim()}." });
        await db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>One drawing winner (ebunny's bounty/winner).</summary>
    [HttpGet("winners/{id:int}")]
    public async Task<IActionResult> Winner(int id)
    {
        var w = await db.DrawingWinners.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id);
        if (w is null) return NotFound();
        var c = w.Customer!;
        return Ok(new
        {
            w.Id, w.Month, w.Prize, w.DrawnAt, w.DrawnBy,
            customer = new { c.Id, c.Account, c.Name, c.Email, c.Phone, c.Address, c.Unit, c.City, c.Zip, c.Stars },
            entries = await db.DrawingEntries.CountAsync(e => e.Month == w.Month),
            theirEntries = await db.DrawingEntries.CountAsync(e => e.Month == w.Month && e.CustomerId == c.Id),
            note = await db.Notes.Where(n => n.CustomerId == c.Id && n.Body.Contains(w.Month) && n.Body.Contains("drawing")).OrderByDescending(n => n.CreatedAt)
                .Select(n => new { n.Body, n.Author, n.CreatedAt }).FirstOrDefaultAsync(),
        });
    }

    [HttpGet("winners")]
    public async Task<object> Winners() => await db.DrawingWinners.OrderByDescending(w => w.Month)
        .Select(w => new { w.Id, w.Month, w.CustomerId, account = w.Customer!.Account, customer = w.Customer.Name, w.Prize, w.DrawnBy, w.DrawnAt,
            entries = db.DrawingEntries.Count(e => e.Month == w.Month) })
        .ToListAsync();

    /// <summary>Remove a winner (for example if they can't be reached), so that month can be drawn again.</summary>
    [HttpDelete("winners/{id:int}")]
    public async Task<IActionResult> RemoveWinner(int id)
    {
        var winner = await db.DrawingWinners.FindAsync(id);
        if (winner is null) return NotFound();
        db.DrawingWinners.Remove(winner);
        Audit.Add(db, await MyName(), "access", $"Removed the {winner.Month} drawing winner.");
        await db.SaveChangesAsync();
        return Ok();
    }

    private async Task<string> MyName() => (await Permissions.CurrentAsync(HttpContext))!.Name;
}
