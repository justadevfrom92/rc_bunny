using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Services;

/// <summary>
/// The automatic survey emails (ebunny's SurveyEmails; sent daily by the "Survey emails" job, shown in Rodeo → Survey
/// Emails): a 90-day check-in to customers about 90 days after their service started, and a farewell survey to customers
/// whose service has ended. Each customer gets each one once; suppressed emails are skipped.
/// </summary>
public static class SurveyEmails
{
    public record Rule(string Name, string Template, string Slug, string Who);

    public static readonly Dictionary<string, Rule> Rules = new()
    {
        ["90_day"] = new("90-Day Check-In", "Survey - 90 Day Check-In", "90-day-check-in", "Customers whose service started 90 days ago (up to two weeks late, so nobody is missed)"),
        ["farewell"] = new("Farewell Survey", "Survey - Farewell", "farewell", "Customers whose service ended (dropped, moved out, cancelled or disconnected) in the last 30 days"),
    };

    public static readonly string[] Leaving = ["Dropped - Churned", "Moved Out", "Cancelled", "Disconnected"];

    /// <summary>Who should get it now and hasn't yet.</summary>
    public static IQueryable<Customer> Due(AppDbContext db, string rule)
    {
        var template = Rules[rule].Template;
        var q = db.Customers.Where(c => c.Email != null && c.Email != "" && !db.ContactLogs.Any(l => l.CustomerId == c.Id && l.Template == template));
        if (rule == "90_day")
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            DateOnly from = today.AddDays(-104), to = today.AddDays(-90);
            return q.Where(c => c.ActualStart >= from && c.ActualStart <= to && !Leaving.Contains(c.Status));
        }
        var since = DateTime.UtcNow.AddDays(-30);
        return q.Where(c => Leaving.Contains(c.Status) && c.UpdatedAt >= since);
    }

    /// <summary>Send one rule's emails (logged on each account; email isn't connected yet); returns (sent, skipped).</summary>
    public static async Task<(int Sent, int Skipped)> SendAsync(AppDbContext db, string rule, string websiteUrl)
    {
        var name = Rules[rule].Template;
        if (await db.EmailTemplates.FirstOrDefaultAsync(t => t.Name == name) is not { } template) return (0, 0);
        var suppressed = (await db.EmailSuppressions.Select(s => s.Email).ToListAsync()).ToHashSet();
        int sent = 0, skipped = 0;
        foreach (var c in await Due(db, rule).Include(c => c.Plan).ToListAsync())
        {
            if (suppressed.Contains(c.Email!.Trim().ToLowerInvariant())) { skipped++; continue; }
            db.ContactLogs.Add(new ContactLog
            {
                CustomerId = c.Id, Channel = "Email", Template = template.Name, Body = Campaigns.StripTags(Rodeo.FillIns(template.Body, c, c.Plan?.Name, websiteUrl)),
                Status = "not sent", SentAt = DateTime.UtcNow, Who = "System",
            });
            sent++;
        }
        await db.SaveChangesAsync();
        return (sent, skipped);
    }

    /// <summary>Both rules: "90-Day Check-In: 3 sent · Farewell Survey: 0 sent".</summary>
    public static async Task<string> SendAllAsync(AppDbContext db, string websiteUrl)
    {
        var parts = new List<string>();
        foreach (var (key, rule) in Rules)
        {
            var (sent, skipped) = await SendAsync(db, key, websiteUrl);
            parts.Add($"{rule.Name}: {sent} sent{(skipped > 0 ? $", {skipped} skipped (suppressed)" : "")}");
        }
        return string.Join(" · ", parts);
    }
}
