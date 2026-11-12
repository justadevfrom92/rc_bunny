using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Jobs;

/// <summary>Sheriff → Heartbeat checks: is the database answering, is the website up, did the last jobs work.</summary>
public static class Heartbeat
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<string> RunChecksAsync(IServiceProvider services, AppDbContext db)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var results = new List<HealthCheck>();

        try { results.Add(new HealthCheck { Name = "Database", Ok = true, Detail = $"{await db.Customers.CountAsync()} customers" }); }
        catch (Exception e) { results.Add(new HealthCheck { Name = "Database", Ok = false, Detail = e.Message }); }

        var site = config["Website:Url"] ?? "http://localhost:5100";
        try
        {
            using var response = await Http.GetAsync(site);
            results.Add(new HealthCheck { Name = "Website", Ok = response.IsSuccessStatusCode || (int)response.StatusCode == 503, Detail = $"{site} answered {(int)response.StatusCode}" });
        }
        catch (Exception e) { results.Add(new HealthCheck { Name = "Website", Ok = false, Detail = $"{site}: {e.Message}" }); }

        var failed = await db.JobRuns.Where(r => r.StartedAt > DateTime.UtcNow.AddDays(-1) && !r.Ok).CountAsync();
        results.Add(new HealthCheck { Name = "Jobs", Ok = failed == 0, Detail = failed == 0 ? "No failed runs today" : $"{failed} failed runs today" });

        // Sheriff hears when a check goes bad, and when everything is healthy again
        var before = (await db.HealthChecks.ToListAsync()).GroupBy(h => h.Name).Where(g => g.Any()).Select(g => g.MaxBy(h => h.Id)!).ToList();
        var wasBad = before.Where(h => !h.Ok).Select(h => h.Name).ToHashSet();
        var bad = results.Where(r => !r.Ok).Select(r => r.Name).ToList();
        db.HealthChecks.AddRange(results);
        var newlyBad = bad.Where(n => !wasBad.Contains(n)).ToList();
        var sheriff = newlyBad.Count > 0 || (bad.Count == 0 && wasBad.Count > 0) ? await AdminNotification.AppUsersAsync(db, "sheriff") : [];
        if (newlyBad.Count > 0) AdminNotification.Notify(db, sheriff, "health", $"Heartbeat: {string.Join(", ", newlyBad)} needs attention", null, "/admin/sheriff/heartbeat");
        else if (bad.Count == 0 && wasBad.Count > 0) AdminNotification.Notify(db, sheriff, "health", "Heartbeat: back to healthy", null, "/admin/sheriff/heartbeat");
        await db.SaveChangesAsync();
        return bad.Count == 0 ? "All checks passed." : $"Problems: {string.Join(", ", bad)}.";
    }
}
