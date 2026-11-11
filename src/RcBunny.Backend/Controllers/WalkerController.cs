using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Walker;

namespace RcBunny.Controllers;

/// <summary>
/// Walker: reporting and files, the same screens as ebunny's Walker. Report Status (every run, live), Run a Report (the
/// library, each run in the background and saved as a file), Saved Reports, the Orders, Notes and Phonecalls reports,
/// Files (by folder, with previews) and the ETL feeds.
/// </summary>
[ApiController]
[Route("api/walker")]
[RequirePermission("walker")]
public class WalkerController(AppDbContext db, IServiceScopeFactory scopes, IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    private string Storage => Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage"));
    private async Task<Permissions.SignedIn> Me() => (await Permissions.CurrentAsync(HttpContext))!;

    // ---- Report Status ----------------------------------------------------------------------------------------

    private static readonly string[] Tables = ["running", "problem", "done"];

    private static IQueryable<ReportRun> InState(IQueryable<ReportRun> q, string state)
    {
        var cut = DateTime.UtcNow.AddMinutes(-ReportRun.TimeoutMinutes);
        return state switch
        {
            "running" => q.Where(r => (r.Status == "queued" || r.Status == "running") && (r.StartedAt ?? r.CreatedAt) >= cut),
            "done" => q.Where(r => r.Status == "done"),
            _ => q.Where(r => r.Status == "failed" || ((r.Status == "queued" || r.Status == "running") && (r.StartedAt ?? r.CreatedAt) < cut)),
        };
    }

    private static object RunRow(ReportRun r) => new
    {
        r.Id, r.Report, title = r.Title ?? r.Report, r.Model, by = r.User?.Name, app = r.App, r.CreatedAt, r.StartedAt, r.FinishedAt, r.DurationMs, r.Rows, r.Status, state = r.State,
        r.Error, file = r.File != null, format = Reports.Formats.GetValueOrDefault(r.Format).Label ?? r.Format, r.Summary, rerun = Reports.Find(r.Report) != null,
        @params = r.Params == null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(r.Params),
    };

    [HttpGet("runs")]
    public async Task<object> Status(int running_page = 1, int problem_page = 1, int done_page = 1)
    {
        var pages = new Dictionary<string, int> { ["running"] = running_page, ["problem"] = problem_page, ["done"] = done_page };
        var tables = new Dictionary<string, object>();
        foreach (var t in Tables)
        {
            var q = InState(db.ReportRuns.Include(r => r.User), t);
            var size = t == "running" ? 50 : 15;
            tables[t] = new { total = await q.CountAsync(), page = pages[t], size, runs = (await q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip((Math.Max(pages[t], 1) - 1) * size).Take(size).ToListAsync()).Select(RunRow) };
        }
        return tables;
    }

    [HttpGet("runs/{id:int}")]
    public async Task<IActionResult> Run(int id)
    {
        var run = await db.ReportRuns.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
        if (run == null) return NotFound();
        var def = Reports.Find(run.Report);
        var saved = run.SavedReportId == null ? null : await db.SavedReports.FindAsync(run.SavedReportId);
        return Ok(new
        {
            run = RunRow(run), def = def == null ? null : new { def.Key, def.Title, def.Description }, columns = def?.Columns ?? [],
            fileName = run.File == null ? null : Path.GetFileName(run.File), folder = run.File == null ? null : Path.GetDirectoryName(run.File) + "/", run.FileSize, run.RerunOf,
            preview = run.Preview == null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(run.Preview), savedReport = saved == null ? null : new { saved.Id, saved.Name },
            history = (await db.ReportRuns.Include(r => r.User).Where(r => r.Report == run.Report && r.Id != id).OrderByDescending(r => r.Id).Take(8).ToListAsync()).Select(RunRow),
        });
    }

    [HttpPost("runs/{id:int}/rerun")]
    public async Task<IActionResult> Rerun(int id)
    {
        var run = await db.ReportRuns.FindAsync(id);
        if (run == null) return NotFound();
        if (Reports.Find(run.Report) == null) return BadRequest(new { error = "This report no longer exists." });
        var again = await Queue(run.Report, run.Params, run.Format, run.Summary, run.Id);
        return Ok(new { again.Id, message = $"Report #{run.Id} is running again as #{again.Id}." });
    }

    [HttpGet("runs/{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var run = await db.ReportRuns.FindAsync(id);
        var path = run?.File == null ? null : Path.Combine(Storage, run.File);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, Reports.Formats.GetValueOrDefault(run!.Format).Type ?? "application/octet-stream", Path.GetFileName(path));
    }

    private async Task<ReportRun> Queue(string key, string? @params, string format, bool summary, int? rerunOf = null, int? savedId = null)
    {
        var def = Reports.Find(key)!;
        var run = new ReportRun
        {
            UserId = (await Me()).Id, App = "walker", Report = key, Title = def.Title, Model = def.Model, Params = @params,
            Format = Reports.Formats.ContainsKey(format) ? format : "xlsx", Summary = summary, RerunOf = rerunOf, SavedReportId = savedId,
        };
        db.ReportRuns.Add(run);
        await db.SaveChangesAsync();
        Reports.Start(scopes, run.Id, Storage);
        return run;
    }

    // ---- Run a Report: the library ----------------------------------------------------------------------------

    [HttpGet("reports")]
    public async Task<object> Library()
    {
        var last = await db.ReportRuns.GroupBy(r => r.Report).Select(g => new { g.Key, runs = g.Count(), at = g.Max(r => r.CreatedAt) }).ToDictionaryAsync(x => x.Key);
        return Reports.All.GroupBy(r => r.Category).Select(g => new
        {
            category = g.Key, reports = g.Select(r => new { r.Key, r.Title, r.Model, r.Description, runs = last.GetValueOrDefault(r.Key)?.runs ?? 0, lastAt = last.GetValueOrDefault(r.Key)?.at }),
        });
    }

    [HttpGet("reports/{key}")]
    public async Task<IActionResult> Report(string key, int? saved)
    {
        if (Reports.Find(key) is not { } r) return NotFound();
        var me = await Me();
        var from = saved == null ? null : await db.SavedReports.FirstOrDefaultAsync(s => s.Id == saved && s.Report == key && (s.Shared || s.UserId == me.Id));
        var columns = await db.ReportColumns.FirstOrDefaultAsync(c => c.Report == key);
        var defaults = Reports.With(r, from?.Params).Values;
        return Ok(new
        {
            def = new { r.Key, r.Title, r.Model, r.Description }, filters = await r.Filters(db), defaults, columns = r.Columns,
            from = from == null ? null : new { from.Id, from.Name, from.Format, from.Summary },
            export = columns == null ? [] : JsonSerializer.Deserialize<List<Reports.ExportColumn>>(columns.Columns)!,
            exportBy = columns?.UserId is { } u ? await db.Users.Where(x => x.Id == u).Select(x => x.Name).FirstOrDefaultAsync() : null, exportAt = columns?.UpdatedAt,
            fields = await Reports.FieldsOf(db, r),
            runs = (await db.ReportRuns.Include(x => x.User).Where(x => x.Report == key).OrderByDescending(x => x.Id).Take(10).ToListAsync()).Select(RunRow),
            formats = Reports.Formats.ToDictionary(f => f.Key, f => f.Value.Label),
        });
    }

    public record RunInput(Dictionary<string, JsonElement>? Params, string? Format, bool Summary);

    /// <summary>The filters as given, without empty ones.</summary>
    private static string Clean(Dictionary<string, JsonElement>? p) => JsonSerializer.Serialize((p ?? []).Where(kv =>
        !(kv.Value.ValueKind == JsonValueKind.Null || (kv.Value.ValueKind == JsonValueKind.String && kv.Value.GetString() == "") || (kv.Value.ValueKind == JsonValueKind.Array && kv.Value.GetArrayLength() == 0)))
        .ToDictionary());

    [HttpPost("reports/{key}")]
    public async Task<IActionResult> RunReport(string key, RunInput input)
    {
        if (Reports.Find(key) is not { } r) return NotFound();
        if (input.Params?.TryGetValue("start", out var s) == true && input.Params.TryGetValue("end", out var e) && DateOnly.TryParse(s.ToString(), out var sd) && DateOnly.TryParse(e.ToString(), out var ed) && ed < sd)
            return BadRequest(new { error = "The end date can't be before the start." });
        var run = await Queue(key, Clean(input.Params), input.Format ?? "xlsx", input.Summary);
        return Ok(new { run.Id, message = $"{r.Title}{(input.Summary ? " summary" : "")} is running in the background." });
    }

    public record ColumnsInput(List<ColumnRow>? Cols, string? KeyArray, bool Reset);
    public record ColumnRow(string? Key, string? Title, int? Order);

    /// <summary>Export Columns: which keys go in the file, their titles and order. key_array takes the same as nested JSON.</summary>
    [HttpPost("reports/{key}/columns")]
    public async Task<IActionResult> SaveColumns(string key, ColumnsInput input)
    {
        if (Reports.Find(key) == null) return NotFound();
        var row = await db.ReportColumns.FirstOrDefaultAsync(c => c.Report == key);
        if (input.Reset)
        {
            if (row != null) db.ReportColumns.Remove(row);
            await db.SaveChangesAsync();
            return Ok(new { message = "Export columns reset to the report's own columns." });
        }
        List<(string Key, string Title)> cols;
        if (!string.IsNullOrWhiteSpace(input.KeyArray))
        {
            try { cols = Reports.Flatten(JsonSerializer.Deserialize<JsonElement>(input.KeyArray)); }
            catch { return BadRequest(new { error = "That isn't a key array. Use JSON like {\"customer\": {\"name\": \"Name\"}, \"status\": \"Status\"}." }); }
        }
        else
        {
            if ((input.Cols ?? []).Any(c => !string.IsNullOrEmpty(c.Key) && !Regex.IsMatch(c.Key, @"^[A-Za-z0-9_.*]+$"))) return BadRequest(new { error = "Model keys are letters, numbers, dots and *." });
            cols = (input.Cols ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Key)).OrderBy(c => c.Order ?? 0).Select(c => (c.Key!.Trim(), string.IsNullOrWhiteSpace(c.Title) ? c.Key!.Trim() : c.Title.Trim())).ToList();
        }
        if (cols.Count == 0) return BadRequest(new { error = "Add at least one column." });
        if (row == null) db.ReportColumns.Add(row = new ReportColumn { Report = key });
        (row.Columns, row.UserId, row.UpdatedAt) = (JsonSerializer.Serialize(cols.Select(c => new Reports.ExportColumn(c.Key, c.Title))), (await Me()).Id, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{cols.Count} export {(cols.Count == 1 ? "column" : "columns")} saved. Every run of this report uses them." });
    }

    // ---- Saved Reports ----------------------------------------------------------------------------------------

    [HttpGet("saved")]
    public async Task<object> Saved()
    {
        var me = await Me();
        var runs = await db.ReportRuns.Where(r => r.SavedReportId != null).GroupBy(r => r.SavedReportId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        return (await db.SavedReports.Include(s => s.User).Where(s => s.Shared || s.UserId == me.Id).ToListAsync())
            .OrderByDescending(s => s.UserId == me.Id).ThenBy(s => s.Name).Select(s => new
            {
                s.Id, s.Name, s.Shared, s.Report, title = Reports.Find(s.Report)?.Title ?? s.Report, format = Reports.Formats.GetValueOrDefault(s.Format).Label ?? s.Format, s.Summary,
                settings = Settings(s.Params), by = s.UserId == me.Id ? "You" : s.User?.Name, runs = runs.GetValueOrDefault(s.Id), s.LastRunAt,
            });
    }

    private static string Settings(string? json) => json == null ? "" : string.Join(" · ", JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!
        .Select(kv => $"{Regex.Replace(kv.Key, "_", " ")}: {(kv.Value.ValueKind == JsonValueKind.Array ? string.Join(", ", kv.Value.EnumerateArray()) : kv.Value.ToString())}"));

    [HttpGet("saved/{id:int}")]
    public async Task<IActionResult> SavedShow(int id, int page = 1)
    {
        var me = await Me();
        var s = await db.SavedReports.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id && (x.Shared || x.UserId == me.Id));
        if (s == null) return NotFound();
        var def = Reports.Find(s.Report);
        var runs = db.ReportRuns.Include(r => r.User).Where(r => r.SavedReportId == id);
        return Ok(new
        {
            saved = new { s.Id, s.Name, s.Report, s.Shared, s.Summary, format = Reports.Formats.GetValueOrDefault(s.Format).Label ?? s.Format, s.CreatedAt, s.LastRunAt, mine = s.UserId == me.Id,
                by = s.User?.Name, @params = s.Params == null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(s.Params) },
            def = def == null ? null : new { def.Key, def.Title }, canRemove = s.UserId == me.Id || me.Permissions.Contains("sheriff"),
            total = await runs.CountAsync(), page, runs = (await runs.OrderByDescending(r => r.Id).Skip((Math.Max(page, 1) - 1) * 10).Take(10).ToListAsync()).Select(RunRow),
        });
    }

    public record SaveInput(string? Name, string? Report, int? Run, bool Shared, string? Format, bool Summary, Dictionary<string, JsonElement>? Params);

    /// <summary>Save the report form's settings (Save Report), or a past run's.</summary>
    [HttpPost("saved")]
    public async Task<IActionResult> Save(SaveInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Give the saved report a name." });
        if (input.Report == null || Reports.Find(input.Report) == null) return NotFound();
        string? @params; string format; bool summary;
        if (input.Run is { } runId)
        {
            var run = await db.ReportRuns.FirstOrDefaultAsync(r => r.Id == runId && r.Report == input.Report);
            if (run == null) return NotFound();
            (@params, format, summary) = (run.Params, run.Format, run.Summary);
        }
        else (@params, format, summary) = (Clean(input.Params), input.Format ?? "xlsx", input.Summary);
        var s = new SavedReport { UserId = (await Me()).Id, Name = input.Name.Trim(), Report = input.Report, Params = @params, Format = format, Summary = summary, Shared = input.Shared };
        db.SavedReports.Add(s);
        await db.SaveChangesAsync();
        return Ok(new { s.Id, message = $"\"{s.Name}\" saved. Find it under Saved Reports to rerun any time." });
    }

    [HttpPost("saved/{id:int}/run")]
    public async Task<IActionResult> RunSaved(int id)
    {
        var me = await Me();
        var s = await db.SavedReports.FirstOrDefaultAsync(x => x.Id == id && (x.Shared || x.UserId == me.Id));
        if (s == null) return NotFound();
        if (Reports.Find(s.Report) == null) return BadRequest(new { error = "This report no longer exists." });
        var run = await Queue(s.Report, s.Params, s.Format, s.Summary, savedId: s.Id);
        s.LastRunAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { run.Id, message = $"\"{s.Name}\" is running in the background as #{run.Id}." });
    }

    [HttpDelete("saved/{id:int}")]
    public async Task<IActionResult> DeleteSaved(int id)
    {
        var me = await Me();
        var s = await db.SavedReports.FindAsync(id);
        if (s == null) return NotFound();
        if (s.UserId != me.Id && !me.Permissions.Contains("sheriff")) return StatusCode(403, new { error = "Only the person who saved it can remove it." });
        await db.ReportRuns.Where(r => r.SavedReportId == id).ExecuteUpdateAsync(u => u.SetProperty(r => r.SavedReportId, (int?)null));
        db.SavedReports.Remove(s);
        await db.SaveChangesAsync();
        return Ok(new { message = $"\"{s.Name}\" removed from Saved Reports. Its past runs are kept." });
    }

    // ---- Orders, Notes and Phonecalls (ebunny's customer service reports, run here and listed as your runs) -----

    [HttpGet("activity/{key}")]
    public async Task<IActionResult> Activity(string key)
    {
        if (key is not ("orders" or "notes" or "phonecalls") || Reports.Find(key) is not { } r) return NotFound();
        var me = await Me();
        return Ok(new
        {
            def = new { r.Key, r.Title }, defaults = Reports.With(r, null).Values, formats = Reports.Formats.ToDictionary(f => f.Key, f => f.Value.Label),
            statuses = Corral.Statuses.Select(s => s.Status), priorities = Corral.Priorities.Append("System"),
            users = await db.Users.OrderBy(u => u.Name).Select(u => new { u.Id, u.Name }).ToListAsync(),
            recent = (await db.ReportRuns.Where(x => x.UserId == me.Id && x.Report == key).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(10).ToListAsync()).Select(RunRow),
        });
    }

    // ---- Files ------------------------------------------------------------------------------------------------

    public static readonly string[] Categories = ["ETL", "Legal Documents", "Customer Templates", "Finance", "Utility / ERCOT", "Vendor", "Marketing", "Operations", "Regulatory", "Other"];
    public static readonly Dictionary<string, string> Folders = new() { ["etl"] = "ETL", ["legal"] = "Legal Documents", ["templates"] = "Customer Templates", ["finance"] = "Finance", ["ercot"] = "Utility / ERCOT", ["vendor"] = "Vendor" };
    private static readonly string[] Allowed = [".xlsx", ".xls", ".csv", ".pdf", ".txt", ".zip"];

    [HttpGet("uploads")]
    public async Task<object> Uploads(string? category, string? folder, int page = 1)
    {
        if (folder != null) category = Folders.GetValueOrDefault(folder);
        var users = await db.Users.ToDictionaryAsync(u => u.Id, u => u.Name);
        var q = db.ReportUploads.Where(u => category == null || u.Category == category);
        return new
        {
            category, total = await q.CountAsync(), page,
            counts = await db.ReportUploads.GroupBy(u => u.Category).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n),
            categories = Categories, folders = Folders,
            uploads = (await q.OrderByDescending(u => u.CreatedAt).Skip((Math.Max(page, 1) - 1) * 25).Take(25).ToListAsync())
                .Select(u => new { u.Id, u.Title, u.Description, u.Category, u.OriginalName, u.Size, u.CreatedAt, by = u.UserId is { } x ? users.GetValueOrDefault(x) : null }),
            canDelete = (await Me()).Permissions.Contains("delete"),
        };
    }

    [HttpGet("uploads/{id:int}")]
    public async Task<IActionResult> Upload(int id)
    {
        var u = await db.ReportUploads.FindAsync(id);
        if (u == null) return NotFound();
        var path = Path.Combine(Storage, u.Path);
        var exists = System.IO.File.Exists(path);
        var ext = Path.GetExtension(u.OriginalName).TrimStart('.').ToLowerInvariant();
        object? preview = null;
        if (exists && ext == "csv")
        {
            var lines = (await System.IO.File.ReadAllLinesAsync(path)).Take(51);
            preview = new { type = "table", rows = lines.Select(SplitCsv) };
        }
        else if (exists && ext == "txt") preview = new { type = "text", text = (await System.IO.File.ReadAllTextAsync(path)) is var t && t.Length > 20000 ? t[..20000] : t };
        else if (exists && ext == "pdf") preview = new { type = "pdf" };
        else if (exists && ext is "png" or "jpg" or "jpeg" or "gif") preview = new { type = "image" };
        return Ok(new
        {
            upload = u, ext, exists, preview, by = u.UserId is { } x ? await db.Users.Where(y => y.Id == x).Select(y => y.Name).FirstOrDefaultAsync() : null,
            folder = Folders.FirstOrDefault(f => f.Value == u.Category).Key, categories = Categories, canDelete = (await Me()).Permissions.Contains("delete"),
            siblings = await db.ReportUploads.Where(s => s.Category == u.Category && s.Id != id).OrderByDescending(s => s.CreatedAt).Take(10).Select(s => new { s.Id, s.Title, s.OriginalName, s.CreatedAt }).ToListAsync(),
        });
    }

    private static string[] SplitCsv(string line)
    {
        var cells = new List<string>();
        var cur = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted && ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
            else if (ch == '"') quoted = !quoted;
            else if (ch == ',' && !quoted) { cells.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(ch);
        }
        cells.Add(cur.ToString());
        return cells.ToArray();
    }

    [HttpPost("uploads")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> AddUpload([FromForm] string? title, [FromForm] string? category, [FromForm] string? description, IFormFile? file)
    {
        if (string.IsNullOrWhiteSpace(title)) return BadRequest(new { error = "Give the file a title." });
        if (!Categories.Contains(category)) return BadRequest(new { error = "Pick a folder." });
        if (file == null || file.Length == 0) return BadRequest(new { error = "Choose a file." });
        if (!Allowed.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) return BadRequest(new { error = "Excel, CSV, PDF, text or zip files only." });
        var u = new ReportUpload { Title = title.Trim(), Category = category!, Description = Blank(description), UserId = (await Me()).Id };
        await Store(u, file);
        db.ReportUploads.Add(u);
        await db.SaveChangesAsync();
        return Ok(new { u.Id, message = $"{u.Title} uploaded" });
    }

    [HttpPut("uploads/{id:int}")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UpdateUpload(int id, [FromForm] string? title, [FromForm] string? category, [FromForm] string? description, IFormFile? file)
    {
        var u = await db.ReportUploads.FindAsync(id);
        if (u == null) return NotFound();
        if (string.IsNullOrWhiteSpace(title) || !Categories.Contains(category)) return BadRequest(new { error = "Give the file a title and a folder." });
        if (file is { Length: > 0 } && !Allowed.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) return BadRequest(new { error = "Excel, CSV, PDF, text or zip files only." });
        (u.Title, u.Category, u.Description, u.UpdatedAt, u.UserId) = (title.Trim(), category!, Blank(description), DateTime.UtcNow, (await Me()).Id);
        if (file is { Length: > 0 }) await Store(u, file);
        await db.SaveChangesAsync();
        return Ok(new { message = file is { Length: > 0 } ? "New version saved" : "Saved" });
    }

    private async Task Store(ReportUpload u, IFormFile file)
    {
        var name = Regex.Replace(Path.GetFileName(file.FileName), @"[^A-Za-z0-9._-]", "-");
        u.Path = $"report-uploads/{Guid.NewGuid():N}-{name}";
        u.OriginalName = Path.GetFileName(file.FileName);
        u.Size = file.Length;
        Directory.CreateDirectory(Path.Combine(Storage, "report-uploads"));
        await using var s = System.IO.File.Create(Path.Combine(Storage, u.Path));
        await file.CopyToAsync(s);
    }

    [HttpDelete("uploads/{id:int}")]
    public async Task<IActionResult> DeleteUpload(int id)
    {
        var u = await db.ReportUploads.FindAsync(id);
        if (u == null) return NotFound();
        if (!(await Me()).Permissions.Contains("delete")) return StatusCode(403, new { error = "Deleting needs the delete right." });
        var path = Path.Combine(Storage, u.Path);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        db.ReportUploads.Remove(u);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{u.Title} deleted" });
    }

    [HttpGet("uploads/{id:int}/download")]
    public async Task<IActionResult> DownloadUpload(int id) => await FileOf(id, true);

    [HttpGet("uploads/{id:int}/view")]
    public async Task<IActionResult> ViewUpload(int id) => await FileOf(id, false);

    private async Task<IActionResult> FileOf(int id, bool download)
    {
        var u = await db.ReportUploads.FindAsync(id);
        var path = u == null ? null : Path.Combine(Storage, u.Path);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        var type = Path.GetExtension(u!.OriginalName).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".csv" => "text/csv", ".txt" => "text/plain", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => "application/octet-stream" };
        return download ? PhysicalFile(path, type, u.OriginalName) : PhysicalFile(path, type);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ---- ETL feeds --------------------------------------------------------------------------------------------

    public record Feed(string Name, string Direction, string Party, string Schedule, string Cron, string Format, string File, string Target);

    /// <summary>Files that come in from, or go out to, other companies on a schedule (ebunny's config walker.etl). Runs are job runs "etl:&lt;key&gt;".</summary>
    public static readonly Dictionary<string, Feed> Feeds = new()
    {
        ["ercot-867"] = new("ERCOT 867_03 Usage Import", "In", "ERCOT (via market gateway)", "Hourly", "0 * * * *", "EDI X12 → CSV", "ercot_867_03_{date}_{hour}.csv", "Usage reads on each account"),
        ["ercot-814"] = new("ERCOT 814 Enrollment Responses", "In", "ERCOT (via market gateway)", "Every 10 minutes", "*/10 * * * *", "EDI X12", "ercot_814_{date}_{time}.edi", "Orders (accepted, rejected, switch dates)"),
        ["utilibill-bills"] = new("Utilibill Bill Export", "In", "Utilibill", "Daily 4:00 AM", "0 4 * * *", "CSV + PDF zip", "bills_{date}.zip", "Bills and bill PDFs (S3 bills/)"),
        ["lockbox"] = new("Bank Lockbox Payments", "In", "Sample Bank lockbox", "Weekdays 7:30 AM", "30 7 * * 1-5", "BAI2", "lockbox_{date}.bai", "Payments (check and money order)"),
        ["stripe-settlement"] = new("Card Settlement Report", "In", "Card processor", "Daily 6:15 AM", "15 6 * * *", "CSV", "settlement_{date}.csv", "Payment status and fees"),
        ["quickbooks-journal"] = new("Journal Export", "Out", "QuickBooks", "Daily 11:30 PM", "30 23 * * *", "IIF", "journal_{date}.iif", "Ledger entries for the day"),
        ["monthly-payment-letters"] = new("Monthly Payment Letters", "Out", "Print & mail vendor", "1st of the month 5:00 AM", "0 5 1 * *", "PDF batch + CSV manifest", "payment_letters_{month}.zip", "Customer Templates → Monthly Payment Reminder"),
        ["collections"] = new("Collections Placement", "Out", "Collections agency", "Mondays 8:00 AM", "0 8 * * 1", "Fixed-width text", "placements_{date}.txt", "Accounts 90+ days past due"),
    };

    [HttpGet("etl")]
    public async Task<object> Etl()
    {
        var since = DateTime.UtcNow.AddDays(-7);
        var runs = await db.JobRuns.Where(r => r.Job.StartsWith("etl:")).ToListAsync();
        return Feeds.Select(f =>
        {
            var mine = runs.Where(r => r.Job == "etl:" + f.Key).ToList();
            var last = mine.MaxBy(r => r.StartedAt);
            var week = mine.Where(r => r.StartedAt >= since).ToList();
            return new
            {
                key = f.Key, feed = f.Value, last = last == null ? null : new { last.StartedAt, status = last.StatusLabel },
                week = new { runs = week.Count, failed = week.Count(r => r.StatusLabel == "Failed"), warned = week.Count(r => r.StatusLabel == "Warning") },
            };
        });
    }

    [HttpGet("etl/{key}")]
    public async Task<IActionResult> EtlFeed(string key, string? status, int page = 1)
    {
        if (!Feeds.TryGetValue(key, out var feed)) return NotFound();
        var all = await db.JobRuns.Where(r => r.Job == "etl:" + key).OrderByDescending(r => r.StartedAt).ToListAsync();
        var list = all.Where(r => status == null || r.StatusLabel == status).ToList();
        return Ok(new
        {
            key, feed, counts = all.GroupBy(r => r.StatusLabel).ToDictionary(g => g.Key, g => g.Count()), lastOk = all.FirstOrDefault(r => r.StatusLabel == "OK")?.StartedAt,
            total = list.Count, page, runs = list.Skip((Math.Max(page, 1) - 1) * 25).Take(25).Select(r => new { r.Id, r.StartedAt, status = r.StatusLabel, r.Message, r.DurationMs }),
        });
    }
}
