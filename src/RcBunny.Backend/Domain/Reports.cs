using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend;

public sealed record ReportFilter(string Name, string Label, string Type, IReadOnlyList<string>? Options = null);

public sealed record ReportResult(IReadOnlyList<string> Columns, IReadOnlyList<object?[]> Rows, IReadOnlyList<string> SummaryColumns, IReadOnlyList<object?[]> Summary);

/// <summary>One Walker report (ebunny app/Reports/*Report.php): its filters and how it gets its rows.</summary>
public sealed class ReportDef
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public string? Model { get; init; }
    public required ReportFilter[] Filters { get; init; }
    public Func<Dictionary<string, string>>? Defaults { get; init; }
    public required Func<AppDbContext, Dictionary<string, string>, Task<ReportResult>> Run { get; init; }
}

public static class Reports
{
    private static string Today => DateTime.Today.ToString("yyyy-MM-dd");
    private static string YearStart => new DateTime(DateTime.Today.Year, 1, 1).ToString("yyyy-MM-dd");
    private static Dictionary<string, string> Range() => new() { ["start"] = YearStart, ["end"] = Today };
    private static ReportFilter[] DateRange => [new("start", "Start", "date"), new("end", "End", "date")];
    private static DateOnly D(Dictionary<string, string> f, string k, string fallback) => DateOnly.TryParse(f.GetValueOrDefault(k), out var d) ? d : DateOnly.Parse(fallback);
    private static DateTime Start(Dictionary<string, string> f) => D(f, "start", YearStart).ToDateTime(TimeOnly.MinValue);
    private static DateTime End(Dictionary<string, string> f) => D(f, "end", Today).ToDateTime(TimeOnly.MaxValue);
    private static List<string> Multi(Dictionary<string, string> f, string k) => (f.GetValueOrDefault(k) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>Counts by the first column (ebunny's default summary).</summary>
    private static (string[], List<object?[]>) CountBy(string label, IEnumerable<object?[]> rows, int col) =>
        ([label, "Rows"], rows.GroupBy(r => r[col]?.ToString() ?? "").Select(g => new object?[] { g.Key, g.Count() }).ToList());

    private static ReportDef Def(string key, ReportFilter[] filters, Func<AppDbContext, Dictionary<string, string>, Task<ReportResult>> run, Func<Dictionary<string, string>>? defaults = null)
    {
        var cfg = AppConfig.At($"walker.reports.{key}") as JsonObject;
        return new ReportDef
        {
            Key = key, Title = cfg?["title"]?.ToString() ?? key, Category = cfg?["category"]?.ToString() ?? "", Description = cfg?["description"]?.ToString() ?? "",
            Model = cfg?["model"]?.ToString(), Filters = filters, Defaults = defaults ?? Range, Run = run,
        };
    }

    private static readonly string[] Statuses = AppConfig.Keys("admin.customer_statuses").ToArray();

    public static readonly IReadOnlyList<ReportDef> All =
    [
        Def("orders", [.. DateRange, new("statuses", "Statuses", "multi", Statuses)], async (db, f) =>
        {
            var st = Multi(f, "statuses");
            var s = Start(f); var e = End(f);
            var q = db.Customers.Include(c => c.Plan).Include(c => c.Market).Where(c => c.CreatedAt >= s && c.CreatedAt <= e);
            if (st.Count > 0) q = q.Where(c => st.Contains(c.Status));
            var rows = (await q.OrderBy(c => c.CreatedAt).ToListAsync()).Select(c => new object?[] { c.Account, c.CreatedAt.ToString("yyyy-MM-dd"), c.Status, c.Exception, c.Name, c.Type, c.Phone, c.Email, c.Address, c.City, c.Zip, c.Market?.Name, c.Esiid, c.Plan?.Internal, c.Source, c.Msid }).ToList();
            var (sc, sr) = CountBy("Status", rows, 2);
            return new(["Account", "Created", "Status", "Exception", "Customer", "Type", "Phone", "Email", "Address", "City", "Zip", "Market", "ESIID", "Plan", "Source", "MSID"], rows, sc, sr);
        }, () => new() { ["start"] = YearStart, ["end"] = Today, ["statuses"] = string.Join(",", Statuses) }),
        Def("notes", [.. DateRange, new("author", "Agent", "text"), new("priority", "Priority", "select", ["Low", "Medium", "High"]), new("text", "Text contains", "text")], async (db, f) =>
        {
            var s = Start(f); var e = End(f);
            var q = db.Notes.Include(n => n.Customer).Where(n => n.CreatedAt >= s && n.CreatedAt <= e);
            if (f.GetValueOrDefault("author") is { Length: > 0 } a) q = q.Where(n => n.Author.Contains(a));
            if (f.GetValueOrDefault("priority") is { Length: > 0 } p) q = q.Where(n => n.Priority == p);
            if (f.GetValueOrDefault("text") is { Length: > 0 } t) q = q.Where(n => n.Body.Contains(t));
            var rows = (await q.OrderBy(n => n.CreatedAt).ToListAsync()).Select(n => new object?[] { n.CreatedAt.ToString("yyyy-MM-dd HH:mm"), n.Customer?.Account, n.Author, n.Category, n.Action, n.Priority, n.Body }).ToList();
            var (sc, sr) = CountBy("Agent", rows, 2);
            return new(["Created", "Account", "Agent", "Category", "Action", "Priority", "Note"], rows, sc, sr);
        }),
        Def("phonecalls", [.. DateRange, new("phone", "Phone", "text"), new("direction", "Direction", "select", ["inbound", "outbound"])], async (db, f) =>
        {
            var s = Start(f); var e = End(f);
            var q = db.Phonecalls.Include(p => p.Customer).Include(p => p.User).Where(p => p.StartedAt >= s && p.StartedAt <= e);
            if (f.GetValueOrDefault("phone") is { Length: > 0 } ph) q = q.Where(p => p.Phone.Contains(ph));
            if (f.GetValueOrDefault("direction") is { Length: > 0 } d) q = q.Where(p => p.Direction == d);
            var rows = (await q.OrderBy(p => p.StartedAt).ToListAsync()).Select(p => new object?[] { p.StartedAt.ToString("yyyy-MM-dd HH:mm"), p.Customer?.Account, p.User?.Name, p.AgentId, p.Phone, p.Direction, p.DurationSec, p.Disposition }).ToList();
            var (sc, sr) = CountBy("Direction", rows, 5);
            return new(["Started", "Account", "Agent", "Agent ID", "Phone", "Direction", "Seconds", "Disposition"], rows, sc, sr);
        }),
        Def("contacts", [.. DateRange, new("channel", "Channel", "select", ["Email", "SMS"])], async (db, f) =>
        {
            var s = Start(f); var e = End(f);
            var q = db.ContactLogs.Include(c => c.Customer).Where(c => c.CreatedAt >= s && c.CreatedAt <= e);
            if (f.GetValueOrDefault("channel") is { Length: > 0 } ch) q = q.Where(c => c.Channel == ch);
            var rows = (await q.OrderBy(c => c.CreatedAt).ToListAsync()).Select(c => new object?[] { c.SentAt?.ToString("yyyy-MM-dd HH:mm"), c.Customer?.Account, c.Channel, c.Template, c.Status, c.OpenedAt != null, c.ClickedAt != null, c.DroppedAt != null }).ToList();
            var (sc, sr) = CountBy("Template", rows, 3);
            return new(["Sent", "Account", "Channel", "Template", "Status", "Opened", "Clicked", "Bounced"], rows, sc, sr);
        }),
        Def("exceptions", [new("queue", "Queue", "select", AppConfig.Keys("admin.queues").ToArray()), new("open", "Only open", "select", ["yes", "no"])], async (db, f) =>
        {
            var q = db.WorkItems.Include(w => w.Customer).AsQueryable();
            if (f.GetValueOrDefault("queue") is { Length: > 0 } qu) q = q.Where(w => w.Queue == qu);
            if (f.GetValueOrDefault("open") != "no") q = q.Where(w => w.ResolvedAt == null);
            var now = DateTime.UtcNow;
            var rows = (await q.OrderBy(w => w.CreatedAt).ToListAsync()).Select(w => new object?[] { w.Queue, w.Customer?.Account, w.Customer?.Name, w.Summary, w.CreatedAt.ToString("yyyy-MM-dd"), (int)((w.ResolvedAt ?? now) - w.CreatedAt).TotalDays, w.ResolvedAt?.ToString("yyyy-MM-dd") }).ToList();
            var (sc, sr) = CountBy("Queue", rows, 0);
            return new(["Queue", "Account", "Customer", "Summary", "Opened", "Days Open", "Resolved"], rows, sc, sr);
        }, () => new() { ["open"] = "yes" }),
        Def("payments", [.. DateRange, new("status", "Status", "select", ["Success", "Pending", "Failed", "Reversed"]), new("source", "Source", "text")], async (db, f) =>
        {
            var s = D(f, "start", YearStart); var e = D(f, "end", Today);
            var q = db.Payments.Include(p => p.Customer).Where(p => p.PaidOn >= s && p.PaidOn <= e);
            if (f.GetValueOrDefault("status") is { Length: > 0 } st) q = q.Where(p => p.Status == st);
            if (f.GetValueOrDefault("source") is { Length: > 0 } so) q = q.Where(p => p.Source == so);
            var list = await q.OrderBy(p => p.PaidOn).ToListAsync();
            var rows = list.Select(p => new object?[] { p.PaidOn.ToString("yyyy-MM-dd"), p.Customer?.Account, p.Customer?.Name, p.Amount, p.Method, p.Source, p.Kind, p.Status, p.Reference }).ToList();
            var summary = list.GroupBy(p => (p.Status, p.Method)).Select(g => new object?[] { g.Key.Status, g.Key.Method, g.Count(), Math.Round(g.Sum(p => p.Amount), 2) }).ToList();
            return new(["Paid On", "Account", "Customer", "Amount", "Method", "Source", "Kind", "Status", "Reference"], rows, ["Status", "Method", "Payments", "Amount"], summary);
        }),
        Def("bills", DateRange, async (db, f) =>
        {
            var s = D(f, "start", YearStart); var e = D(f, "end", Today);
            var list = await db.Bills.Include(b => b.Customer).Where(b => b.BilledOn >= s && b.BilledOn <= e).OrderBy(b => b.BilledOn).ToListAsync();
            var rows = list.Select(b => new object?[] { b.BilledOn.ToString("yyyy-MM-dd"), b.Customer?.Account, b.Customer?.Name, b.Invoice, b.Kwh, b.Amount, b.DueOn?.ToString("yyyy-MM-dd"), b.AmountPaid }).ToList();
            var summary = list.GroupBy(b => b.BilledOn.ToString("yyyy-MM")).Select(g => new object?[] { g.Key, g.Count(), g.Sum(b => b.Kwh), Math.Round(g.Sum(b => b.Amount), 2) }).ToList();
            return new(["Billed", "Account", "Customer", "Invoice", "kWh", "Amount", "Due", "Paid"], rows, ["Month", "Bills", "kWh", "Amount"], summary);
        }),
        Def("receivables", [new("as_of", "As Of", "date"), new("min", "Minimum Balance ($)", "text")], async (db, f) =>
        {
            var asOf = D(f, "as_of", Today);
            var min = decimal.TryParse(f.GetValueOrDefault("min"), out var m) ? m : 0.01m;
            var list = (await db.Customers.Include(c => c.Market).ToListAsync()).Where(c => c.Balance >= min).OrderByDescending(c => c.Balance).ToList();
            var rows = list.Select(c => { var days = DaysPastDue(c, asOf); return new object?[] { c.Account, c.Name, c.Status, c.Market?.Name, c.Balance, c.DueDate?.ToString("yyyy-MM-dd"), days, Bucket(days) }; }).ToList();
            var summary = Buckets.Select(b => { var g = list.Where(c => Bucket(DaysPastDue(c, asOf)) == b).ToList(); return new object?[] { b, g.Count, Math.Round(g.Sum(c => c.Balance), 2) }; }).ToList();
            return new(["Account", "Customer", "Status", "Market", "Balance", "Due Date", "Days Past Due", "Aging"], rows, ["Aging", "Accounts", "Balance"], summary);
        }, () => new() { ["as_of"] = Today, ["min"] = "0.01" }),
        Def("ercot", [.. DateRange, new("type", "Transaction", "text")], async (db, f) =>
        {
            var s = D(f, "start", YearStart); var e = D(f, "end", Today);
            var q = db.ErcotTransactions.Include(t => t.Customer).Where(t => t.TransDate >= s && t.TransDate <= e);
            if (f.GetValueOrDefault("type") is { Length: > 0 } ty) q = q.Where(t => t.TransType == ty);
            var rows = (await q.OrderBy(t => t.TransDate).ToListAsync()).Select(t => new object?[] { t.TransDate.ToString("yyyy-MM-dd"), t.Customer?.Account, t.Esiid, t.TransType, t.Label, t.Purpose, t.Tracking, t.Status }).ToList();
            var (sc, sr) = CountBy("Transaction", rows, 3);
            return new(["Date", "Account", "ESIID", "Transaction", "Label", "Purpose", "Tracking", "Status"], rows, sc, sr);
        }),
        Def("stars", DateRange, async (db, f) =>
        {
            var s = Start(f); var e = End(f);
            var list = await db.StarEntries.Include(x => x.Customer).Where(x => x.CreatedAt >= s && x.CreatedAt <= e).OrderBy(x => x.CreatedAt).ToListAsync();
            var rows = list.Select(x => new object?[] { x.CreatedAt.ToString("yyyy-MM-dd"), x.Customer?.Account, x.Customer?.Name, x.Reason, x.Stars, x.Fulfillment }).ToList();
            var summary = list.GroupBy(x => x.Stars < 0 ? "Spent" : "Earned").Select(g => new object?[] { g.Key, g.Count(), g.Sum(x => x.Stars) }).ToList();
            return new(["Date", "Account", "Customer", "Reason", "Stars", "Fulfillment"], rows, ["Type", "Entries", "Stars"], summary);
        }),
    ];

    public static readonly string[] Buckets = ["Current", "1-30", "31-60", "61-90", "90+"];
    public static int DaysPastDue(Customer c, DateOnly asOf) => c.DueDate is { } d ? Math.Max(0, asOf.DayNumber - d.DayNumber) : 0;
    public static string Bucket(int days) => days == 0 ? "Current" : days <= 30 ? "1-30" : days <= 60 ? "31-60" : days <= 90 ? "61-90" : "90+";

    public static ReportDef? Get(string key) => All.FirstOrDefault(r => r.Key == key);

    /// <summary>Runs a queued report: query, apply saved Export Columns, write the file, keep a preview, notify the person.</summary>
    public static async Task ExecuteAsync(AppDbContext db, FileStore files, ReportRun run, CancellationToken ct)
    {
        var def = Get(run.Report);
        var started = DateTime.UtcNow;
        run.Status = "running"; run.StartedAt = started;
        await db.SaveChangesAsync(ct);
        try
        {
            if (def is null) throw new InvalidOperationException($"Unknown report {run.Report}.");
            var filters = (run.Params as JsonObject ?? []).ToDictionary(kv => kv.Key, kv => kv.Value is JsonArray a ? string.Join(",", a) : kv.Value?.ToString() ?? "");
            var result = await def.Run(db, filters);
            var (columns, rows) = (result.Columns, result.Rows);
            // Export Columns (Walker → report → Export Columns): pick and rename columns
            if (await db.ReportColumns.FirstOrDefaultAsync(c => c.Report == run.Report, ct) is { Columns: JsonArray map } && map.Count > 0)
            {
                var idx = map.Select(m => columns.ToList().IndexOf(m!["key"]?.ToString() ?? "")).ToList();
                columns = map.Select(m => m!["title"]?.ToString() ?? "").ToList();
                rows = rows.Select(r => idx.Select(i => i >= 0 ? r[i] : null).ToArray()).ToList();
            }
            var (outCols, outRows) = run.Summary ? (result.SummaryColumns, result.Summary) : (columns, rows);
            var (bytes, _, ext) = TableExport.Write(run.Format, outCols, outRows, def.Title);
            var path = $"reports/{run.UserId}/{run.Report}-{started:yyyyMMdd-HHmmss}-{run.Id}.{ext}";
            await files.SaveAsync(path, bytes);
            run.File = path; run.FileSize = bytes.Length; run.Rows = outRows.Count; run.Status = "completed";
            run.Preview = new JsonObject
            {
                ["columns"] = new JsonArray(outCols.Select(c => (JsonNode?)c).ToArray()),
                ["rows"] = new JsonArray(outRows.Take(20).Select(r => (JsonNode?)new JsonArray(r.Select(v => v is null ? null : (JsonNode?)JsonValue.Create(v is DateTime dt ? dt.ToString("yyyy-MM-dd HH:mm") : v is DateOnly d0 ? d0.ToString("yyyy-MM-dd") : v.ToString())).ToArray())).ToArray()),
            };
        }
        catch (Exception e)
        {
            run.Status = "failed"; run.Error = e.Message;
        }
        run.FinishedAt = DateTime.UtcNow; run.DurationMs = (int)(run.FinishedAt.Value - started).TotalMilliseconds;
        db.AdminNotifications.Add(new AdminNotification { UserId = run.UserId, Kind = "report", Title = run.Status == "completed" ? $"Report ready: {run.Title}" : $"Report failed: {run.Title}", Body = run.Error, Url = $"/walker/runs/{run.Id}" });
        await db.SaveChangesAsync(ct);
    }
}
