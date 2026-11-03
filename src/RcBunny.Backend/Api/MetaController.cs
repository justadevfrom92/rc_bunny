using Microsoft.AspNetCore.Mvc;
using RcBunny.Backend.Access;

namespace RcBunny.Backend.Api;

/// <summary>Configuration the client needs once per session: apps and menus, rights, lookups. Browser-cacheable.</summary>
[Route("api/meta")]
public class MetaController(RcBunny.Data.AppDbContext db) : ApiControllerBase
{
    [HttpGet, ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Get() => Ok(new
    {
        apps = AdminApps.Builtin.Concat(await CustomApps()),
        account = AdminApps.Account,
        rights = AdminApps.Rights,
        referenceTables = RcBunny.Data.AppConfig.Get("admin")["reference_tables"]!.AsObject().ToDictionary(kv => kv.Key, kv => new { name = kv.Value![0]!.ToString(), columns = kv.Value[1]!.AsArray().Select(c => c!.ToString()), app = kv.Value[2]!.ToString() }),
        queues = RcBunny.Data.AppConfig.Get("admin")["queues"]!.AsObject().ToDictionary(kv => kv.Key, kv => new { name = kv.Value![0]!.ToString(), description = kv.Value[1]!.ToString() }),
        departments = AdminApps.Departments,
        corral = new
        {
            statuses = CorralConfig.Statuses,
            exceptions = CorralConfig.Exceptions,
            noteCategories = CorralConfig.NoteCategories,
            priorities = CorralConfig.Priorities,
        },
    });

    /// <summary>Apps made with "+ New Admin App": menu JSON is [{heading, items: [{label, path}]}].</summary>
    private async Task<IEnumerable<AppDef>> CustomApps()
    {
        var apps = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.AdminApps.OrderBy(a => a.Position));
        return apps.Select(a =>
        {
            var groups = ((a.Menu as System.Text.Json.Nodes.JsonArray) ?? []).Select(g => new MenuGroup(g?["heading"]?.ToString(),
                (g?["items"]?.AsArray() ?? []).Select(i => new MenuItem(i?["label"]?.ToString() ?? "", i?["path"]?.ToString() ?? "/")).ToList())).ToList();
            return new AppDef(a.Key, a.Name, a.Description, a.Icon, groups.SelectMany(g => g.Items).FirstOrDefault()?.Path ?? "/", groups, true);
        });
    }
}

/// <summary>ebunny's config/admin.php customer_statuses/exceptions and config/corral.php note categories.</summary>
public static class CorralConfig
{
    /// <summary>Status → pill tone.</summary>
    public static readonly IReadOnlyDictionary<string, string> Statuses = new Dictionary<string, string>
    {
        ["Submitted"] = "info",
        ["Pending - Credit"] = "warn",
        ["Pending - Deposit Due"] = "warn",
        ["Pending - No Deposit Due"] = "warn",
        ["Pending - Utility Not Answered"] = "warn",
        ["Good - On Flow"] = "ok",
        ["Rejected - By Utility"] = "bad",
        ["Dropped - Churned"] = "bad",
        ["Pending - Disconnect"] = "warn",
        ["Disconnected"] = "bad",
        ["Moved Out"] = "bad",
        ["Cancelled"] = "bad",
    };

    public static readonly IReadOnlyList<string> Exceptions =
    [
        "Not sent to UtiliBill", "Not sent to Utility", "No ESIID", "Non-Resi Meter",
        "Permit Required", "Switch Hold", "Possible Duplicate", "Other Exception",
    ];

    public static readonly IReadOnlyDictionary<string, string[]> NoteCategories = new Dictionary<string, string[]>
    {
        ["Agent Issue"] = ["Audio Issue", "Customer Disconnect/Hang up", "Customer Not Authorized", "Internal Transfer", "Problem with System", "Test", "Wrong Number"],
        ["Billing"] = ["Balance Inquiry", "Balanced Billing Inquiry", "Balanced Billing Set up", "Bill Not Received", "Billing Dispute", "DNP/RNP", "Fees", "PYDD/Due Date Change", "Rate", "Resend Bill", "Usage", "Waive Deposit/Fee"],
        ["Complaint"] = ["Billing", "Customer Service", "Deposit Refund", "Disconnection", "PA/DPP", "Provision of Service", "Rates", "Sales Misinformation", "Slamming", "Solar Buy Back"],
        ["Inquiry"] = ["Contract Inquiry", "Outage", "Update Contact Info"],
        ["MVO/Cancel/Rescission"] = ["Cancel - Changed Mind", "Cancel - Contract", "Cancel - Not Moving to Address", "Cancel - Price", "MVO", "Rescission"],
        ["Order"] = ["Change Plan", "Date Change", "Deposit Waiver Docs", "General Inquiry", "Link Accounts", "Meter/ESI Inquiry", "Military/First Responder", "MT Request/Status", "New Enrollment", "Re-run Credit", "Reject/Rejection", "Renewal", "Status Inquiry", "Switch Hold", "Transfer of Service", "Verify ID"],
        ["Payment"] = ["AutoPay Set Up", "Deposit Refund Request", "DPP Set Up", "Duplicate Payment/Refund", "Missing Payment", "PA/DPP/Extension Inquiry", "Pay Deposit", "Payment Arrangement Set up", "Payment Dispute", "Payment Made", "Pledge Inquiry", "Remove Autopay"],
        ["Ticket"] = ["Internal Note", "Manager Request", "Follow-up", "Call Note"],
        ["Rewards"] = ["Gift Card", "Promo Code", "Redeem Stars"],
        ["Website/Mobile"] = ["Cannot Log in", "Online Issue", "Reset Username/Password"],
    };

    public static readonly IReadOnlyList<string> Priorities = ["Low", "Medium", "High"];
}
