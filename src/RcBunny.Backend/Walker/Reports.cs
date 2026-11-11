using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Walker;

/// <summary>A report's filter: name, label, type (date, text, select, multi) and the choices for select and multi.</summary>
public record Filter(string Name, string Label, string Type, Dictionary<string, string>? Options = null);

/// <summary>A report's settings: filter name → a string or a list of strings.</summary>
public class Params(Dictionary<string, JsonElement> values)
{
    public Dictionary<string, JsonElement> Values { get; } = values;
    public string? Str(string k) => Values.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;
    public DateOnly Date(string k, DateOnly fallback) => DateOnly.TryParse(Str(k), out var d) ? d : fallback;
    public string[] List(string k) => Values.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.ToString()).ToArray() : [];
}

/// <summary>
/// One report (ebunny's app/Reports): what it reads (its model), its filters and defaults, its records, the columns and
/// row for each record, and a summary (counts). Records are field → value, with the account under "customer", so export
/// columns can name any key on them (customer.name, amount…).
/// </summary>
public class Report
{
    public required string Key, Title, Model, Category, Description;
    public required Func<AppDbContext, Task<List<Filter>>> Filters;
    public Func<Dictionary<string, object>> Defaults = () => new() { ["start"] = new DateOnly(DateTime.Today.Year, 1, 1).ToString("yyyy-MM-dd"), ["end"] = Today };
    public required Func<AppDbContext, Params, Task<List<Dictionary<string, object?>>>> Query;
    public required string[] Columns;
    public required Func<Dictionary<string, object?>, object?[]> Row;
    /// <summary>[header, rows]. Default: count by the first column.</summary>
    public Func<List<Dictionary<string, object?>>, (string[], List<object?[]>)>? Summary;

    public (string[] Header, List<object?[]> Rows) SummaryOf(List<Dictionary<string, object?>> records) => Summary?.Invoke(records)
        ?? ([Columns[0], "Rows"], records.Select(r => Row(r)[0]?.ToString() ?? "").GroupBy(v => v).Select(g => new object?[] { g.Key, g.Count() }).ToList());

    public static string Today => DateTime.Today.ToString("yyyy-MM-dd");
}

/// <summary>Walker's report library (ebunny's config/walker.php reports), and how a run is done.</summary>
public static class Reports
{
    /// <summary>format → (label, extension, content type), as ebunny's Export::FORMATS.</summary>
    public static readonly Dictionary<string, (string Label, string Ext, string Type)> Formats = new()
    {
        ["xlsx"] = ("Excel (.xlsx)", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        ["csv"] = ("CSV (.csv)", "csv", "text/csv"),
        ["txt"] = ("Tab-separated (.txt)", "txt", "text/plain"),
        ["json"] = ("JSON (.json)", "json", "application/json"),
    };

    // ---- Records: an entity's fields (snake_case, like ebunny's model keys) with its account under "customer" ----

    private static string Snake(string s) => Regex.Replace(s, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();
    private static readonly Regex Secret = new("(password|remember_token|ssn|secret|token)", RegexOptions.IgnoreCase);

    public static Dictionary<string, object?> Fields(object e)
    {
        var d = new Dictionary<string, object?>();
        foreach (var p in e.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (!(t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateOnly) || t == typeof(TimeOnly))) continue;
            if (Secret.IsMatch(p.Name) || p.GetIndexParameters().Length > 0) continue;
            d[Snake(p.Name)] = p.GetValue(e);
        }
        return d;
    }

    /// <summary>The records with each one's account (and its market and plan names) under "customer".</summary>
    private static async Task<List<Dictionary<string, object?>>> WithCustomers<T>(AppDbContext db, List<T> items, Func<T, int?> customerId, Action<T, Dictionary<string, object?>>? more = null) where T : class
    {
        var ids = items.Select(customerId).OfType<int>().Distinct().ToList();
        var customers = await db.Customers.Include(c => c.Market).Include(c => c.Plan).Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        return items.Select(i =>
        {
            var d = Fields(i);
            if (customerId(i) is { } id && customers.TryGetValue(id, out var c)) d["customer"] = Customer(c);
            more?.Invoke(i, d);
            return d;
        }).ToList();
    }

    private static Dictionary<string, object?> Customer(Customer c)
    {
        var d = Fields(c);
        d["market"] = c.Market?.Code;
        d["plan"] = c.Plan?.Code;
        return d;
    }

    /// <summary>A value at a key like "customer.name" (ebunny's data_get).</summary>
    public static object? Get(Dictionary<string, object?> r, string key)
    {
        object? cur = r;
        foreach (var part in key.Split('.'))
            cur = cur is Dictionary<string, object?> d && d.TryGetValue(part, out var v) ? v : null;
        return cur;
    }

    private static string? C(Dictionary<string, object?> r, string k) => Get(r, "customer." + k)?.ToString();
    private static string? D(object? v) => v switch { DateTime t => t.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), DateOnly d => d.ToString("yyyy-MM-dd"), _ => v?.ToString() };
    private static string? Day(object? v) => v switch { DateTime t => t.ToLocalTime().ToString("yyyy-MM-dd"), DateOnly d => d.ToString("yyyy-MM-dd"), _ => v?.ToString() };
    private static DateTime Start(Params f, string k, DateOnly fallback) => f.Date(k, fallback).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
    private static DateTime End(Params f, string k) => f.Date(k, DateOnly.FromDateTime(DateTime.Today)).AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
    private static DateOnly YearStart => new(DateTime.Today.Year, 1, 1);
    private static Dictionary<string, string> Same(IEnumerable<string> list) => list.ToDictionary(x => x, x => x);
    private static readonly string[] Buckets = ["Current", "1-30", "31-60", "61-90", "90+"];
    public static string Bucket(int days) => days == 0 ? "Current" : days <= 30 ? "1-30" : days <= 60 ? "31-60" : days <= 90 ? "61-90" : "90+";

    public static readonly Report[] All =
    [
        new()
        {
            Key = "orders", Title = "Orders", Model = "TicketCustomer_model", Category = "Customers", Description = "New orders (accounts) by order date and status.",
            Filters = _ => Task.FromResult(new List<Filter> { new("start", "Start", "date"), new("end", "End", "date"), new("statuses", "Statuses", "multi", Same(Corral.Statuses.Select(s => s.Status))) }),
            Defaults = () => new() { ["start"] = YearStart.ToString("yyyy-MM-dd"), ["end"] = Report.Today, ["statuses"] = Corral.Statuses.Select(s => s.Status).ToArray() },
            Query = async (db, f) =>
            {
                var statuses = f.List("statuses");
                var list = await db.Customers.Include(c => c.Market).Include(c => c.Plan).Where(c => c.CreatedAt >= Start(f, "start", YearStart) && c.CreatedAt < End(f, "end") && statuses.Contains(c.Status))
                    .OrderBy(c => c.CreatedAt).ToListAsync();
                return list.Select(Customer).ToList();
            },
            Columns = ["Account", "Created", "Status", "Exception", "Customer", "Type", "Phone", "Email", "Address", "City", "Zip", "Market", "ESIID", "Plan", "Source", "MSID"],
            Row = r => [r["account"], Day(r["created_at"]), r["status"], r["exception"], r["name"], r["type"], r["phone"], r["email"], r["address"], r["city"], r["zip"], r["market"], r["esiid"], r["plan"], r["source"], r["msid"]],
            Summary = rs => (["Status", "Orders"], rs.GroupBy(r => r["status"]?.ToString()).Select(g => new object?[] { g.Key, g.Count() }).ToList()),
        },
        new()
        {
            Key = "notes", Title = "Notes", Model = "ItemNote_model", Category = "Customers", Description = "Account notes by agent, date, text and priority.",
            Filters = async db => [new("user", "Username", "select", await db.Users.OrderBy(u => u.Name).ToDictionaryAsync(u => u.Id.ToString(), u => u.Name)), new("start", "Start", "date"), new("end", "End", "date"),
                new("contains", "Note Contains", "text"), new("priorities", "Priority", "multi", Same([.. Corral.Priorities, "System"]))],
            Defaults = () => new() { ["start"] = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd"), ["end"] = Report.Today, ["priorities"] = Corral.Priorities.Append("System").ToArray() },
            Query = async (db, f) =>
            {
                var wanted = f.List("priorities");
                var system = wanted.Contains("System");
                var q = db.Notes.Where(n => n.CreatedAt >= Start(f, "start", DateOnly.FromDateTime(DateTime.Today.AddDays(-30))) && n.CreatedAt < End(f, "end"))
                    .Where(n => wanted.Contains(n.Priority!) || (system && n.UserId == null));
                if (int.TryParse(f.Str("user"), out var uid)) q = q.Where(n => n.UserId == uid);
                if (f.Str("contains") is { } t) q = q.Where(n => n.Body.Contains(t));
                return await WithCustomers(db, await q.OrderByDescending(n => n.CreatedAt).ToListAsync(), n => n.CustomerId);
            },
            Columns = ["Date", "Account", "Customer", "Author", "Category", "Action", "Priority", "Note"],
            Row = r => [D(r["created_at"]), C(r, "account"), C(r, "name"), r["author"], r["category"], r["action"], r["priority"], r["body"]],
            Summary = rs => (["Author", "Category", "Notes"], rs.GroupBy(r => (A: r["author"]?.ToString() ?? "System", Cat: r["category"]?.ToString() ?? "—")).OrderBy(g => g.Key.A + g.Key.Cat)
                .Select(g => new object?[] { g.Key.A, g.Key.Cat, g.Count() }).ToList()),
        },
        new()
        {
            Key = "phonecalls", Title = "Phonecalls", Model = "ItemCall_model", Category = "Customers", Description = "Calls by date, account, agent id or phone number.",
            Filters = _ => Task.FromResult(new List<Filter> { new("start", "Start", "date"), new("end", "End", "date"), new("account", "Account Number", "text"), new("agent", "Agent Id", "text"), new("phone", "Phone Number", "text") }),
            Query = async (db, f) =>
            {
                var q = db.PhoneCalls.Include(c => c.User).Where(c => c.StartedAt >= Start(f, "start", YearStart) && c.StartedAt < End(f, "end"));
                if (f.Str("account") is { } a) q = q.Where(c => c.Customer!.Account == a.Trim());
                if (f.Str("agent") is { } ag) q = q.Where(c => c.AgentId == ag.Trim());
                var calls = await q.OrderByDescending(c => c.StartedAt).ToListAsync();
                // Phone numbers are stored formatted; compare digits only
                var digits = Regex.Replace(f.Str("phone") ?? "", @"\D", "");
                if (digits != "") calls = calls.Where(c => Regex.Replace(c.Phone, @"\D", "").Contains(digits)).ToList();
                return await WithCustomers(db, calls, c => c.CustomerId, (c, d) => d["agent"] = c.User?.Name);
            },
            Columns = ["Started", "Direction", "Phone", "Account", "Customer", "Agent Id", "Agent", "Seconds", "Disposition"],
            Row = r => [D(r["started_at"]), r["direction"], r["phone"], C(r, "account"), C(r, "name"), r["agent_id"], r["agent"], r["duration_sec"], r["resolution"]],
            Summary = rs => (["Agent Id", "Calls", "Inbound", "Outbound", "Minutes"], rs.GroupBy(r => r["agent_id"]?.ToString() ?? "—").OrderBy(g => g.Key)
                .Select(g => new object?[] { g.Key, g.Count(), g.Count(r => r["direction"] is "in" or "inbound"), g.Count(r => r["direction"] is "out" or "outbound"), (int)Math.Round(g.Sum(r => Convert.ToInt32(r["duration_sec"])) / 60.0) }).ToList()),
        },
        new()
        {
            Key = "contacts", Title = "Emails & Texts", Model = "ItemEmailSalesforce_model", Category = "Customers", Description = "Emails and texts sent to customers, with opens and clicks.",
            Filters = _ => Task.FromResult(new List<Filter> { new("start", "Start", "date"), new("end", "End", "date"), new("channel", "Channel", "select", new() { ["Email"] = "Email", ["SMS"] = "SMS" }) }),
            Query = async (db, f) =>
            {
                var q = db.ContactLogs.Where(l => l.CreatedAt >= Start(f, "start", YearStart) && l.CreatedAt < End(f, "end"));
                if (f.Str("channel") is { } ch) q = q.Where(l => l.Channel == ch);
                return await WithCustomers(db, await q.OrderBy(l => l.CreatedAt).ToListAsync(), l => l.CustomerId);
            },
            Columns = ["Date", "Account", "Channel", "Template", "Status", "Sent", "Opened", "Clicked"],
            Row = r => [D(r["created_at"]), C(r, "account"), r["channel"], r["template"], r["status"], D(r["sent_at"]), null, null],
            Summary = rs => (["Template", "Sent", "Opened", "Clicked"], rs.GroupBy(r => r["template"]?.ToString()).Select(g => new object?[] { g.Key, g.Count(), 0, 0 }).ToList()),
        },
        new()
        {
            Key = "exceptions", Title = "Exception Queues", Model = "TicketQueue_model", Category = "Customers", Description = "Work items in the exception queues and how long they have been open.",
            Filters = _ => Task.FromResult(new List<Filter> { new("queue", "Queue", "select", Corral.WorkQueues.ToDictionary(q => q.Key, q => q.Value.Name)), new("open", "Show", "select", new() { ["open"] = "Open only", ["all"] = "Open and fixed" }) }),
            Defaults = () => new() { ["open"] = "open" },
            Query = async (db, f) =>
            {
                var q = db.WorkItems.AsQueryable();
                if ((f.Str("open") ?? "open") == "open") q = q.Where(w => w.ResolvedAt == null);
                if (f.Str("queue") is { } k) q = q.Where(w => w.Queue == k);
                return await WithCustomers(db, await q.OrderBy(w => w.CreatedAt).ToListAsync(), w => w.CustomerId);
            },
            Columns = ["Queue", "Account", "Customer", "Summary", "Opened", "Days Open", "Fixed"],
            Row = r => [Corral.WorkQueues.TryGetValue(r["queue"]?.ToString() ?? "", out var q) ? q.Name : r["queue"], C(r, "account"), C(r, "name"), r["summary"], Day(r["created_at"]),
                (int)((r["resolved_at"] as DateTime? ?? DateTime.UtcNow) - (DateTime)r["created_at"]!).TotalDays, Day(r["resolved_at"])],
        },
        new()
        {
            Key = "payments", Title = "Payments", Model = "ItemPayment_model", Category = "Billing", Description = "Payments by date, status and source.",
            Filters = _ => Task.FromResult(new List<Filter> { new("start", "Start", "date"), new("end", "End", "date"), new("status", "Status", "select", Same(["Success", "Pending", "Failed", "Reversed"])), new("source", "Source", "text") }),
            Query = async (db, f) =>
            {
                var from = f.Date("start", YearStart); var to = f.Date("end", DateOnly.FromDateTime(DateTime.Today));
                var q = db.Payments.Where(p => p.PaidOn >= from && p.PaidOn <= to);
                if (f.Str("status") is { } s) q = q.Where(p => p.Status == s);
                if (f.Str("source") is { } src) q = q.Where(p => p.Source == src);
                return await WithCustomers(db, await q.OrderBy(p => p.PaidOn).ToListAsync(), p => p.CustomerId);
            },
            Columns = ["Paid On", "Account", "Customer", "Amount", "Method", "Source", "Kind", "Status", "Reference"],
            Row = r => [Day(r["paid_on"]), C(r, "account"), C(r, "name"), r["amount"], r["method"], r["source"], r["kind"], r["status"], r["reference"]],
            Summary = rs => (["Status", "Method", "Payments", "Amount"], rs.GroupBy(r => (S: r["status"]?.ToString(), M: r["method"]?.ToString()))
                .Select(g => new object?[] { g.Key.S, g.Key.M, g.Count(), Math.Round(g.Sum(r => (decimal)r["amount"]!), 2) }).ToList()),
        },
        new()
        {
            Key = "bills", Title = "Bills", Model = "ItemFileBill_model", Category = "Billing", Description = "Bills issued, with usage and amounts.",
            Filters = _ => Task.FromResult(new List<Filter> { new("start", "Start", "date"), new("end", "End", "date") }),
            Query = async (db, f) =>
            {
                var from = f.Date("start", YearStart); var to = f.Date("end", DateOnly.FromDateTime(DateTime.Today));
                return await WithCustomers(db, await db.Bills.Where(b => b.BilledOn >= from && b.BilledOn <= to).OrderBy(b => b.BilledOn).ToListAsync(), b => b.CustomerId);
            },
            Columns = ["Billed On", "Account", "Customer", "Market", "kWh", "Amount", "Due", "Paid On", "Invoice"],
            Row = r => [Day(r["billed_on"]), C(r, "account"), C(r, "name"), C(r, "market"), r["kwh"], r["amount"], Day(r["due_on"]), Day(r["paid_on"]), r["invoice"]],
            Summary = rs => (["Market", "Bills", "kWh", "Billed"], rs.GroupBy(r => C(r, "market") ?? "—")
                .Select(g => new object?[] { g.Key, g.Count(), g.Sum(r => (int)r["kwh"]!), Math.Round(g.Sum(r => (decimal)r["amount"]!), 2) }).ToList()),
        },
        new()
        {
            Key = "receivables", Title = "Accounts Receivable Aging", Model = "TicketCustomer_model", Category = "Billing", Description = "Open balances aged Current, 1-30, 31-60, 61-90 and 90+ days past due.",
            Filters = _ => Task.FromResult(new List<Filter> { new("as_of", "As Of", "date"), new("min", "Minimum Balance ($)", "text") }),
            Defaults = () => new() { ["as_of"] = Report.Today, ["min"] = "0.01" },
            Query = async (db, f) =>
            {
                var min = decimal.TryParse(f.Str("min"), out var m) ? m : 0.01m;
                var asOf = f.Date("as_of", DateOnly.FromDateTime(DateTime.Today));
                return (await db.Customers.Include(c => c.Market).Include(c => c.Plan).Where(c => c.Balance >= min).OrderByDescending(c => c.Balance).ToListAsync()).Select(c =>
                {
                    var d = Customer(c);
                    d["days_past_due"] = c.DueDate is { } due ? Math.Max(0, asOf.DayNumber - due.DayNumber) : 0;
                    return d;
                }).ToList();
            },
            Columns = ["Account", "Customer", "Status", "Market", "Balance", "Due Date", "Days Past Due", "Aging"],
            Row = r => [r["account"], r["name"], r["status"], r["market"], r["balance"], Day(r["due_date"]), r["days_past_due"], Bucket((int)r["days_past_due"]!)],
            Summary = rs => (["Aging", "Accounts", "Balance"], Buckets.Select(b => { var g = rs.Where(r => Bucket((int)r["days_past_due"]!) == b).ToList(); return new object?[] { b, g.Count, Math.Round(g.Sum(r => (decimal)r["balance"]!), 2) }; }).ToList()),
        },
        new()
        {
            Key = "ercot", Title = "ERCOT Transactions", Model = "ItemErcot81405_model", Category = "Market", Description = "Market transactions (814s, 867s) by date, type and status.",
            Filters = _ => Task.FromResult(new List<Filter> { new("start", "Start", "date"), new("end", "End", "date"), new("type", "Trans Type", "text"), new("status", "Status", "select", Same(["linked", "unlinked", "cancelled"])) }),
            Query = async (db, f) =>
            {
                var from = f.Date("start", YearStart); var to = f.Date("end", DateOnly.FromDateTime(DateTime.Today));
                var q = db.ErcotTransactions.Where(t => t.TransDate >= from && t.TransDate <= to);
                if (f.Str("type") is { } t) q = q.Where(x => x.TransType == t);
                if (f.Str("status") is { } s) q = q.Where(x => x.Status == s);
                return await WithCustomers(db, await q.OrderBy(x => x.TransDate).ToListAsync(), x => x.CustomerId);
            },
            Columns = ["Trans Date", "Account", "ESIID", "Purpose", "Trans Type", "Label", "Tracking", "Status"],
            Row = r => [Day(r["trans_date"]), C(r, "account"), r["esiid"], r["purpose"], r["trans_type"], r["label"], r["tracking"], r["status"]],
        },
        new()
        {
            Key = "stars", Title = "Rewards Stars", Model = "ItemProductReward_model", Category = "Rewards", Description = "Stars earned and redeemed.",
            Filters = _ => Task.FromResult(new List<Filter> { new("start", "Start", "date"), new("end", "End", "date") }),
            Query = async (db, f) => await WithCustomers(db, await db.StarEntries.Where(s => s.CreatedAt >= Start(f, "start", YearStart) && s.CreatedAt < End(f, "end")).OrderBy(s => s.CreatedAt).ToListAsync(), s => s.CustomerId),
            Columns = ["Date", "Account", "Customer", "Reason", "Stars"],
            Row = r => [Day(r["created_at"]), C(r, "account"), C(r, "name"), r["reason"], r["stars"]],
            Summary = rs => (["Reason", "Entries", "Stars"], rs.GroupBy(r => r["reason"]?.ToString() is { } x && x.StartsWith("Redeemed") ? "Redeemed" : r["reason"]?.ToString())
                .Select(g => new object?[] { g.Key, g.Count(), g.Sum(r => (int)r["stars"]!) }).ToList()),
        },
    ];

    public static Report? Find(string key) => All.FirstOrDefault(r => r.Key == key);

    /// <summary>The filters as JSON, with the report's defaults under anything not given.</summary>
    public static Params With(Report r, string? json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(r.Defaults()))!;
        if (!string.IsNullOrEmpty(json))
            foreach (var (k, v) in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!)
                if (!(v.ValueKind == JsonValueKind.Null || (v.ValueKind == JsonValueKind.String && v.GetString() == "") || (v.ValueKind == JsonValueKind.Array && v.GetArrayLength() == 0))) values[k] = v;
        return new Params(values);
    }

    /// <summary>Keys the report's records offer for export columns (their fields, and the account's).</summary>
    public static async Task<List<string>> FieldsOf(AppDbContext db, Report r)
    {
        var first = (await r.Query(db, With(r, null))).FirstOrDefault();
        if (first == null) return [];
        return first.SelectMany(kv => kv.Value is Dictionary<string, object?> d ? d.Keys.Select(k => kv.Key + "." + k) : [kv.Key]).ToList();
    }

    /// <summary>A nested {key: title} array as export columns, in order ({"customer": {"name": "Name"}} → customer.name).</summary>
    public static List<(string Key, string Title)> Flatten(JsonElement nested, string prefix = "")
    {
        var cols = new List<(string, string)>();
        foreach (var p in nested.EnumerateObject())
        {
            var key = prefix + (p.Name == "_all" ? "*" : p.Name);
            if (p.Value.ValueKind == JsonValueKind.Object) cols.AddRange(Flatten(p.Value, key + "."));
            else if (Regex.IsMatch(key, @"^[A-Za-z0-9_.*]+$")) cols.Add((key, p.Value.ToString() is { Length: > 0 } t ? t : key));
        }
        return cols;
    }

    // ---- A run: query, build the file, keep the first rows --------------------------------------------------------

    /// <summary>Runs the report in the background (ebunny's RunReport job): queued → running → done or failed.</summary>
    public static void Start(IServiceScopeFactory scopes, int runId, string storage) => _ = Task.Run(async () =>
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TrackHistory = false;
        await RunAsync(db, runId, storage);
    });

    public static async Task RunAsync(AppDbContext db, int runId, string storage)
    {
        var run = await db.ReportRuns.FindAsync(runId);
        if (run == null) return;
        var report = Find(run.Report);
        (run.Status, run.StartedAt, run.Error) = ("running", DateTime.UtcNow, null);
        await db.SaveChangesAsync();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (report == null) throw new InvalidOperationException("This report no longer exists.");
            var records = await report.Query(db, With(report, run.Params));
            var (header, rows) = run.Summary ? report.SummaryOf(records) : await DetailAsync(db, report, records);
            var format = Formats.ContainsKey(run.Format) ? run.Format : "xlsx";
            var file = $"reports/{run.Report}{(run.Summary ? "-summary" : "")}-{run.Id}.{Formats[format].Ext}";
            var bytes = Build(format, header, rows, run.Title ?? run.Report);
            Directory.CreateDirectory(Path.Combine(storage, "reports"));
            await File.WriteAllBytesAsync(Path.Combine(storage, file), bytes);
            (run.Status, run.File, run.FileSize, run.Rows, run.FinishedAt, run.DurationMs) = ("done", file, bytes.Length, records.Count, DateTime.UtcNow, (int)watch.ElapsedMilliseconds);
            run.Preview = JsonSerializer.Serialize(new { header, rows = rows.Take(25).Select(r => r.Select(Cell)) });
        }
        catch (Exception e)
        {
            (run.Status, run.FinishedAt, run.Error) = ("failed", DateTime.UtcNow, e.Message.Length > 2000 ? e.Message[..2000] : e.Message);
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Every row: the report's own columns, or its export columns (model keys with titles) when it has them.</summary>
    private static async Task<(string[], List<object?[]>)> DetailAsync(AppDbContext db, Report report, List<Dictionary<string, object?>> records)
    {
        var saved = await db.ReportColumns.FirstOrDefaultAsync(c => c.Report == report.Key);
        var map = saved == null ? null : JsonSerializer.Deserialize<List<ExportColumn>>(saved.Columns);
        if (map is not { Count: > 0 }) return (report.Columns, records.Select(report.Row).ToList());
        return (map.Select(c => c.Title).ToArray(), records.Select(r => map.Select(c => Get(r, c.Key.Replace(".*", ""))).ToArray()).ToList());
    }

    public record ExportColumn(string Key, string Title);

    /// <summary>One value in a file: dates as text, decimals as numbers, yes/no.</summary>
    private static object? Cell(object? v) => v switch
    {
        DateTime t => t.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), DateOnly d => d.ToString("yyyy-MM-dd"), bool b => b ? "Yes" : "No",
        Dictionary<string, object?> m => m.GetValueOrDefault("name") ?? m.GetValueOrDefault("account") ?? m.GetValueOrDefault("id"), _ => v,
    };

    // ---- Files: xlsx, csv, txt, json (ebunny's Export) -----------------------------------------------------------

    public static byte[] Build(string format, string[] header, List<object?[]> rows, string title) => format switch
    {
        "csv" => Delimited(header, rows, ','),
        "txt" => Delimited(header, rows, '\t'),
        "json" => JsonSerializer.SerializeToUtf8Bytes(rows.Select(r => header.Select((h, i) => (h, v: i < r.Length ? Cell(r[i]) : null)).ToDictionary(x => x.h, x => x.v)), new JsonSerializerOptions { WriteIndented = true }),
        _ => Xlsx(header, rows, title),
    };

    private static byte[] Delimited(string[] header, List<object?[]> rows, char sep)
    {
        string Q(object? v)
        {
            var s = Convert.ToString(Cell(v), System.Globalization.CultureInfo.InvariantCulture) ?? "";
            if (s.Length > 0 && "=+-@".Contains(s[0]) && v is string) s = "'" + s; // keep spreadsheet apps from running cell text as a formula
            return s.IndexOfAny([sep, '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(sep, header.Select(Q)));
        foreach (var r in rows) sb.AppendLine(string.Join(sep, r.Select(Q)));
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>A plain one-sheet Excel workbook: a bold header row, numbers as numbers, everything else as text.</summary>
    private static byte[] Xlsx(string[] header, List<object?[]> rows, string title)
    {
        static string X(string s) => System.Security.SecurityElement.Escape(s) ?? "";
        static string Col(int i) { var s = ""; for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s; return s; }
        string RowXml(int n, IEnumerable<object?> cells, bool bold) => $"<row r=\"{n}\">" + string.Concat(cells.Select((c, i) =>
        {
            var v = Cell(c);
            var at = $"{Col(i)}{n}";
            return v is int or long or decimal or double or float
                ? $"<c r=\"{at}\"><v>{Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)}</v></c>"
                : $"<c r=\"{at}\" t=\"inlineStr\"{(bold ? " s=\"1\"" : "")}><is><t>{X(v?.ToString() ?? "")}</t></is></c>";
        })) + "</row>";
        var sheet = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>"
            + RowXml(1, header, true) + string.Concat(rows.Select((r, i) => RowXml(i + 2, r, false))) + "</sheetData></worksheet>";
        var name = X(Regex.Replace(title, @"[\[\]:*?/\\]", "").Trim() is { Length: > 0 } t ? t[..Math.Min(31, t.Length)] : "Report");
        var files = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>",
            ["_rels/.rels"] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>",
            ["xl/workbook.xml"] = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"{name}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>",
            ["xl/styles.xml"] = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font/><font><b/></font></fonts><fills count=\"1\"><fill/></fills><borders count=\"1\"><border/></borders><cellXfs count=\"2\"><xf/><xf fontId=\"1\" applyFont=\"1\"/></cellXfs></styleSheet>",
            ["xl/worksheets/sheet1.xml"] = sheet,
        };
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var (path, xml) in files)
            {
                using var w = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                w.Write(xml);
            }
        return ms.ToArray();
    }
}
