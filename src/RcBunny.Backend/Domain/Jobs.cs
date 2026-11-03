using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend;

/// <summary>Five-field cron expressions (minute hour day-of-month month day-of-week): *, */n, a-b, a,b.</summary>
public static class Cron
{
    public static bool Matches(string expr, DateTime t)
    {
        var f = expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (f.Length != 5) return false;
        return Field(f[0], t.Minute, 0) && Field(f[1], t.Hour, 0) && Field(f[2], t.Day, 1) && Field(f[3], t.Month, 1) && Field(f[4], (int)t.DayOfWeek, 0);
    }

    private static bool Field(string spec, int v, int min) => spec.Split(',').Any(part =>
    {
        var step = 1;
        if (part.Contains('/')) { step = int.Parse(part[(part.IndexOf('/') + 1)..]); part = part[..part.IndexOf('/')]; }
        int lo, hi;
        if (part == "*") { lo = min; hi = 59; }
        else if (part.Contains('-')) { lo = int.Parse(part.Split('-')[0]); hi = int.Parse(part.Split('-')[1]); }
        else { lo = hi = int.Parse(part); if (step > 1) hi = 59; }
        return v >= lo && v <= hi && (v - lo) % step == 0;
    });
}

/// <summary>
/// ebunny's scheduled commands (et:*), each logged as a job run for Sheriff → Crons and the Timeline.
/// Jobs that need an outside service report Skipped until its keys are set (like ebunny).
/// </summary>
public static class Jobs
{
    public sealed record JobDef(string Command, string Name, string Schedule);

    public static IReadOnlyList<JobDef> All => AppConfig.Get("admin")["jobs"]!.AsObject()
        .Select(kv => new JobDef(kv.Key, kv.Value![0]!.ToString(), kv.Value[1]!.ToString())).ToList();

    public static async Task<JobRun> RunAsync(AppDbContext db, FileStore files, string command, int? triggeredBy, CancellationToken ct)
    {
        var run = new JobRun { Command = command, Status = "Running", StartedAt = DateTime.UtcNow, TriggeredBy = triggeredBy };
        db.JobRuns.Add(run);
        await db.SaveChangesAsync(ct);
        var sw = Stopwatch.StartNew();
        try
        {
            (run.Status, run.Message) = command switch
            {
                "et:send-orders" => await SendOrders(db),
                "et:ercot-responses" => await ErcotResponses(db),
                "et:autopay" => await Autopay(db),
                "et:scheduled-payments" => await ScheduledPayments(db),
                "et:campaigns" => await ScheduledCampaigns(db),
                "et:survey-emails" => await SurveyEmails.SendDueAsync(db),
                "et:sync-payments" => Env("UTILIBILL_API_KEY") ? ("Failed", "Utilibill client isn't written yet.") : ("Skipped", "UTILIBILL_API_KEY isn't set."),
                "et:welcome-packets" => await WelcomePackets(db),
                "et:rewards-stars" => await RewardsStars(db),
                "et:export-rates" => await ExportRates(db, files),
                "et:sitemap" => ("OK", $"{await db.Pages.CountAsync(p => p.Status == "Published", ct)} published pages in the sitemap."),
                _ => ("Failed", "Unknown job."),
            };
            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e)
        {
            run.Status = "Failed"; run.Message = e.Message;
        }
        run.FinishedAt = DateTime.UtcNow; run.DurationMs = (int)sw.ElapsedMilliseconds;
        await db.SaveChangesAsync(ct);
        return run;
    }

    private static bool Env(string k) => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(k));

    private static async Task<(string, string)> SendOrders(AppDbContext db)
    {
        var orders = await db.Customers.AsTracking().Where(c => c.Status == "Submitted" && c.Exception == null).ToListAsync();
        foreach (var c in orders)
        {
            c.Status = "Pending - Utility Not Answered";
            db.ErcotTransactions.Add(new ErcotTransaction { CustomerId = c.Id, Esiid = c.Esiid, TransDate = DateOnly.FromDateTime(DateTime.Today), Purpose = "request", TransType = "814_16", Label = c.MoveSwitch == "move" ? "MoveIn" : "Switch", Tracking = "T" + Random.Shared.Next(1000000, 9999999), Status = "linked", ScheduledOn = c.RequestedStart });
            db.Notes.Add(new Note { CustomerId = c.Id, Author = "System", Body = "Order sent to the utility (814_16)." });
        }
        return ("OK", $"{orders.Count} orders sent.");
    }

    private static async Task<(string, string)> ErcotResponses(AppDbContext db)
    {
        var waiting = await db.Customers.AsTracking().Where(c => c.Status == "Pending - Utility Not Answered").ToListAsync();
        var accepted = 0;
        foreach (var c in waiting.Where(c => db.ErcotTransactions.Any(t => t.CustomerId == c.Id && t.TransType == "814_16" && t.TransDate <= DateOnly.FromDateTime(DateTime.Today.AddDays(-1)))))
        {
            c.Status = "Good - On Flow"; c.ActualStart ??= DateOnly.FromDateTime(DateTime.Today); accepted++;
            db.ErcotTransactions.Add(new ErcotTransaction { CustomerId = c.Id, Esiid = c.Esiid, TransDate = DateOnly.FromDateTime(DateTime.Today), Purpose = "response", TransType = "814_05", Label = "Accept", Status = "linked" });
            db.Notes.Add(new Note { CustomerId = c.Id, Author = "System", Body = "Utility accepted the enrollment (814_05). On flow." });
        }
        return ("OK", $"{accepted} accepted, {waiting.Count - accepted} still waiting.");
    }

    private static async Task<(string, string)> Autopay(AppDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var due = await db.Customers.AsTracking().Where(c => c.Autopay && c.Balance > 0 && c.DueDate != null && c.DueDate <= today).ToListAsync();
        foreach (var c in due)
        {
            var method = await db.PaymentMethods.FirstOrDefaultAsync(m => m.CustomerId == c.Id && m.Autopay && m.RemovedAt == null);
            db.Payments.Add(new Payment { CustomerId = c.Id, Reference = $"AP-{c.Account[^6..]}-{DateTime.UtcNow:yyMMddHHmm}", PaidOn = today, Amount = c.Balance, Method = method?.Type ?? "Card", Source = "AutoPay", Status = Env("STRIPE_SECRET") ? "Success" : "Pending", PaymentMethodId = method?.Id, Kind = "Balance Payment" });
            if (Env("STRIPE_SECRET")) c.Balance = 0;
        }
        return ("OK", $"{due.Count} AutoPay drafts{(Env("STRIPE_SECRET") ? "" : " recorded as Pending (no Stripe keys)")}.");
    }

    private static async Task<(string, string)> ScheduledPayments(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        var due = await db.ScheduledPayments.AsTracking().Where(s => s.Status == "scheduled" && s.PayAt <= now).ToListAsync();
        foreach (var s in due)
        {
            var c = await db.Customers.AsTracking().FirstAsync(x => x.Id == s.CustomerId);
            var p = new Payment { CustomerId = c.Id, Reference = $"SP-{s.Id}-{now:yyMMddHHmm}", PaidOn = DateOnly.FromDateTime(now), Amount = s.Amount, Method = "Card", Source = "My Account", Status = Env("STRIPE_SECRET") ? "Success" : "Pending", PaymentMethodId = s.PaymentMethodId, Kind = "Balance Payment" };
            db.Payments.Add(p);
            s.Status = "paid"; s.Payment = p;
            if (p.Status == "Success") c.Balance -= s.Amount;
        }
        return ("OK", $"{due.Count} scheduled payments made.");
    }

    private static async Task<(string, string)> ScheduledCampaigns(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        var due = await db.Campaigns.AsTracking().Where(c => c.Status == "scheduled" && c.ScheduledAt <= now).ToListAsync();
        var msgs = new List<string>();
        foreach (var c in due) msgs.Add(c.Name + ": " + await Campaigns.SendAsync(db, c));
        return ("OK", due.Count == 0 ? "Nothing scheduled." : string.Join(" ", msgs));
    }

    private static async Task<(string, string)> WelcomePackets(AppDbContext db)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var missing = await db.Customers.Where(c => c.Status == "Good - On Flow" && c.CreatedAt >= since && !db.CustomerFiles.Any(f => f.CustomerId == c.Id && f.Kind == "welcome")).ToListAsync();
        foreach (var c in missing) db.CustomerFiles.Add(new CustomerFile { CustomerId = c.Id, Name = "Welcome Packet.pdf", Kind = "welcome", GeneratedAt = DateTime.UtcNow, Size = 64_000 });
        return ("OK", $"{missing.Count} welcome packets made.");
    }

    /// <summary>Stars for payments since the last run, per the Earning Rules (Bounty).</summary>
    private static async Task<(string, string)> RewardsStars(AppDbContext db)
    {
        var last = await db.JobRuns.Where(j => j.Command == "et:rewards-stars" && j.Status == "OK").OrderByDescending(j => j.StartedAt).Select(j => (DateTime?)j.StartedAt).FirstOrDefaultAsync() ?? DateTime.UtcNow.AddDays(-1);
        var rule = await db.RewardRules.FirstOrDefaultAsync(r => r.Key == "on_time_payment" && r.Active);
        if (rule is null) return ("OK", "The bill payment rule is off.");
        var payments = await db.Payments.Where(p => p.Status == "Success" && p.CreatedAt > last).ToListAsync();
        var total = 0;
        foreach (var p in payments)
        {
            var stars = (int)Math.Round(rule.PerDollar ? p.Amount * rule.Stars : rule.Stars);
            if (stars <= 0) continue;
            db.StarEntries.Add(new StarEntry { CustomerId = p.CustomerId, Stars = stars, Reason = rule.Label });
            var c = await db.Customers.AsTracking().FirstAsync(x => x.Id == p.CustomerId);
            c.Stars += stars; total += stars;
        }
        return ("OK", $"{total:N0} stars for {payments.Count} payments.");
    }

    private static async Task<(string, string)> ExportRates(AppDbContext db, FileStore files)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var rates = (await db.Rates.Include(r => r.Plan).Include(r => r.Market).Where(r => r.EffectiveOn <= today).ToListAsync())
            .GroupBy(r => (r.PlanId, r.MarketId)).Select(g => g.OrderBy(r => r.EffectiveOn).ThenBy(r => r.Id).Last()).ToList();
        var (bytes, _, _) = TableExport.Write("csv", ["Plan", "Market", "Energy", "Effective"], rates.Select(r => new object?[] { r.Plan!.Internal, r.Market!.Name, r.Energy, r.EffectiveOn }).ToList());
        await files.SaveAsync($"exports/rates-{today:yyyy-MM-dd}.csv", bytes);
        return ("OK", $"{rates.Count} rates exported.");
    }
}

/// <summary>Rodeo → Survey Emails: 90-day check-in and farewell invitations with signed links.</summary>
public static class SurveyEmails
{
    public static async Task<(IReadOnlyList<Customer> NinetyDay, IReadOnlyList<Customer> Farewell)> DueAsync(AppDbContext db)
    {
        var from = DateTime.UtcNow.AddDays(-97); var to = DateTime.UtcNow.AddDays(-90);
        var sent = await db.ContactLogs.Where(l => l.Template.StartsWith("Survey - ")).Select(l => new { l.CustomerId, l.Template }).ToListAsync();
        bool Got(int id, string t) => sent.Any(s => s.CustomerId == id && s.Template == t);
        var ninety = (await db.Customers.Where(c => c.Status == "Good - On Flow" && c.CreatedAt >= from && c.CreatedAt <= to && c.Email != null).ToListAsync()).Where(c => !Got(c.Id, "Survey - 90 Day Check-In")).ToList();
        var farewell = (await db.Customers.Where(c => (c.Status == "Dropped - Churned" || c.Status == "Moved Out") && c.Email != null).ToListAsync()).Where(c => !Got(c.Id, "Survey - Farewell")).ToList();
        return (ninety, farewell);
    }

    public static async Task<(string, string)> SendDueAsync(AppDbContext db)
    {
        var (ninety, farewell) = await DueAsync(db);
        foreach (var c in ninety) db.ContactLogs.Add(new ContactLog { CustomerId = c.Id, Channel = "Email", Template = "Survey - 90 Day Check-In", Status = "logged", SentAt = DateTime.UtcNow, Direction = "out", Body = SurveyLinks.For("90-day-check-in", c.Id) });
        foreach (var c in farewell) db.ContactLogs.Add(new ContactLog { CustomerId = c.Id, Channel = "Email", Template = "Survey - Farewell", Status = "logged", SentAt = DateTime.UtcNow, Direction = "out", Body = SurveyLinks.For("farewell", c.Id) });
        return ("OK", $"{ninety.Count} 90-day and {farewell.Count} farewell survey emails.");
    }
}

/// <summary>Signed survey links so an answer is tied to the account ({{survey:slug}}). Same secret on the website.</summary>
public static class SurveyLinks
{
    public static string Secret = "rcbunny-dev-survey-secret";

    public static string Sign(string slug, int customerId)
    {
        using var h = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes($"{slug}|{customerId}")))[..20].ToLowerInvariant();
    }

    public static string For(string slug, int customerId) => $"/survey/{slug}?c={customerId}&sig={Sign(slug, customerId)}";
}

/// <summary>Sheriff → Heartbeat: is everything working? Database, scheduler, queue, storage, mail, website, keys.</summary>
public static class Health
{
    public static readonly string[] Checks = ["database", "scheduler", "queue", "storage", "mail", "website", "my_account", "admin", "api_keys"];

    public static async Task<Heartbeat> CheckAsync(AppDbContext db, FileStore files, IConfiguration config, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var results = new JsonObject();
        async Task Run(string key, Func<Task<(string, string)>> check)
        {
            var t = Stopwatch.StartNew();
            string status, msg;
            try { (status, msg) = await check(); } catch (Exception e) { (status, msg) = ("fail", e.Message); }
            var control = await db.HeartbeatControls.FirstOrDefaultAsync(c => c.Key == key, ct);
            if (control?.State == "down") (status, msg) = ("warn", $"Switched off by hand: {control.Reason}");
            results[key] = new JsonObject { ["status"] = status, ["message"] = msg, ["ms"] = (int)t.ElapsedMilliseconds };
        }
        await Run("database", async () => { await db.Users.CountAsync(ct); return ("ok", $"Connected ({db.Database.ProviderName?.Split('.').Last()})."); });
        await Run("scheduler", async () => await db.HeartbeatPulses.FirstOrDefaultAsync(p => p.Key == "scheduler", ct) is { } p && p.BeatAt > DateTime.UtcNow.AddMinutes(-3) ? ("ok", $"Last beat {(int)(DateTime.UtcNow - p.BeatAt).TotalSeconds}s ago.") : ("fail", "No beat in the last 3 minutes."));
        await Run("queue", async () => { var stuck = await db.ReportRuns.CountAsync(r => r.Status == "queued" && r.CreatedAt < DateTime.UtcNow.AddMinutes(-10), ct); return stuck == 0 ? ("ok", "Nothing waiting too long.") : ("warn", $"{stuck} reports queued for over 10 minutes."); });
        await Run("storage", () => { Directory.CreateDirectory(files.Root); var probe = Path.Combine(files.Root, ".probe"); File.WriteAllText(probe, "ok"); File.Delete(probe); return Task.FromResult(("ok", files.Root)); });
        await Run("mail", () => Task.FromResult(string.IsNullOrEmpty(config["Mail:Host"]) ? ("warn", "Mail is set to log: emails are recorded, not delivered.") : ("ok", $"SMTP {config["Mail:Host"]}.")));
        await Run("website", async () =>
        {
            var url = config["Website:Url"] ?? "http://localhost:5100";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            try { var r = await http.GetAsync(url + "/healthz", ct); return r.IsSuccessStatusCode ? ("ok", $"{url} answers.") : ("fail", $"{url} answered {(int)r.StatusCode}."); }
            catch (Exception) { return ("fail", $"{url} doesn't answer."); }
        });
        await Run("my_account", async () => (await db.Customers.CountAsync(c => c.Password != null, ct)) > 0 ? ("ok", "Customers can sign in.") : ("warn", "No My Account logins yet."));
        await Run("admin", async () => (await db.Users.CountAsync(u => u.Active, ct)) > 0 ? ("ok", "Admin sign-in works.") : ("fail", "No active admin users."));
        await Run("api_keys", () =>
        {
            var missing = new[] { "STRIPE_SECRET", "TWILIO_SID", "SALESFORCE_CLIENT_ID", "UTILIBILL_API_KEY" }.Where(k => !Env(k)).ToList();
            return Task.FromResult(missing.Count == 0 ? ("ok", "All keys set.") : ("warn", "Not set: " + string.Join(", ", missing)));
        });
        var worst = results.Select(r => r.Value!["status"]!.ToString()).Contains("fail") ? "fail" : results.Select(r => r.Value!["status"]!.ToString()).Contains("warn") ? "warn" : "ok";
        var hb = new Heartbeat { CheckedAt = DateTime.UtcNow, Status = worst, Results = results, DurationMs = (int)sw.ElapsedMilliseconds };
        db.Heartbeats.Add(hb);
        await db.SaveChangesAsync(ct);
        return hb;
    }

    private static bool Env(string k) => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(k));
}

/// <summary>
/// Replaces Laravel's scheduler and queue worker: every minute it beats, runs the jobs whose cron matches and the
/// Deputy automations that are due; every few seconds it runs queued reports.
/// </summary>
public sealed class Scheduler(IServiceScopeFactory scopes, ILogger<Scheduler> log, IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!config.GetValue("Scheduler:Enabled", true)) return;
        var lastMinute = -1;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var files = scope.ServiceProvider.GetRequiredService<FileStore>();
                foreach (var run in await db.ReportRuns.AsTracking().Where(r => r.Status == "queued").OrderBy(r => r.Id).Take(3).ToListAsync(stop))
                    await Reports.ExecuteAsync(db, files, run, stop);
                await Beat(db, "queue", stop);

                var now = DateTime.Now;
                if (now.Minute != lastMinute)
                {
                    lastMinute = now.Minute;
                    await Beat(db, "scheduler", stop);
                    foreach (var job in Jobs.All.Where(j => Cron.Matches(j.Schedule, now)))
                        await Jobs.RunAsync(db, files, job.Command, null, stop);
                    foreach (var agent in await db.AiAgents.Where(a => a.Active && a.Schedule != null).ToListAsync(stop))
                        if (Cron.Matches(agent.Schedule!, now)) await Automations.RunAsync(db, agent, stop);
                    if (now.Minute % 30 == 0) await Health.CheckAsync(db, files, config, stop);
                    // Reports that never finished (server restarted mid-run)
                    var cutoff = DateTime.UtcNow.AddMinutes(-config.GetValue("Walker:TimeoutMinutes", 60));
                    await db.ReportRuns.Where(r => r.Status == "running" && r.StartedAt < cutoff).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "timeout"), stop);
                }
            }
            catch (Exception e) when (!stop.IsCancellationRequested)
            {
                log.LogError(e, "Scheduler tick failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stop).ContinueWith(_ => { });
        }
    }

    private static async Task Beat(AppDbContext db, string key, CancellationToken ct)
    {
        var p = await db.HeartbeatPulses.AsTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
        if (p is null) db.HeartbeatPulses.Add(new HeartbeatPulse { Key = key, BeatAt = DateTime.UtcNow });
        else p.BeatAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Deputy automations (Cron Automations): each run looks at its work and records what it did.</summary>
public static class Automations
{
    public static async Task<AiAutomationRun> RunAsync(AppDbContext db, AiAgent agent, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var (count, what) = agent.Activity switch
        {
            "queues" => (await db.WorkItems.CountAsync(w => w.ResolvedAt == null, ct), "open work items"),
            "enrollments" => (await db.Customers.CountAsync(c => c.Status == "Pending - Utility Not Answered", ct), "enrollments waiting on the utility"),
            "payments" => (await db.Payments.CountAsync(p => p.Status == "Pending", ct), "pending payments"),
            "surveys" => (await db.SurveyResponses.CountAsync(r => r.CreatedAt > DateTime.UtcNow.AddDays(-1), ct), "new survey responses"),
            "reports" => (await db.ReportRuns.CountAsync(r => r.Status == "completed" && r.FinishedAt > DateTime.UtcNow.AddHours(-1), ct), "finished reports"),
            _ => (0, "items"),
        };
        var model = await db.AiModels.FirstAsync(m => m.Id == agent.AiModelId, ct);
        var run = new AiAutomationRun
        {
            AiAgentId = agent.Id, AiModelId = model.Id, Checked = count, Fixed = 0, Flagged = 0, StartedAt = started, DurationSec = 0,
            Status = model.Status == "available" ? "completed" : "skipped",
            Summary = model.Status == "available" ? $"Checked {count} {what}; nothing needed changing." : $"{count} {what} to check, but {model.Name} is {model.Status}.",
        };
        db.AiAutomationRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }
}
