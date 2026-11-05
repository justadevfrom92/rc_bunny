using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// Astro: plans and pricing, the same screens as ebunny's Astro. Plans, plan groups, rates (search and the grid to
/// update them), TDSP fees, term discounts, ETFs, Build Your Own Plan products, their discount limits and a CSV upload.
/// </summary>
[ApiController]
[Route("api/astro")]
[RequirePermission("plans")]
public class AstroController(AppDbContext db) : ControllerBase
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    // ---- Plans ------------------------------------------------------------------------------------------------

    /// <summary>ebunny calls the rate classes Resi and Biz.</summary>
    private static string Class(Plan p) => p.Type == "Business" ? "Biz" : "Resi";

    private static object PlanRow(Plan p) => new
    {
        p.Id, type = Class(p), term = p.TermMonths, p.Name, @internal = p.Code, p.Rolloff, p.Etf, mrc = p.MonthlyFee, green = p.GreenPercent, p.Active,
    };

    [HttpGet("plans")]
    public async Task<object> Plans()
    {
        var plans = await db.Plans.OrderBy(p => p.Type).ThenBy(p => p.TermMonths).ThenBy(p => p.Name).ToListAsync();
        return new { active = plans.Where(p => p.Active).Select(PlanRow), inactive = plans.Where(p => !p.Active).Select(PlanRow) };
    }

    [HttpGet("plans/form")]
    public async Task<IActionResult> PlanForm(int? id)
    {
        var p = id == null ? null : await db.Plans.FindAsync(id);
        if (id != null && p == null) return NotFound();
        return Ok(new
        {
            plan = p == null
                ? new { id = 0, name = "", @internal = "", slug = "", active = true, type = "Resi", term = 12, rolloff = "JEY3", etf = "$250", mrc = 4.95m, green = 100, tags = "", perks = "" }
                : new { id = p.Id, name = p.Name, @internal = p.Code, slug = p.Slug, active = p.Active, type = Class(p), term = p.TermMonths, rolloff = p.Rolloff, etf = p.Etf, mrc = p.MonthlyFee, green = p.GreenPercent,
                    tags = string.Join(", ", p.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)), perks = p.Perks },
            rolloffs = await db.Plans.Where(x => x.TermMonths <= 1).OrderBy(x => x.Code).Select(x => x.Code).ToListAsync(),
        });
    }

    public record PlanInput(string? Name, string? Internal, string? Slug, bool Active, string? Type, int Term, string? Rolloff, string? Etf, decimal? Mrc, int? Green, string? Tags, string? Perks);

    [HttpPost("plans")]
    public Task<IActionResult> AddPlan(PlanInput input) => SavePlan(new Plan(), input);

    [HttpPut("plans/{id:int}")]
    public async Task<IActionResult> UpdatePlan(int id, PlanInput input) =>
        await db.Plans.FindAsync(id) is { } p ? await SavePlan(p, input) : NotFound();

    private async Task<IActionResult> SavePlan(Plan p, PlanInput input)
    {
        var code = input.Internal?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Give the plan a name." });
        if (!Regex.IsMatch(code, "^[A-Za-z0-9]{1,40}$")) return BadRequest(new { error = "The internal name is letters and numbers only." });
        if (await db.Plans.AnyAsync(x => x.Code == code && x.Id != p.Id)) return BadRequest(new { error = "Another plan has that internal name." });
        if (input.Type is not ("Resi" or "Biz")) return BadRequest(new { error = "Pick a rate class." });
        if (input.Term is < 1 or > 60) return BadRequest(new { error = "The term is 1 to 60 months." });
        if (input.Green is < 0 or > 100) return BadRequest(new { error = "Renewable is 0 to 100%." });
        p.Name = input.Name.Trim(); p.Code = code; p.Active = input.Active; p.Type = input.Type == "Biz" ? "Business" : "Residential"; p.TermMonths = input.Term;
        p.Slug = string.IsNullOrWhiteSpace(input.Slug) ? Slug(p.Name) : input.Slug.Trim();
        p.Rolloff = string.IsNullOrWhiteSpace(input.Rolloff) ? "None" : input.Rolloff; p.Etf = string.IsNullOrWhiteSpace(input.Etf) ? "-" : input.Etf.Trim();
        p.MonthlyFee = input.Mrc ?? 0; p.GreenPercent = input.Green ?? 0;
        p.Tags = string.Join(",", (input.Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        p.Perks = string.Join("\n", (input.Perks ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (p.Id == 0) db.Plans.Add(p);
        await db.SaveChangesAsync();
        return Ok(new { message = "Plan saved" });
    }

    private static string Slug(string s) => Regex.Replace(s.ToLowerInvariant().Replace("&", "and"), "[^a-z0-9]+", "-").Trim('-');

    // ---- Plan groups ------------------------------------------------------------------------------------------

    private static List<string> Codes(PlanGroup g) => g.PlanCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    [HttpGet("groups")]
    public async Task<object> Groups()
    {
        var plans = await db.Plans.OrderBy(p => p.Name).ToListAsync();
        return new
        {
            groups = (await db.PlanGroups.OrderBy(g => g.Id).ToListAsync()).Select(g => new
            {
                g.Id, g.Name, g.Slug,
                plans = Codes(g).Select(c => plans.FirstOrDefault(p => p.Code == c)).Where(p => p != null).Select(p => new { p!.Id, p.Name, @internal = p.Code }),
            }),
            plans = plans.Select(p => new { p.Id, p.Name, @internal = p.Code }),
        };
    }

    public record GroupPlan(int PlanId);

    [HttpPost("groups/{id:int}/plans")]
    public async Task<IActionResult> Attach(int id, GroupPlan input)
    {
        var g = await db.PlanGroups.FindAsync(id);
        var p = await db.Plans.FindAsync(input.PlanId);
        if (g is null || p is null) return NotFound();
        var codes = Codes(g);
        if (!codes.Contains(p.Code)) codes.Add(p.Code);
        g.PlanCodes = string.Join(",", codes);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{p.Code} added to {g.Name}" });
    }

    [HttpDelete("groups/{id:int}/plans/{planId:int}")]
    public async Task<IActionResult> Detach(int id, int planId)
    {
        var g = await db.PlanGroups.FindAsync(id);
        var p = await db.Plans.FindAsync(planId);
        if (g is null || p is null) return NotFound();
        g.PlanCodes = string.Join(",", Codes(g).Where(c => c != p.Code));
        await db.SaveChangesAsync();
        return Ok(new { message = $"{p.Code} removed from {g.Name}" });
    }

    [HttpGet("groups/form")]
    public async Task<IActionResult> GroupForm(int? id)
    {
        var g = id == null ? null : await db.PlanGroups.FindAsync(id);
        if (id != null && g == null) return NotFound();
        var plans = await db.Plans.OrderBy(p => p.Name).Select(p => new { p.Id, p.Name, @internal = p.Code }).ToListAsync();
        var chosen = g == null ? [] : Codes(g).Select(c => plans.FirstOrDefault(p => p.@internal == c)?.Id).OfType<int>().ToList();
        return Ok(new { group = g == null ? new { id = 0, name = "", slug = "" } : new { id = g.Id, name = g.Name, slug = g.Slug }, plans, chosen });
    }

    public record GroupInput(string? Name, string? Slug, int[]? Plans);

    [HttpPost("groups")]
    public Task<IActionResult> AddGroup(GroupInput input) => SaveGroup(new PlanGroup(), input);

    [HttpPut("groups/{id:int}")]
    public async Task<IActionResult> UpdateGroup(int id, GroupInput input) =>
        await db.PlanGroups.FindAsync(id) is { } g ? await SaveGroup(g, input) : NotFound();

    private async Task<IActionResult> SaveGroup(PlanGroup g, GroupInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Slug)) return BadRequest(new { error = "Give the group a name and a slug." });
        var slug = input.Slug.Trim();
        if (await db.PlanGroups.AnyAsync(x => x.Slug == slug && x.Id != g.Id)) return BadRequest(new { error = "Another group has that slug." });
        var codes = await db.Plans.Where(p => (input.Plans ?? Array.Empty<int>()).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code);
        g.Name = input.Name.Trim(); g.Slug = slug;
        g.PlanCodes = string.Join(",", (input.Plans ?? []).Where(codes.ContainsKey).Select(id => codes[id]));
        if (g.Id == 0) db.PlanGroups.Add(g);
        await db.SaveChangesAsync();
        return Ok(new { message = "Group saved" });
    }

    // ---- Rates ------------------------------------------------------------------------------------------------

    /// <summary>Search Rates: today's energy charge, the TDSP delivery charge and the average price at a usage.</summary>
    [HttpGet("rates")]
    public async Task<object> Rates(string? plan, int? market, int kwh = 1000)
    {
        if (kwh is not (500 or 1000 or 2000)) kwh = 1000;
        var plans = await db.Plans.Where(p => p.Active && (plan == null || p.Name.Contains(plan) || p.Code.Contains(plan)))
            .OrderBy(p => p.Type).ThenBy(p => p.TermMonths).ThenBy(p => p.Name).ToListAsync();
        var markets = await db.Markets.OrderBy(m => m.Code).ToListAsync();
        var rates = Pricing.CurrentRates(await db.Rates.ToListAsync(), Today);
        var rows = from p in plans
                   from m in markets.Where(m => market == null || m.Id == market)
                   let r = rates.FirstOrDefault(r => r.PlanId == p.Id && r.MarketId == m.Id)
                   where r != null
                   select new
                   {
                       type = Class(p), term = p.TermMonths, p.Name, @internal = p.Code, market = m.Code, energy = r.EnergyCents, tdsp = m.DeliveryCentsPerKwh,
                       avg = r.EnergyCents + m.DeliveryCentsPerKwh + (m.DeliveryPerMonth + p.MonthlyFee) / kwh * 100, effective = r.EffectiveOn,
                   };
        return new { kwh, markets = markets.Select(m => new { m.Id, name = m.Code }), rows };
    }

    /// <summary>Update Rates: the newest rate (scheduled or current) for each plan and market.</summary>
    [HttpGet("rates/edit")]
    public async Task<object> RatesGrid(int? plan, bool all = false)
    {
        var plans = await db.Plans.Where(p => plan != null ? p.Id == plan : all || p.Active).OrderBy(p => p.Type).ThenBy(p => p.TermMonths).ThenBy(p => p.Name).ToListAsync();
        var markets = await db.Markets.OrderBy(m => m.Code).ToListAsync();
        var latest = (await db.Rates.ToListAsync()).GroupBy(r => (r.PlanId, r.MarketId)).Select(g => g.MaxBy(r => r.EffectiveOn)!).ToList();
        return new
        {
            onePlan = plan == null ? null : plans.FirstOrDefault()?.Name, showAll = all,
            markets = markets.Select(m => new { m.Id, m.Short }),
            plans = plans.Select(p => new { p.Id, p.Name, @internal = p.Code, p.Active }),
            rates = latest.ToDictionary(r => $"{r.PlanId}-{r.MarketId}", r => r.EnergyCents),
            pending = latest.Count(r => r.EffectiveOn > Today),
        };
    }

    public record RatesInput(DateOnly EffectiveOn, Dictionary<int, Dictionary<int, decimal?>> Rates);

    /// <summary>Save the changed cells as new rates starting on the effective date.</summary>
    [HttpPut("rates")]
    public async Task<IActionResult> SaveRates(RatesInput input)
    {
        var latest = (await db.Rates.ToListAsync()).GroupBy(r => (r.PlanId, r.MarketId)).ToDictionary(g => g.Key, g => g.MaxBy(r => r.EffectiveOn)!.EnergyCents);
        var planIds = (await db.Plans.Select(p => p.Id).ToListAsync()).ToHashSet();
        var marketIds = (await db.Markets.Select(m => m.Id).ToListAsync()).ToHashSet();
        var changed = 0;
        foreach (var (planId, byMarket) in input.Rates)
            foreach (var (marketId, energy) in byMarket)
            {
                if (energy is null || !planIds.Contains(planId) || !marketIds.Contains(marketId)) continue;
                if (energy is < 0 or > 100) return BadRequest(new { error = "Rates are 0 to 100 ¢/kWh." });
                if (latest.TryGetValue((planId, marketId), out var now) && Math.Abs(now - energy.Value) < 0.0005m) continue;
                db.Rates.Add(new Rate { PlanId = planId, MarketId = marketId, EnergyCents = Math.Round(energy.Value, 3), EffectiveOn = input.EffectiveOn });
                changed++;
            }
        await db.SaveChangesAsync();
        return Ok(new { message = changed > 0 ? $"{changed} rates saved, effective {input.EffectiveOn:yyyy-MM-dd}" : "No changes to save" });
    }

    // ---- TDSP fees --------------------------------------------------------------------------------------------

    [HttpGet("fees")]
    public async Task<object> Fees() => (await db.Markets.OrderBy(m => m.Code).ToListAsync())
        .Select(m => new { m.Id, name = m.Code, description = m.Name, perKwh = m.DeliveryCentsPerKwh, perBill = m.DeliveryPerMonth, effectiveOn = m.DeliveryEffectiveOn });

    public record FeeInput(int MarketId, decimal PerKwh, decimal PerBill, DateOnly? EffectiveOn);

    [HttpPut("fees")]
    public async Task<IActionResult> SaveFees(List<FeeInput> fees)
    {
        var changed = 0;
        foreach (var f in fees)
        {
            if (f.PerKwh is < 0 or > 100 || f.PerBill is < 0 or > 1000) return BadRequest(new { error = "Fees are 0 to 100 ¢/kWh and $0 to $1,000 a bill." });
            if (await db.Markets.FindAsync(f.MarketId) is not { } m) continue;
            if (Math.Abs(m.DeliveryCentsPerKwh - f.PerKwh) < 0.00005m && Math.Abs(m.DeliveryPerMonth - f.PerBill) < 0.005m) continue;
            (m.DeliveryCentsPerKwh, m.DeliveryPerMonth, m.DeliveryEffectiveOn) = (f.PerKwh, f.PerBill, f.EffectiveOn ?? Today);
            changed++;
        }
        await db.SaveChangesAsync();
        return Ok(new { message = changed > 0 ? $"{changed} TDSP fee changes saved" : "No changes to save" });
    }

    // ---- Modifiers: term discounts and ETFs -------------------------------------------------------------------

    [HttpGet("terms")]
    public async Task<object> Terms()
    {
        var discounts = await db.TermDiscounts.ToListAsync();
        var regions = discounts.Select(d => d.Region).Distinct().OrderBy(r => r == "Generic" ? "" : r).ToList();
        return new
        {
            regions,
            grid = discounts.GroupBy(d => d.Term).OrderBy(g => g.Key).Select(g => new { term = g.Key, values = regions.ToDictionary(r => r, r => g.FirstOrDefault(d => d.Region == r)?.Discount ?? 0) }),
        };
    }

    [HttpPut("terms")]
    public async Task<IActionResult> SaveTerms(Dictionary<int, Dictionary<string, decimal>> d)
    {
        var discounts = await db.TermDiscounts.ToListAsync();
        foreach (var (term, byRegion) in d)
            foreach (var (region, value) in byRegion)
            {
                if (value is < -50 or > 50) return BadRequest(new { error = "Discounts are -50 to 50 ¢/kWh." });
                var row = discounts.FirstOrDefault(x => x.Term == term && x.Region == region);
                if (row == null) db.TermDiscounts.Add(new TermDiscount { Term = term, Region = region, Discount = value });
                else row.Discount = value;
            }
        await db.SaveChangesAsync();
        return Ok(new { message = "Term discounts saved" });
    }

    [HttpGet("etfs")]
    public async Task<object> Etfs() => await db.TermEtfs.OrderBy(e => e.Term).Select(e => new { e.Term, e.Amount }).ToListAsync();

    [HttpPut("etfs")]
    public async Task<IActionResult> SaveEtfs(Dictionary<int, decimal> etf)
    {
        foreach (var e in await db.TermEtfs.ToListAsync())
            if (etf.TryGetValue(e.Term, out var amount))
            {
                if (amount is < 0 or > 10000) return BadRequest(new { error = "ETFs are $0 to $10,000." });
                e.Amount = amount;
            }
        await db.SaveChangesAsync();
        return Ok(new { message = "ETFs saved" });
    }

    // ---- Build Your Own Plan products -------------------------------------------------------------------------

    private static object ProductRow(ByopProduct p) => new
    {
        p.Id, p.Key, p.Name, p.Model, p.Type, p.Step, rateAdj = p.RateAdj, p.Monthly, p.Active, p.Description, minDiscount = p.MinDiscount, maxDiscount = p.MaxDiscount,
    };

    [HttpGet("products")]
    public async Task<object> Products() => new
    {
        products = (await db.ByopProducts.OrderBy(p => p.Position).ThenBy(p => p.Id).ToListAsync()).Select(ProductRow),
        steps = ByopProduct.Steps, types = ByopProduct.Types,
    };

    public record ProductPrice(decimal RateAdj, decimal Monthly, bool Active);

    [HttpPut("products")]
    public async Task<IActionResult> SaveProducts(Dictionary<int, ProductPrice> p)
    {
        foreach (var row in await db.ByopProducts.ToListAsync())
            if (p.TryGetValue(row.Id, out var v))
            {
                if (v.RateAdj is < -10 or > 10 || v.Monthly is < 0 or > 500) return BadRequest(new { error = $"{row.Name}: the rate change is -10 to 10 ¢/kWh and the monthly price $0 to $500." });
                (row.RateAdj, row.Monthly, row.Active) = (v.RateAdj, v.Monthly, v.Active);
            }
        await db.SaveChangesAsync();
        return Ok(new { message = "Products saved" });
    }

    public record ProductInput(string? Name, string? Description, string? Key, string? Step, string? Type, decimal RateAdj, decimal Monthly, bool Active);

    [HttpPost("products")]
    public Task<IActionResult> AddProduct(ProductInput input) => SaveProduct(new ByopProduct(), input);

    [HttpPut("products/{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, ProductInput input) =>
        await db.ByopProducts.FindAsync(id) is { } p ? await SaveProduct(p, input) : NotFound();

    private async Task<IActionResult> SaveProduct(ByopProduct p, ProductInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Give the product a name." });
        if (!ByopProduct.Steps.Contains(input.Step) || !ByopProduct.Types.Contains(input.Type)) return BadRequest(new { error = "Pick a step and a type." });
        if (input.RateAdj is < -10 or > 10 || input.Monthly is < 0 or > 500) return BadRequest(new { error = "The rate change is -10 to 10 ¢/kWh and the monthly price $0 to $500." });
        if (p.Id == 0)
        {
            var key = Regex.Replace(string.IsNullOrWhiteSpace(input.Key) ? input.Name : input.Key, "[^a-z0-9]+", "_", RegexOptions.IgnoreCase).Trim('_').ToLowerInvariant();
            if (await db.ByopProducts.AnyAsync(x => x.Key == key)) return BadRequest(new { error = "Another product has that key." });
            p.Key = key[..Math.Min(40, key.Length)];
            p.Position = (await db.ByopProducts.MaxAsync(x => (int?)x.Position) ?? 0) + 1;
            db.ByopProducts.Add(p);
        }
        p.Name = input.Name.Trim(); p.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        (p.Step, p.Type, p.RateAdj, p.Monthly, p.Active) = (input.Step!, input.Type!, input.RateAdj, input.Monthly, input.Active);
        await db.SaveChangesAsync();
        return Ok(new { message = "Product saved", p.Id });
    }

    [HttpGet("products/{id:int}")]
    public async Task<IActionResult> Product(int id) => await db.ByopProducts.FindAsync(id) is { } p ? Ok(ProductRow(p)) : NotFound();

    public record Limits(decimal? MinDiscount, decimal? MaxDiscount);

    /// <summary>Modifiers → Products: how far each add-on can be discounted.</summary>
    [HttpPut("modifiers/products")]
    public async Task<IActionResult> SaveLimits(Dictionary<int, Limits> p)
    {
        foreach (var row in await db.ByopProducts.ToListAsync())
            if (p.TryGetValue(row.Id, out var v))
            {
                if (v.MinDiscount > 0 || v.MaxDiscount > 0) return BadRequest(new { error = $"{row.Name}: discounts are negative numbers." });
                if (v.MinDiscount < v.MaxDiscount)
                    return BadRequest(new { error = $"{row.Name}: the min discount is the smaller cut (e.g. 0 and -0.50)." });
                (row.MinDiscount, row.MaxDiscount) = (v.MinDiscount, v.MaxDiscount);
            }
        await db.SaveChangesAsync();
        return Ok(new { message = "Discount limits saved" });
    }

    /// <summary>Upload BYOP Discounts: key, rate_adj, min_discount, max_discount. Nothing changes unless every row is valid.</summary>
    [HttpPost("uploads/byop")]
    public async Task<IActionResult> UploadByop(IFormFile? file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Choose a CSV file." });
        var products = await db.ByopProducts.ToDictionaryAsync(p => p.Key);
        var problems = new List<string>();
        var changes = new List<(ByopProduct P, decimal? Adj, decimal? Min, decimal? Max)>();
        using var reader = new StreamReader(file.OpenReadStream());
        var line = 0;
        while (await reader.ReadLineAsync() is { } text)
        {
            line++;
            if (string.IsNullOrWhiteSpace(text)) continue;
            var cells = text.Split(',').Select(c => c.Trim().Trim('"')).ToArray();
            if (line == 1 && cells[0].Equals("key", StringComparison.OrdinalIgnoreCase)) continue;
            if (!products.TryGetValue(cells[0], out var p)) { problems.Add($"Line {line}: no product with the key \"{cells[0]}\"."); continue; }
            decimal? Num(int i, string what)
            {
                if (cells.Length <= i || cells[i] == "") return null;
                if (decimal.TryParse(cells[i], out var v)) return v;
                problems.Add($"Line {line}: {what} \"{cells[i]}\" isn't a number.");
                return null;
            }
            changes.Add((p, Num(1, "rate_adj"), Num(2, "min_discount"), Num(3, "max_discount")));
        }
        if (problems.Count > 0) return BadRequest(new { error = "Nothing was saved. " + string.Join(" ", problems) });
        foreach (var (p, adj, min, max) in changes)
        {
            if (adj != null) p.RateAdj = adj.Value;
            if (min != null) p.MinDiscount = min;
            if (max != null) p.MaxDiscount = max;
        }
        await db.SaveChangesAsync();
        return Ok(new { message = $"{changes.Count} products updated" });
    }
}
