using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Backend.Access;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Api;

[Route("api/lando"), RequirePerm("lando")]
public class LandoController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var day = DateTime.UtcNow.AddDays(-1); var week = DateTime.UtcNow.AddDays(-7);
        var visits = await db.SiteVisits.Where(v => v.CreatedAt > week).Select(v => new { v.Visitor, v.Path, v.CreatedAt, v.SeenAt, v.Referrer, v.Blocked }).ToListAsync();
        var consents = await db.CookieConsents.Where(c => c.CreatedAt > week.AddDays(-23)).Select(c => c.Choice).ToListAsync();
        return Ok(new
        {
            onlineNow = visits.Where(v => v.SeenAt > DateTime.UtcNow.AddMinutes(-5)).Select(v => v.Visitor).Distinct().Count(),
            visitors24h = visits.Where(v => v.CreatedAt > day).Select(v => v.Visitor).Distinct().Count(),
            views24h = visits.Count(v => v.CreatedAt > day), views7d = visits.Count, visitors7d = visits.Select(v => v.Visitor).Distinct().Count(),
            blocked7d = visits.Count(v => v.Blocked),
            topPages = visits.GroupBy(v => v.Path).Select(g => new { path = g.Key, views = g.Count() }).OrderByDescending(x => x.views).Take(10),
            referrers = visits.Where(v => v.Referrer != null).GroupBy(v => new Uri(v.Referrer!).Host).Select(g => new { host = g.Key, visits = g.Count() }).OrderByDescending(x => x.visits).Take(6),
            byDay = visits.GroupBy(v => v.CreatedAt.Date).OrderBy(g => g.Key).Select(g => new { day = g.Key.ToString("yyyy-MM-dd"), views = g.Count(), visitors = g.Select(x => x.Visitor).Distinct().Count() }),
            consents = consents.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count()),
            pages = await db.Pages.GroupBy(p => p.Status).Select(g => new { status = g.Key, n = g.Count() }).ToListAsync(),
            blocks = await db.SiteBlocks.CountAsync(b => b.Active),
        });
    }

    /// <summary>Site Health: things on the site that need fixing.</summary>
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var pages = await db.Pages.Select(p => new { p.Id, p.Path, p.Title, p.Status, p.MetaDescription, p.HtmlTitle, p.Redirect, p.ContentPrimaryId, p.File }).ToListAsync();
        var paths = pages.Select(p => p.Path).ToHashSet();
        var issues = new List<object>();
        foreach (var p in pages)
        {
            if (p.Status == "Published" && string.IsNullOrWhiteSpace(p.MetaDescription)) issues.Add(new { p.Id, p.Path, problem = "No meta description", severity = "warn" });
            if (p.Status == "Published" && p.ContentPrimaryId == null && p.File == null && p.Redirect == null) issues.Add(new { p.Id, p.Path, problem = "No content", severity = "bad" });
            if (p.Redirect is { } r && r.StartsWith('/') && !paths.Contains(r.Trim('/')) && r != "/") issues.Add(new { p.Id, p.Path, problem = $"Redirects to {r}, which isn't a page", severity = "bad" });
        }
        foreach (var dup in pages.GroupBy(p => p.HtmlTitle).Where(g => g.Key != null && g.Count() > 1))
            issues.Add(new { Id = dup.First().Id, Path = string.Join(", ", dup.Select(p => p.Path)), problem = $"Same page title on {dup.Count()} pages", severity = "warn" });
        var unusedBlocks = await db.ContentBlocks.Where(b => !db.PageComponents.Any(c => c.ContentBlockId == b.Id) && !db.Pages.Any(p => p.ContentPrimaryId == b.Id || p.ContentSecondaryId == b.Id || p.ContentParentId == b.Id || p.ContentAuxiliaryId == b.Id)).CountAsync();
        return Ok(new { pages = pages.Count, published = pages.Count(p => p.Status == "Published"), issues, unusedBlocks });
    }

    [HttpGet("sitemap")]
    public async Task<IActionResult> Sitemap() => Ok(await db.Pages.Where(p => p.Status == "Published" && p.Redirect == null && !p.NoIndex).OrderBy(p => p.Path).Select(p => new { p.Path, p.Title, p.UpdatedAt }).ToListAsync());

    [HttpPost("sitemap")]
    public async Task<IActionResult> RebuildSitemap()
    {
        var n = await db.Pages.CountAsync(p => p.Status == "Published" && p.Redirect == null && !p.NoIndex);
        Audit(db, "sitemap_rebuilt", $"{n} pages");
        await db.SaveChangesAsync();
        return Ok(new { message = $"Sitemap rebuilt: {n} pages. The website serves it at /sitemap.xml." });
    }
}

/// <summary>Reference data tables (Caboose deposits/taxes…, Lando IPs, Bounty winners): a grid per table, saved whole.</summary>
[Route("api/data")]
public class DataTablesController(AppDbContext db) : ApiControllerBase
{
    private static JsonObject Defs => AppConfig.Get("admin")["reference_tables"]!.AsObject();

    [HttpGet]
    public IActionResult Index() => Ok(Defs.Where(kv => Can(kv.Value![2]!.ToString())).Select(kv => new { key = kv.Key, name = kv.Value![0]!.ToString(), app = kv.Value[2]!.ToString(), columns = kv.Value[1]!.AsArray().Count }));

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        if (Defs[key] is not JsonArray def) return NotFound();
        if (!Can(def[2]!.ToString()) && !Can("sheriff")) return Forbidden(def[2]!.ToString());
        var rows = await db.ReferenceRows.Where(r => r.TableKey == key).OrderBy(r => r.Position).Select(r => r.Cells).ToListAsync();
        var opts = def.Count > 3 ? def[3] : null;
        return Ok(new { key, name = def[0]!.ToString(), app = def[2]!.ToString(), columns = def[1]!.AsArray().Select(c => c!.ToString()), types = opts?["types"], rows });
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Save(string key, List<List<string>> rows)
    {
        if (Defs[key] is not JsonArray def) return NotFound();
        if (!Can(def[2]!.ToString()) && !Can("sheriff")) return Forbidden(def[2]!.ToString());
        var cols = def[1]!.AsArray().Count;
        await db.ReferenceRows.Where(r => r.TableKey == key).ExecuteDeleteAsync();
        foreach (var (r, i) in rows.Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).Select((r, i) => (r, i)))
            db.ReferenceRows.Add(new ReferenceRow { TableKey = key, Position = i, Cells = new JsonArray(Enumerable.Range(0, cols).Select(c => (JsonNode?)(r.ElementAtOrDefault(c)?.Trim() ?? "")).ToArray()) });
        Audit(db, "reference_table_saved", def[0]!.ToString(), $"{rows.Count} rows");
        await db.SaveChangesAsync();
        return Ok(new { saved = rows.Count });
    }
}

[Route("api/astro"), RequirePerm("astro")]
public class AstroController(AppDbContext db) : ApiControllerBase
{
    private static decimal Avg(decimal energy, TdspFee? f, decimal mrc, int kwh) => f is null ? 0 : Math.Round(Pricing.AveragePrice(energy, f.PerKwh, f.PerBill, mrc, kwh), 2);

    /// <summary>View Rates: current (and next scheduled) energy charge with the EFL average prices.</summary>
    [HttpGet("rates")]
    public async Task<IActionResult> Rates([FromQuery] int? marketId, [FromQuery] string? type, [FromQuery] string? q)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fees = (await db.TdspFees.Where(f => f.EffectiveOn <= today).ToListAsync()).GroupBy(f => f.MarketId).ToDictionary(g => g.Key, g => g.OrderBy(f => f.EffectiveOn).Last());
        var all = await db.Rates.Include(r => r.Plan).Include(r => r.Market).Where(r => marketId == null || r.MarketId == marketId).ToListAsync();
        var rows = all.GroupBy(r => (r.PlanId, r.MarketId)).Select(g =>
        {
            var cur = g.Where(r => r.EffectiveOn <= today).OrderBy(r => r.EffectiveOn).ThenBy(r => r.Id).LastOrDefault();
            var next = g.Where(r => r.EffectiveOn > today).OrderBy(r => r.EffectiveOn).FirstOrDefault();
            var p = g.First().Plan!; var m = g.First().Market!;
            var fee = fees.GetValueOrDefault(m.Id);
            return new
            {
                planId = p.Id, plan = p.Name, code = p.Internal, p.Type, p.Term, marketId = m.Id, market = m.Short, energy = cur?.Energy, effective = cur?.EffectiveOn,
                next = next?.Energy, nextOn = next?.EffectiveOn,
                avg500 = cur is null ? 0 : Avg(cur.Energy, fee, p.Mrc, 500), avg1000 = cur is null ? 0 : Avg(cur.Energy, fee, p.Mrc, 1000), avg2000 = cur is null ? 0 : Avg(cur.Energy, fee, p.Mrc, 2000),
            };
        }).Where(r => (type == null || r.Type == type) && (q == null || r.plan.Contains(q, StringComparison.OrdinalIgnoreCase) || r.code.Contains(q, StringComparison.OrdinalIgnoreCase)))
          .OrderBy(r => r.plan).ThenBy(r => r.market);
        return Ok(new { rows, markets = await db.Markets.Select(m => new { m.Id, m.Short, m.Name }).ToListAsync() });
    }

    public record RateChange(int PlanId, int MarketId, decimal Energy);
    public record RateUpdate(DateOnly EffectiveOn, List<RateChange> Changes);

    /// <summary>Update Rates: new rows with an effective date (history is kept; the website switches on that date).</summary>
    [HttpPut("rates")]
    public async Task<IActionResult> SaveRates(RateUpdate r)
    {
        if (r.Changes.Count == 0) return Problem422("Change at least one rate.");
        if (r.EffectiveOn < DateOnly.FromDateTime(DateTime.Today)) return Problem422("The effective date can't be in the past.");
        if (r.Changes.Any(c => c.Energy <= 0 || c.Energy > 100)) return Problem422("Rates are in ¢/kWh, between 0 and 100.");
        foreach (var c in r.Changes) db.Rates.Add(new Rate { PlanId = c.PlanId, MarketId = c.MarketId, Energy = c.Energy, EffectiveOn = r.EffectiveOn, CreatedBy = Me.UserId });
        Audit(db, "rates_saved", $"{r.Changes.Count} rates", $"effective {r.EffectiveOn:yyyy-MM-dd}");
        await db.SaveChangesAsync();
        return Ok(new { saved = r.Changes.Count });
    }

    /// <summary>Upload a rate sheet (CSV: Plan, Market, Energy[, Effective]) → a Rate Upload to review and confirm.</summary>
    [HttpPost("rates/import"), RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Import(IFormFile file, [FromForm] DateOnly? effectiveOn, [FromServices] FileStore files)
    {
        var path = $"rate-imports/{DateTime.UtcNow:yyyyMMddHHmmss}-{Path.GetFileName(file.FileName)}";
        await using (var s = file.OpenReadStream()) await files.SaveAsync(path, s);
        var imp = new RateImport { UserId = Me.UserId, FileName = file.FileName, Path = path, Status = "ready", EffectiveOn = effectiveOn ?? DateOnly.FromDateTime(DateTime.Today.AddDays(1)) };
        var plans = await db.Plans.ToDictionaryAsync(p => p.Internal, StringComparer.OrdinalIgnoreCase);
        var markets = await db.Markets.ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var current = (await db.Rates.Where(x => x.EffectiveOn <= today).ToListAsync()).GroupBy(x => (x.PlanId, x.MarketId)).ToDictionary(g => g.Key, g => g.OrderBy(x => x.EffectiveOn).Last().Energy);
        var changes = new JsonArray(); var problems = new JsonArray();
        using var reader = new StreamReader(System.IO.File.OpenRead(files.PathFor(path)));
        var header = (await reader.ReadLineAsync())?.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToList() ?? [];
        int Col(string n) => header.IndexOf(n);
        if (Col("plan") < 0 || Col("market") < 0 || Col("energy") < 0) { imp.Status = "failed"; imp.Error = "The first row must have Plan, Market and Energy columns."; }
        else
        {
            var line = 1;
            while (await reader.ReadLineAsync() is { } l)
            {
                line++;
                if (string.IsNullOrWhiteSpace(l)) continue;
                var c = l.Split(',').Select(x => x.Trim()).ToArray();
                imp.Rows++;
                if (!plans.TryGetValue(c[Col("plan")], out var p)) { problems.Add($"Row {line}: no plan {c[Col("plan")]}"); continue; }
                var m = markets.FirstOrDefault(x => x.Name.Equals(c[Col("market")], StringComparison.OrdinalIgnoreCase) || x.Short.Equals(c[Col("market")], StringComparison.OrdinalIgnoreCase));
                if (m is null) { problems.Add($"Row {line}: no market {c[Col("market")]}"); continue; }
                if (!decimal.TryParse(c[Col("energy")], NumberStyles.Number, CultureInfo.InvariantCulture, out var e)) { problems.Add($"Row {line}: energy isn't a number"); continue; }
                var from = current.GetValueOrDefault((p.Id, m.Id));
                if (from == e) { imp.Unchanged++; continue; }
                changes.Add(new JsonObject { ["plan"] = p.Internal, ["market"] = m.Name, ["from"] = from, ["to"] = e });
            }
        }
        imp.Changes = changes; imp.Problems = problems; imp.FinishedAt = DateTime.UtcNow;
        if (imp.Status == "ready" && changes.Count == 0) { imp.Status = "failed"; imp.Error ??= "Nothing changed."; }
        db.RateImports.Add(imp);
        await db.SaveChangesAsync();
        return Ok(new { imp.Id, imp.Status, changes = changes.Count, problems = problems.Count, imp.Unchanged, imp.Error });
    }

    [HttpGet("rates/template")]
    public IActionResult Template() => File(System.Text.Encoding.UTF8.GetBytes("Plan,Market,Energy\nCT12,TX-E-ONCOR,11.2\n"), "text/csv", "rate-template.csv");

    /// <summary>Term modifiers: discount (¢/kWh) per term and region, and the ETF per term.</summary>
    [HttpGet("terms")]
    public async Task<IActionResult> Terms()
    {
        var mods = await db.TermDiscounts.ToListAsync();
        var etfs = await db.TermEtfs.ToDictionaryAsync(e => e.Term, e => e.Amount);
        var regions = mods.Select(m => m.Region).Distinct().OrderBy(r => r == "Generic" ? "" : r).ToList();
        return Ok(new { regions, rows = mods.GroupBy(m => m.Term).OrderBy(g => g.Key).Select(g => new { term = g.Key, discounts = regions.ToDictionary(r => r, r => g.FirstOrDefault(x => x.Region == r)?.Discount ?? 0), etf = etfs.GetValueOrDefault(g.Key) }) });
    }

    public record TermRow(int Term, Dictionary<string, decimal> Discounts, int Etf);

    [HttpPut("terms")]
    public async Task<IActionResult> SaveTerms(List<TermRow> rows)
    {
        var mods = await db.TermDiscounts.AsTracking().ToListAsync();
        var etfs = await db.TermEtfs.AsTracking().ToListAsync();
        foreach (var r in rows)
        {
            foreach (var (region, d) in r.Discounts)
                if (mods.FirstOrDefault(m => m.Term == r.Term && m.Region == region) is { } m) m.Discount = d;
                else db.TermDiscounts.Add(new TermDiscount { Term = r.Term, Region = region, Discount = d });
            if (etfs.FirstOrDefault(e => e.Term == r.Term) is { } e) e.Amount = r.Etf; else db.TermEtfs.Add(new TermEtf { Term = r.Term, Amount = r.Etf });
        }
        Audit(db, "terms_saved", $"{rows.Count} terms");
        await db.SaveChangesAsync();
        return Ok(new { saved = rows.Count });
    }
}

[Route("api/walker"), RequirePerm("walker")]
public class WalkerController(AppDbContext db, FileStore files) : ApiControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] string tab = "running")
    {
        var q = db.ReportRuns.Include(r => r.User).AsQueryable();
        q = tab switch { "completed" => q.Where(r => r.Status == "completed"), "problems" => q.Where(r => r.Status == "failed" || r.Status == "timeout"), _ => q.Where(r => r.Status == "running" || r.Status == "queued") };
        var counts = await db.ReportRuns.GroupBy(r => r.Status).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.n);
        var rows = await q.OrderByDescending(r => r.Id).Take(100).Select(r => new { r.Id, r.Title, r.Status, r.Rows, r.Format, r.CreatedAt, r.FinishedAt, r.DurationMs, who = r.User!.Name, r.App }).ToListAsync();
        return Ok(new { counts, rows });
    }

    [HttpGet("reports")]
    public IActionResult ReportsList() => Ok(Reports.All.Select(r => new { r.Key, r.Title, r.Category, r.Description, r.Model, r.Filters, defaults = r.Defaults?.Invoke() }));

    public record RunRequest(Dictionary<string, string> Params, string Format, bool Summary, string? SaveAs, string? App);

    [HttpPost("reports/{key}")]
    public async Task<IActionResult> Run(string key, RunRequest r)
    {
        var def = Reports.Get(key);
        if (def is null) return NotFound();
        if (r.Format is not ("xlsx" or "csv" or "tsv" or "json")) return Problem422("Pick a format.");
        var p = new JsonObject(r.Params.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value)));
        SavedReport? saved = null;
        if (!string.IsNullOrWhiteSpace(r.SaveAs))
        {
            saved = new SavedReport { UserId = Me.UserId, Name = r.SaveAs.Trim(), Report = key, Params = p.DeepClone(), Format = r.Format, Summary = r.Summary, LastRunAt = DateTime.UtcNow };
            db.SavedReports.Add(saved);
        }
        var run = new ReportRun { UserId = Me.UserId, Report = key, Title = def.Title + (r.Summary ? " (summary)" : ""), Model = def.Model, App = r.App ?? "walker", Params = p, Format = r.Format, Summary = r.Summary, Status = "queued", SavedReport = saved };
        db.ReportRuns.Add(run);
        await db.SaveChangesAsync();
        return Ok(new { run.Id });
    }

    [HttpGet("runs/{id:int}")]
    public async Task<IActionResult> RunDetail(int id)
    {
        var r = await db.ReportRuns.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        if (r.UserId == Me.UserId && r.SeenAt == null) await db.ReportRuns.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.SeenAt, DateTime.UtcNow));
        return Ok(new { r.Id, r.Title, r.Report, r.Status, r.Rows, r.Format, r.Summary, r.Params, r.Preview, r.Error, r.File, r.FileSize, r.CreatedAt, r.StartedAt, r.FinishedAt, r.DurationMs, who = r.User?.Name, r.RerunOf, r.SavedReportId, mine = r.UserId == Me.UserId });
    }

    [HttpGet("runs/{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var r = await db.ReportRuns.FirstOrDefaultAsync(x => x.Id == id);
        if (r?.File is null || !files.Exists(r.File)) return NotFound(new { code = "not_found", message = "The file isn't there (the report didn't finish, or the sample data has no file)." });
        return PhysicalFile(files.PathFor(r.File), TableExport.Write(r.Format, [], [], "").Mime, Path.GetFileName(r.File));
    }

    [HttpPost("runs/{id:int}/rerun")]
    public async Task<IActionResult> Rerun(int id)
    {
        var r = await db.ReportRuns.FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return NotFound();
        var run = new ReportRun { UserId = Me.UserId, Report = r.Report, Title = r.Title, Model = r.Model, App = r.App, Params = r.Params?.DeepClone() ?? new JsonObject(), Format = r.Format, Summary = r.Summary, Status = "queued", RerunOf = r.Id, SavedReportId = r.SavedReportId };
        db.ReportRuns.Add(run);
        await db.SaveChangesAsync();
        return Ok(new { run.Id });
    }

    [HttpGet("reports/{key}/columns")]
    public async Task<IActionResult> Columns(string key)
    {
        var def = Reports.Get(key);
        if (def is null) return NotFound();
        var sample = await def.Run(db, def.Defaults?.Invoke() ?? []);
        var saved = await db.ReportColumns.FirstOrDefaultAsync(c => c.Report == key);
        return Ok(new { available = sample.Columns, columns = saved?.Columns });
    }

    public record ColumnMap(string Key, string Title);

    [HttpPut("reports/{key}/columns")]
    public async Task<IActionResult> SaveColumns(string key, List<ColumnMap> map)
    {
        var row = await db.ReportColumns.AsTracking().FirstOrDefaultAsync(c => c.Report == key);
        var json = new JsonArray(map.Select(m => (JsonNode?)new JsonObject { ["key"] = m.Key, ["title"] = m.Title }).ToArray());
        if (map.Count == 0) { if (row != null) db.ReportColumns.Remove(row); }
        else if (row is null) db.ReportColumns.Add(new ReportColumn { Report = key, Columns = json, UserId = Me.UserId });
        else { row.Columns = json; row.UserId = Me.UserId; }
        await db.SaveChangesAsync();
        return Ok(new { saved = map.Count });
    }

    [HttpPost("uploads"), RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string title, [FromForm] string category, [FromForm] string? description)
    {
        if (!AppConfig.Keys("walker.folders").Contains(category)) return Problem422("Pick a folder.");
        var path = $"uploads/{category}/{DateTime.UtcNow:yyyyMMddHHmmss}-{Path.GetFileName(file.FileName)}";
        await using (var s = file.OpenReadStream()) await files.SaveAsync(path, s);
        var u = new ReportUpload { Title = string.IsNullOrWhiteSpace(title) ? file.FileName : title, Category = category, Description = description, Path = path, OriginalName = file.FileName, Size = file.Length, UserId = Me.UserId };
        db.ReportUploads.Add(u);
        await db.SaveChangesAsync();
        return Ok(new { u.Id });
    }

    [HttpGet("uploads/{id:int}/download")]
    public async Task<IActionResult> UploadDownload(int id)
    {
        var u = await db.ReportUploads.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null || !files.Exists(u.Path)) return NotFound(new { code = "not_found", message = "The file isn't in storage (sample data only has the record)." });
        return PhysicalFile(files.PathFor(u.Path), "application/octet-stream", u.OriginalName);
    }

    /// <summary>ETL feeds (config/walker.php etl): each feed with its schedule and recent files.</summary>
    [HttpGet("etl")]
    public async Task<IActionResult> Etl()
    {
        var uploads = await db.ReportUploads.Where(u => u.Category == "etl" || u.Category == "ercot").OrderByDescending(u => u.CreatedAt).Take(30).Select(u => new { u.Id, u.Title, u.OriginalName, u.Size, u.CreatedAt }).ToListAsync();
        var feeds = AppConfig.Get("walker")["etl"]!.AsObject().Select(kv => new
        {
            key = kv.Key, name = kv.Value!["name"]?.ToString(), direction = kv.Value["direction"]?.ToString(), party = kv.Value["party"]?.ToString(), schedule = kv.Value["schedule"]?.ToString(),
            format = kv.Value["format"]?.ToString(), file = kv.Value["file"]?.ToString(), target = kv.Value["target"]?.ToString(),
            status = kv.Key.Contains("utilibill") || kv.Key.Contains("lockbox") ? "Skipped (keys not set)" : "Waiting for files",
        });
        return Ok(new { feeds, uploads });
    }
}
