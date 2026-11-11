using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RcBunny.Controllers;
using RcBunny.Data;
using RcBunny.Walker;

namespace RcBunny.Seed;

public static partial class Seeder
{
    /// <summary>
    /// Walker (ebunny's WalkerSeeder): report runs (some really run, so they have files and results; one error, one
    /// running, one that never finished), small fictional files in each folder, two weeks of ETL feed runs, and saved reports.
    /// </summary>
    private static async Task SeedWalkerAsync(AppDbContext db, User admin, string storage)
    {
        var finance = await db.Users.FirstOrDefaultAsync(u => u.Email == "finance@example.com") ?? admin;
        var now = DateTime.UtcNow;
        ReportRun Make(string key, User by, object? @params, string status, DateTime at)
        {
            var def = Reports.Find(key)!;
            var run = new ReportRun { UserId = by.Id, Report = key, Title = def.Title, Model = def.Model, Params = @params == null ? null : JsonSerializer.Serialize(@params), Status = status, CreatedAt = at };
            db.ReportRuns.Add(run);
            return run;
        }

        // Completed: actually run, so they have real results and files
        foreach (var (key, by, hours) in new[] { ("payments", finance, 26), ("receivables", finance, 8), ("orders", admin, 5), ("bills", finance, 2), ("stars", admin, 1) })
        {
            var run = Make(key, by, null, "queued", now.AddHours(-hours));
            await db.SaveChangesAsync();
            await Reports.RunAsync(db, run.Id, storage);
            (run.StartedAt, run.FinishedAt, run.DurationMs) = (now.AddHours(-hours), now.AddHours(-hours).AddSeconds(4), 4000);
        }
        // An error, one running now, and one that never finished
        var failed = Make("ercot", admin, new { type = "867_03" }, "failed", now.AddHours(-3));
        (failed.StartedAt, failed.FinishedAt, failed.Error) = (now.AddHours(-3), now.AddHours(-3).AddSeconds(41), "Utilibill export timed out after 40 seconds (sample error).");
        Make("contacts", admin, null, "running", now.AddMinutes(-4)).StartedAt = now.AddMinutes(-4);
        Make("exceptions", finance, new { open = "all" }, "running", now.AddHours(-5)).StartedAt = now.AddHours(-5);
        await db.SaveChangesAsync();

        // Files in each folder (small fictional sample files)
        string Letter(bool es) => es
            ? "Estimado/a {{customer.first_name}}:\n\nSu pago mensual de {{bill.amount_due}} para la cuenta {{customer.account}} vence el {{bill.due_date}}.\nPague en línea en {{site.url}}/myaccount o llame al {{site.phone}}.\n\nGracias,\n{{brand.name}}\n"
            : "Dear {{customer.first_name}},\n\nYour monthly payment of {{bill.amount_due}} for account {{customer.account}} is due on {{bill.due_date}}.\nPay online at {{site.url}}/myaccount or call {{site.phone}}.\n\nThank you,\n{{brand.name}}\n";
        (string Title, string Category, string Name, string Content, string About)[] files =
        [
            ("Monthly Revenue Close - September", "Finance", "revenue-close-2026-09.csv", "Account Group,Revenue\nResidential,412880.11\nSmall Business,98012.40\n", "Month-end close workbook export."),
            ("Deposits Held - Q3", "Finance", "deposits-q3.csv", "Month,Held,Returned\nJul,18200.00,2400.00\nAug,19150.00,3100.00\nSep,18760.00,2950.00\n", "Customer deposits by month."),
            ("ERCOT 867 Usage Exceptions", "Utility / ERCOT", "ercot-867-exceptions.csv", "ESIID,Issue\n10089011000000000000,Missing read\n", "Reads that failed validation."),
            ("TDSP Charges Reconciliation - September", "Utility / ERCOT", "tdsp-recon-2026-09.csv", "TDSP,Billed,Invoiced,Difference\nOncor,88120.40,88120.40,0.00\nCenterPoint,61004.18,61010.02,-5.84\n", "Billed delivery charges against the utility invoices."),
            ("ERCOT 867_03 Import - Today 6 AM", "ETL", "ercot_867_03_sample_06.csv", "ESIID,Read Date,kWh,Status\n10089011000000000001,2026-10-18,31.2,Loaded\n10089011000000000002,2026-10-18,28.7,Loaded\n10089011000000000003,2026-10-18,,Rejected: missing kWh\n", "Hourly usage import (sample rows)."),
            ("Card Settlement - Yesterday", "ETL", "settlement_sample.csv", "Batch,Payments,Gross,Fees,Net\n4471,212,24830.15,620.75,24209.40\n", "Daily settlement file from the card processor."),
            ("Collections Placement - This Week", "ETL", "placements_sample.txt", "ACCT      NAME                BALANCE   DAYS\n100118    SAMPLE CUSTOMER A      412.18    97\n100342    SAMPLE CUSTOMER B      188.40    112\n", "Fixed-width file sent to the collections agency."),
            ("Terms of Service - Residential (v2026.3)", "Legal Documents", "terms-of-service-residential-v2026-3.txt", "TERMS OF SERVICE (SAMPLE)\n\n1. Agreement. These terms are between you and the retail electric provider...\n2. Term and price...\n3. Billing and payment...\n", "Current residential terms. Approved by Legal 9/2026."),
            ("Your Rights as a Customer (YRAC)", "Legal Documents", "yrac-2026.txt", "YOUR RIGHTS AS A CUSTOMER (SAMPLE)\n\nYou have the right to receive a copy of your contract...\n", "Required disclosure, sent with every welcome packet."),
            ("Electricity Facts Label - Template", "Legal Documents", "efl-template.txt", "ELECTRICITY FACTS LABEL (SAMPLE)\nPlan: {{plan.name}}\nAverage price per kWh: 500 kWh {{plan.p500}} / 1000 kWh {{plan.p1000}} / 2000 kWh {{plan.p2000}}\n", "Template filled for each plan and TDSP."),
            ("Privacy Policy (v2026.1)", "Legal Documents", "privacy-policy-v2026-1.txt", "PRIVACY POLICY (SAMPLE)\n\nWe collect the information needed to provide electric service...\n", "Published on the website."),
            ("Monthly Payment Reminder - English", "Customer Templates", "monthly-payment-reminder-en.txt", Letter(false), "Mailed and emailed with the monthly payment letters run (Walker → ETL Feeds)."),
            ("Monthly Payment Reminder - Spanish", "Customer Templates", "monthly-payment-reminder-es.txt", Letter(true), "Spanish version of the monthly payment reminder."),
            ("Past Due Notice - English", "Customer Templates", "past-due-notice-en.txt", "Dear {{customer.first_name}},\n\nYour account {{customer.account}} is past due by {{balance.past_due}}. Please pay by {{notice.pay_by}} to avoid disconnection.\n", "Sent when an account is 15 days past due."),
            ("Disconnection Notice - English", "Customer Templates", "disconnection-notice-en.txt", "IMPORTANT NOTICE\n\nService at {{address.line}} may be disconnected on or after {{notice.disconnect_on}} unless {{balance.past_due}} is paid.\n", "Required 10-day notice before disconnection."),
            ("Welcome Letter - English", "Customer Templates", "welcome-letter-en.txt", "Welcome, {{customer.first_name}}!\n\nYour service at {{address.line}} starts on {{order.start_date}}. Your plan: {{plan.name}}.\n", "Included in the welcome packet."),
            ("Meter Vendor Monthly Report", "Vendor", "meter-vendor-2026-09.csv", "Region,Installs,Swaps\nNorth,112,18\nHouston,96,22\n", "From the meter services vendor."),
            ("Call Center Vendor Scorecard - September", "Vendor", "call-center-scorecard-2026-09.csv", "Metric,Target,Actual\nAnswer rate,95%,96.4%\nAverage handle time,6:00,5:41\n", "Monthly scorecard from the outside call center."),
            ("Q3 Campaign Results", "Marketing", "q3-campaigns.csv", "Campaign,Sent,Opened\nFall Renewal,1200,540\n", "Campaign totals for the quarter."),
        ];
        Directory.CreateDirectory(Path.Combine(storage, "report-uploads"));
        foreach (var ((title, category, name, content, about), n) in files.Select((f, n) => (f, n)))
        {
            var path = "report-uploads/sample-" + name;
            await File.WriteAllTextAsync(Path.Combine(storage, path), content);
            var at = now.AddDays(-(n % 9)).AddHours(-n);
            db.ReportUploads.Add(new ReportUpload { Title = title, Category = category, Path = path, OriginalName = name, Size = System.Text.Encoding.UTF8.GetByteCount(content),
                UserId = (category is "Finance" or "ETL" ? finance : admin).Id, Description = about, CreatedAt = at, UpdatedAt = at });
        }

        // ETL feed runs: the last two weeks of each feed, with a few failures to look into
        var r = new Random(41);
        var problems = new Dictionary<string, string>
        {
            ["ercot-867"] = "Gateway timeout after 120 seconds; 0 of 1,842 rows loaded (sample error)", ["lockbox"] = "File not found on the bank SFTP (sample error)",
            ["collections"] = "Agency SFTP rejected the login (sample error)", ["stripe-settlement"] = "3 payments in the file have no matching payment (sample warning)",
        };
        foreach (var (key, feed) in WalkerController.Feeds)
        {
            var monthly = feed.Schedule.Contains("month");
            var weekly = feed.Schedule.Contains("Mondays");
            var every = feed.Schedule.Contains("minutes") ? 180 : feed.Schedule.Contains("Hourly") ? 240 : weekly ? 60 * 24 * 7 : 60 * 24;
            var parts = feed.Cron.Split(' ');
            var first = (monthly ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-2) : weekly ? DateTime.Today.AddDays(-14 - ((int)DateTime.Today.DayOfWeek + 6) % 7) : DateTime.Today.AddDays(-14))
                .AddHours(int.TryParse(parts[1], out var h) ? h : 0).AddMinutes(int.TryParse(parts[0], out var m) ? m : 0);
            var i = 0;
            for (var at = first; at < DateTime.Now; at = monthly ? at.AddMonths(1) : at.AddMinutes(every), i++)
            {
                if (feed.Schedule.Contains("Weekdays") && at.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                var bad = problems.ContainsKey(key) && i % 9 == 4;
                var status = bad ? (key == "stripe-settlement" ? "Warning" : "Failed") : "OK";
                var file = feed.File.Replace("{date}", at.ToString("yyyyMMdd")).Replace("{hour}", at.ToString("HH")).Replace("{time}", at.ToString("HHmmss")).Replace("{month}", at.ToString("yyyyMM"));
                var rows = r.Next(40, 2401);
                db.JobRuns.Add(new JobRun
                {
                    Job = "etl:" + key, Status = status, Ok = status != "Failed", StartedAt = at.ToUniversalTime(), DurationMs = r.Next(800, 45001),
                    Message = bad ? problems[key] : (feed.Direction == "In" ? $"Loaded {rows:N0} rows from " : $"Sent {rows:N0} rows in ") + file,
                });
            }
        }
        await db.SaveChangesAsync();

        // Saved Reports: a few bookmarked reports (fictional settings)
        var yearStart = new DateTime(DateTime.Today.Year, 1, 1).ToString("yyyy-MM-dd");
        foreach (var (s, i) in new[]
        {
            (By: admin, Name: "On-flow orders this year", Key: "orders", Params: (object)new { start = yearStart, statuses = new[] { "Good - On Flow" } }, Format: "xlsx", Summary: false, Shared: true),
            (admin, "Failed payments (summary)", "payments", new { start = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd"), status = "Failed" }, "csv", true, false),
            (finance, "Payments last 30 days", "payments", new { start = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd") }, "xlsx", false, true),
            (admin, "All notes this month", "notes", new { start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("yyyy-MM-dd") }, "xlsx", false, false),
        }.Select((s, i) => (s, i)))
        {
            var saved = new SavedReport { UserId = s.By.Id, Name = s.Name, Report = s.Key, Params = JsonSerializer.Serialize(s.Params), Format = s.Format, Summary = s.Summary, Shared = s.Shared, CreatedAt = now.AddDays(-(20 - i * 3)) };
            db.SavedReports.Add(saved);
            await db.SaveChangesAsync();
            // its most recent run of that report becomes the saved report's run
            if (await db.ReportRuns.Where(x => x.Report == s.Key && x.SavedReportId == null).OrderByDescending(x => x.Id).FirstOrDefaultAsync() is { } run)
                (run.SavedReportId, saved.LastRunAt) = (saved.Id, run.CreatedAt);
        }
        await db.SaveChangesAsync();
    }
}
