using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>
/// A page for any row that has no page of its own (a note, a star entry, a plan term, a job run…), like ebunny's record and
/// data item pages: every field, Edit, Delete (needs the "delete" right), and a few actions (resend an email, mark a payment as a deposit).
/// </summary>
[ApiController]
[Route("api/records")]
public class RecordsController(AppDbContext db) : ControllerBase
{
    /// <summary>type → (table, label, the permission that opens it).</summary>
    public static readonly Dictionary<string, (Type Type, string Label, string Permission)> Types = new()
    {
        ["note"] = (typeof(Note), "Note", "corral"),
        ["payment"] = (typeof(Payment), "Payment", "corral"),
        ["bill"] = (typeof(Bill), "Bill", "corral"),
        ["payment_method"] = (typeof(PaymentMethod), "Payment Method", "corral"),
        ["plan_term"] = (typeof(PlanTerm), "Plan Term", "corral"),
        ["service_address"] = (typeof(ServiceAddress), "Service Address", "corral"),
        ["ercot_transaction"] = (typeof(ErcotTransaction), "ERCOT Transaction", "corral"),
        ["queue_log"] = (typeof(QueueLog), "Queue Log", "corral"),
        ["customer_flag"] = (typeof(CustomerFlag), "Account Flag", "corral"),
        ["customer_product"] = (typeof(CustomerProduct), "Account Product", "corral"),
        ["contact_log"] = (typeof(ContactLog), "Email / Text", "corral"),
        ["phonecall"] = (typeof(PhoneCall), "Phone Call", "corral"),
        ["api_log"] = (typeof(ApiLog), "API Call", "corral"),
        ["ledger_entry"] = (typeof(LedgerEntry), "Credit / Debit", "corral"),
        ["star_entry"] = (typeof(StarEntry), "Star Entry", "corral"),
        ["history_entry"] = (typeof(HistoryEntry), "History Entry", "corral"),
        ["crm_activity"] = (typeof(CrmActivity), "CRM Activity", "corral"),
        ["work_item"] = (typeof(WorkItem), "Work Item", "corral"),
        ["job_run"] = (typeof(JobRun), "Job Run", "sheriff"),
        ["health_check"] = (typeof(HealthCheck), "Heartbeat Check", "sheriff"),
        ["audit_entry"] = (typeof(AuditEntry), "Timeline Entry", "sheriff"),
        ["site_visit"] = (typeof(SiteVisit), "Site Visit", "lando"),
        ["history_item"] = (typeof(HistoryEntry), "History Entry", "lando"),
    };

    private static readonly HashSet<string> NotEditable = ["Id", "CustomerId", "CreatedAt", "UpdatedAt", "PasswordHash", "Changes"];

    [HttpGet("{type}/{id:int}")]
    public async Task<IActionResult> Get(string type, int id)
    {
        var (entity, me, problem) = await Load(type, id);
        if (problem != null) return problem;
        var entry = db.Entry(entity!);
        var t = Types[type];
        int? customerId = entry.Metadata.FindProperty("CustomerId") != null ? entry.Property("CustomerId").CurrentValue as int? : null;
        var customer = customerId is { } cid ? await db.Customers.Where(c => c.Id == cid).Select(c => new { c.Id, c.Account, c.Name }).FirstOrDefaultAsync() : null;
        return Ok(new
        {
            type, id, label = t.Label, customer,
            fields = entry.Properties.Where(p => p.Metadata.Name != "PasswordHash").Select(p => new
            {
                name = p.Metadata.Name, value = p.CurrentValue, kind = Kind(p.Metadata.ClrType), editable = !NotEditable.Contains(p.Metadata.Name),
            }),
            canDelete = me!.Permissions.Contains("delete"),
            actions = type switch
            {
                "contact_log" => new[] { new { key = "resend", label = "Resend" } },
                "payment" when entity is Payment { Kind: not "Deposit", Status: "Success" } => [new { key = "mark-deposit", label = "Mark as Deposit" }],
                _ => [],
            },
            history = await db.History.Where(h => h.RecordType == t.Type.Name && h.RecordId == id).OrderByDescending(h => h.CreatedAt).Take(50).ToListAsync(),
        });
    }

    [HttpPut("{type}/{id:int}")]
    public async Task<IActionResult> Update(string type, int id, Dictionary<string, string?> values)
    {
        var (entity, _, problem) = await Load(type, id);
        if (problem != null) return problem;
        var entry = db.Entry(entity!);
        foreach (var (name, raw) in values)
        {
            var p = entry.Metadata.FindProperty(name);
            if (p is null || NotEditable.Contains(name)) continue;
            var target = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
            var nullable = p.IsNullable;
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (!nullable && target != typeof(string)) return BadRequest(new { error = $"{name} can't be empty." });
                entry.Property(name).CurrentValue = target == typeof(string) && !nullable ? "" : null;
                continue;
            }
            try
            {
                object value = target == typeof(string) ? raw
                    : target == typeof(int) ? int.Parse(raw, CultureInfo.InvariantCulture)
                    : target == typeof(decimal) ? decimal.Parse(raw, CultureInfo.InvariantCulture)
                    : target == typeof(bool) ? bool.Parse(raw)
                    : target == typeof(DateOnly) ? DateOnly.Parse(raw, CultureInfo.InvariantCulture)
                    : target == typeof(TimeOnly) ? TimeOnly.Parse(raw, CultureInfo.InvariantCulture)
                    : target == typeof(DateTime) ? DateTime.Parse(raw, CultureInfo.InvariantCulture)
                    : throw new FormatException();
                entry.Property(name).CurrentValue = value;
            }
            catch (FormatException) { return BadRequest(new { error = $"{name}: \"{raw}\" isn't a valid {Kind(p.ClrType)}." }); }
        }
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{type}/{id:int}")]
    public async Task<IActionResult> Delete(string type, int id)
    {
        var (entity, me, problem) = await Load(type, id);
        if (problem != null) return problem;
        if (!me!.Permissions.Contains("delete")) return StatusCode(403, new { error = "Deleting needs the \"delete\" right." });
        db.Remove(entity!);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{type}/{id:int}/{action}")]
    public async Task<IActionResult> Act(string type, int id, string action)
    {
        var (entity, me, problem) = await Load(type, id);
        if (problem != null) return problem;
        string message;
        switch (entity)
        {
            case ContactLog log when action == "resend":
                db.ContactLogs.Add(new ContactLog { CustomerId = log.CustomerId, Channel = log.Channel, Template = log.Template, Body = log.Body, Phone = log.Phone, Status = "not sent", Who = me!.Name });
                message = $"Resent \"{log.Template}\": logged, but not sent (mail and texts aren't set up).";
                break;
            case Payment { Kind: not "Deposit", Status: "Success" } payment when action == "mark-deposit":
                var c = (await db.Customers.FindAsync(payment.CustomerId))!;
                payment.Kind = "Deposit";
                c.Balance += payment.Amount;
                c.DepositHeld += payment.Amount;
                c.DepositDue = Math.Max(0, c.DepositDue - payment.Amount);
                db.Notes.Add(new Note { CustomerId = c.Id, Author = me!.Name, Body = $"Payment {payment.Reference} ({payment.Amount:C}) moved to the deposit." });
                message = "Marked as a deposit.";
                break;
            default:
                return NotFound();
        }
        await db.SaveChangesAsync();
        return Ok(new { message });
    }

    private async Task<(object? Entity, Permissions.SignedIn? Me, IActionResult? Problem)> Load(string type, int id)
    {
        var me = await Permissions.CurrentAsync(HttpContext);
        if (me is null) return (null, null, Unauthorized());
        if (!Types.TryGetValue(type, out var t)) return (null, me, NotFound());
        if (!me.Permissions.Contains(t.Permission)) return (null, me, StatusCode(403));
        (db.ActorName, db.ActorId) = (me.Name, me.Id);
        var entity = await db.FindAsync(t.Type, id);
        return entity is null ? (null, me, NotFound()) : (entity, me, null);
    }

    private static string Kind(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t == typeof(bool) ? "yes/no" : t == typeof(int) || t == typeof(decimal) ? "number" : t == typeof(DateOnly) ? "date"
            : t == typeof(DateTime) ? "date and time" : t == typeof(TimeOnly) ? "time" : "text";
    }
}
