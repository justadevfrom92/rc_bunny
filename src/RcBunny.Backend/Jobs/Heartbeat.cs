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

        db.HealthChecks.AddRange(results);
        await db.SaveChangesAsync();
        var bad = results.Where(r => !r.Ok).Select(r => r.Name).ToList();
        return bad.Count == 0 ? "All checks passed." : $"Problems: {string.Join(", ", bad)}.";
    }
}
