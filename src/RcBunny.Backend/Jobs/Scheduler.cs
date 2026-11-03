using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Jobs;

/// <summary>A job the scheduler runs. Run returns a short message for Sheriff → Crons.</summary>
public record Job(string Name, string Schedule, TimeSpan Every, Func<IServiceProvider, AppDbContext, Task<string>> Run);

/// <summary>
/// Runs the jobs below on their schedule (checked once a minute) and records each run in Sheriff → Job Runs.
/// "Run now" in Sheriff calls RunAsync directly.
/// </summary>
public class Scheduler(IServiceScopeFactory scopes, ILogger<Scheduler> log) : BackgroundService
{
    public static readonly Job[] All =
    [
        new("Heartbeat checks", "Every 5 minutes", TimeSpan.FromMinutes(5), Heartbeat.RunChecksAsync),
        new("AutoPay", "Every day", TimeSpan.FromDays(1), AutoPayAsync),
        new("Close solved tickets", "Every day", TimeSpan.FromDays(1), CloseSolvedTicketsAsync),
        new("Reward stars", "Every day", TimeSpan.FromDays(1), RewardStarsAsync),
    ];

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            foreach (var job in All)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var last = await db.JobRuns.Where(r => r.Job == job.Name).OrderByDescending(r => r.StartedAt).Select(r => (DateTime?)r.StartedAt).FirstOrDefaultAsync(stop);
                    if (last is null || last < DateTime.UtcNow - job.Every) await RunAsync(scope.ServiceProvider, job, runBy: null);
                }
                catch (Exception e) when (!stop.IsCancellationRequested) { log.LogError(e, "Scheduler couldn't check {Job}", job.Name); }
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stop).ContinueWith(_ => { });
        }
    }

    public static async Task<JobRun> RunAsync(IServiceProvider services, Job job, string? runBy)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var run = new JobRun { Job = job.Name, RunBy = runBy };
        try { run.Message = await job.Run(services, db); run.Ok = true; }
        catch (Exception e) { run.Message = e.Message; run.Ok = false; }
        db.ChangeTracker.Clear();
        db.JobRuns.Add(run);
        if (runBy != null) Audit.Add(db, runBy, "job", $"Ran \"{job.Name}\": {run.Message}");
        await db.SaveChangesAsync();
        return run;
    }

    /// <summary>Pays the balance for AutoPay customers whose bill is due. No card processor is connected, so it records the payment only.</summary>
    private static async Task<string> AutoPayAsync(IServiceProvider _, AppDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var due = await db.Customers.Where(c => c.Autopay && c.Balance > 0 && c.DueDate <= today).ToListAsync();
        foreach (var c in due)
        {
            db.Payments.Add(new Payment { CustomerId = c.Id, PaidOn = today, Amount = c.Balance, Method = "AutoPay", Source = "AutoPay" });
            db.Notes.Add(new Note { CustomerId = c.Id, Author = "AutoPay", Body = $"AutoPay paid {c.Balance:C}." });
            c.Balance = 0;
        }
        await db.SaveChangesAsync();
        return due.Count == 0 ? "Nobody due today." : $"Paid {due.Count} bills.";
    }

    /// <summary>Bounty: stars for every bill paid in the last week that hasn't earned them yet (the "Pay a bill" rule).</summary>
    private static async Task<string> RewardStarsAsync(IServiceProvider _, AppDbContext db)
    {
        var rule = await db.RewardRules.FirstOrDefaultAsync(r => r.Event == "bill_paid" && r.Active);
        if (rule is null) return "The \"Pay a bill\" rule is turned off.";
        var since = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        var payments = await db.Payments
            .Where(p => p.Status == "Paid" && p.Kind == "Balance Payment" && p.PaidOn >= since && !db.StarEntries.Any(e => e.PaymentId == p.Id))
            .ToListAsync();
        foreach (var p in payments)
        {
            db.StarEntries.Add(new StarEntry { CustomerId = p.CustomerId, Stars = rule.Stars, Reason = rule.Name, PaymentId = p.Id, By = "Reward stars job" });
            await db.Customers.Where(c => c.Id == p.CustomerId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Stars, c => c.Stars + rule.Stars));
        }
        await db.SaveChangesAsync();
        return $"Gave {rule.Stars} stars for {payments.Count} payments.";
    }

    /// <summary>Solved tickets close after 7 days without a reply.</summary>
    private static async Task<string> CloseSolvedTicketsAsync(IServiceProvider _, AppDbContext db)
    {
        var count = await db.SupportTickets.Where(t => t.Status == "solved" && t.SolvedAt < DateTime.UtcNow.AddDays(-7))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, "closed"));
        return $"Closed {count} tickets.";
    }
}
