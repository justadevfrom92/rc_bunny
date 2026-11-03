using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data.Entities;

namespace RcBunny.Data.Seed;

public partial class DbSeeder
{
    /// <summary>Rodeo → Surveys: categories, answer sets, question bank, five surveys and their responses (SurveySeeder).</summary>
    private async Task SeedSurveys()
    {
        var cats = new Dictionary<string, SurveyCategory>();
        foreach (var (c, i) in new[] {
            ("Overall Experience", "How customers feel about us overall", "#102247"), ("Customer Service", "Phone, chat and email support", "#00AEEF"),
            ("Billing & Payments", "Bills, payments, AutoPay and due dates", "#EF8A00"), ("Website & My Account", "Signing up online and managing the account", "#5dd3ff"),
            ("Plans & Pricing", "Rates, terms and plan choices", "#137a3a"), ("Getting Started", "Enrollment, switching and the first bill", "#C80611"),
            ("About You", "How customers found us and what they want", "#96999d") }.Select((c, i) => (c, i)))
        { cats[c.Item1] = new SurveyCategory { Name = c.Item1, Description = c.Item2, Color = c.Item3, Position = i }; db.SurveyCategories.Add(cats[c.Item1]); }

        var sets = new Dictionary<string, SurveyAnswerSet>();
        foreach (var (name, group, type, options) in new (string, string, string, string[])[] {
            ("Satisfaction (5-point)", "Satisfaction", "scale", ["Very dissatisfied", "Dissatisfied", "Neutral", "Satisfied", "Very satisfied"]),
            ("Agreement (5-point)", "Agreement", "scale", ["Strongly disagree", "Disagree", "Neither agree nor disagree", "Agree", "Strongly agree"]),
            ("Ease (5-point)", "Ease", "scale", ["Very difficult", "Difficult", "Neither", "Easy", "Very easy"]),
            ("Likelihood (5-point)", "Likelihood", "scale", ["Very unlikely", "Unlikely", "Not sure", "Likely", "Very likely"]),
            ("How often", "Frequency", "single", ["Never", "Rarely", "Sometimes", "Often", "Every month"]),
            ("Contact channels", "Channels", "multi", ["Phone", "Email", "Text message", "Website chat", "My Account messages"]),
            ("How you heard about us", "Channels", "single", ["Search engine", "Friend or family", "Ad", "Power to Choose", "Other"]),
            ("What matters most", "Other", "multi", ["Low rate", "Fixed price", "Renewable energy", "Rewards", "Great service", "No deposit"]),
            ("Reasons for leaving", "Other", "single", ["Moving out of the area", "Found a lower rate", "Unhappy with customer service", "Billing problems", "Contract ended", "Other"]) })
        { sets[name] = new SurveyAnswerSet { Name = name, Group = group, Type = type, Options = new JsonArray(options.Select(o => (JsonNode)o!).ToArray()) }; db.SurveyAnswerSets.Add(sets[name]); }

        var bank = new Dictionary<string, SurveyBankQuestion>();
        foreach (var (cat, q, type, set, help) in new (string, string, string, string?, string?)[] {
            ("Overall Experience", "How likely are you to recommend us to a friend or colleague?", "rating", null, "0 = not at all likely, 10 = extremely likely"),
            ("Overall Experience", "Overall, how satisfied are you with your electricity service?", "scale", "Satisfaction (5-point)", null),
            ("Overall Experience", "How likely are you to renew when your contract ends?", "scale", "Likelihood (5-point)", null),
            ("Customer Service", "How satisfied were you with your last call to customer service?", "scale", "Satisfaction (5-point)", null),
            ("Customer Service", "Was your question answered on the first contact?", "yes_no", null, null),
            ("Customer Service", "How would you like us to contact you?", "multi", "Contact channels", "Pick all that apply"),
            ("Billing & Payments", "My bill is easy to understand.", "scale", "Agreement (5-point)", null),
            ("Billing & Payments", "How easy is it to pay your bill?", "scale", "Ease (5-point)", null),
            ("Billing & Payments", "Do you use AutoPay?", "yes_no", null, null),
            ("Website & My Account", "How easy was it to sign up on our website?", "scale", "Ease (5-point)", null),
            ("Website & My Account", "How often do you sign in to My Account?", "single", "How often", null),
            ("Plans & Pricing", "What matters most when you choose a plan?", "multi", "What matters most", "Pick all that apply"),
            ("Plans & Pricing", "My rate is fair for what I get.", "scale", "Agreement (5-point)", null),
            ("Getting Started", "How easy was it to switch to us?", "scale", "Ease (5-point)", null),
            ("Getting Started", "Did your service start on the date you expected?", "yes_no", null, null),
            ("About You", "How did you hear about us?", "single", "How you heard about us", null),
            ("About You", "Anything we could do better?", "text", null, "Optional"),
            ("Overall Experience", "What is the main reason you left?", "single", "Reasons for leaving", null),
            ("Overall Experience", "How likely are you to come back to us someday?", "scale", "Likelihood (5-point)", null),
            ("Getting Started", "How has your first three months with us been?", "scale", "Satisfaction (5-point)", null) })
        { bank[q] = new SurveyBankQuestion { SurveyCategory = cats[cat], Question = q, Type = type, SurveyAnswerSet = set is null ? null : sets[set], Help = help }; db.SurveyBankQuestions.Add(bank[q]); }
        await db.SaveChangesAsync();

        Survey Make(string title, string slug, string intro, string? thanks, string status, DateOnly? opens, DateOnly? closes, (string Q, bool Req)[] qs)
        {
            var s = new Survey { Title = title, Slug = slug, Intro = intro, ThankYou = thanks, Status = status, OpensOn = opens, ClosesOn = closes };
            foreach (var (x, i) in qs.Select((x, i) => (x, i)))
            {
                var b = bank[x.Q];
                s.Questions.Add(new SurveyQuestion
                {
                    SurveyCategoryId = b.SurveyCategoryId, SurveyBankQuestionId = b.Id, Question = b.Question, Type = b.Type, SurveyAnswerSetId = b.SurveyAnswerSetId,
                    Options = b.SurveyAnswerSet?.Options?.DeepClone(), Required = x.Req, Help = b.Help, Position = i,
                });
            }
            db.Surveys.Add(s);
            return s;
        }
        var how = Make("How are we doing?", "how-are-we-doing", "Two minutes to help us serve you better.", "Thanks for telling us how we’re doing. We read every answer.", "active", today.AddMonths(-3), null, [
            ("How likely are you to recommend us to a friend or colleague?", true), ("Overall, how satisfied are you with your electricity service?", true),
            ("How satisfied were you with your last call to customer service?", false), ("Was your question answered on the first contact?", false),
            ("My bill is easy to understand.", false), ("How easy is it to pay your bill?", false), ("What matters most when you choose a plan?", false),
            ("How did you hear about us?", false), ("Anything we could do better?", false)]);
        var onboarding = Make("New Customer Welcome", "new-customer-welcome", "You’re a few weeks in. How did getting started go?", "Thanks, and welcome aboard.", "active", null, null, [
            ("How easy was it to sign up on our website?", true), ("How easy was it to switch to us?", true), ("Did your service start on the date you expected?", true),
            ("How would you like us to contact you?", false), ("Anything we could do better?", false)]);
        Make("Billing Check-In", "billing-check-in", "Help us make bills clearer.", null, "active", today.AddDays(10), today.AddDays(40), [
            ("My bill is easy to understand.", true), ("Do you use AutoPay?", false), ("My rate is fair for what I get.", false)]);
        var ninety = Make("90-Day Check-In", "90-day-check-in", "You’ve been with us about three months. How’s it going?", "Thanks! We read every answer.", "active", null, null, [
            ("How has your first three months with us been?", true), ("How likely are you to recommend us to a friend or colleague?", true), ("My bill is easy to understand.", false),
            ("How likely are you to renew when your contract ends?", false), ("Anything we could do better?", false)]);
        var farewell = Make("Sorry to See You Go", "farewell", "Thank you for being a customer. Tell us why you left; it helps us do better.", "Thank you, and good luck. We’d love to have you back someday.", "active", null, null, [
            ("What is the main reason you left?", true), ("Overall, how satisfied are you with your electricity service?", false),
            ("How likely are you to come back to us someday?", false), ("Anything we could do better?", false)]);
        await db.SaveChangesAsync();

        var comments = new[] { "Faster answers on the phone.", "Text me when my bill is ready.", "Happy so far, the app is easy.", "The renewal letter was confusing.", "Love the rewards.", "Rates went up at renewal, not thrilled.", "Chat support was great.", "Wish AutoPay was easier to set up." };
        var customers = await db.Customers.OrderBy(c => c.Id).Select(c => c.Id).Take(44).ToListAsync();
        string PickChoice(SurveyQuestion q, int i, int skew = 0) { var ch = q.Options!.AsArray(); return (string)ch[Math.Min(ch.Count - 1, Math.Max(0, (i * 7 + skew) % ch.Count))]!; }
        void Respond(Survey s, int? cid, int i, string source, int daysAgo, Func<SurveyQuestion, JsonNode?> answer)
        {
            var answers = new JsonObject();
            foreach (var q in s.Questions) if (answer(q) is { } a) answers[q.Id.ToString()] = a;
            db.SurveyResponses.Add(new SurveyResponse { SurveyId = s.Id, CustomerId = cid, Answers = answers, Source = source, CreatedAt = now.AddDays(-daysAgo).AddHours(-i) });
        }
        int[] nps = [10, 9, 9, 8, 10, 7, 6, 9, 10, 4, 8, 9, 10, 5, 9, 7, 10];
        int[] scale = [4, 3, 3, 4, 2, 3, 4, 1, 3, 4];
        for (var i = 0; i < 34; i++)
            Respond(how, i % 11 == 10 ? null : customers[i], i, new[] { "email", "email", "website", "my-account" }[i % 4], 2 + i * 2, q =>
                !q.Required && i % 9 == 4 ? null : q.Type switch
                {
                    "rating" => nps[i % nps.Length].ToString(),
                    "scale" => q.Options!.AsArray()[scale[(i + q.Position) % 10]]!.DeepClone(),
                    "yes_no" => i % 4 != 0 ? "Yes" : "No",
                    "multi" => new JsonArray(new[] { PickChoice(q, i), PickChoice(q, i, 3) }.Distinct().Select(x => (JsonNode)x!).ToArray()),
                    "single" => PickChoice(q, i),
                    _ => i % 3 != 0 ? null : comments[i / 3 % comments.Length],
                });
        for (var i = 0; i < 12; i++)
            Respond(onboarding, customers[(32 + i) % customers.Count], i, "email", 1 + i * 3, q => q.Type switch
            {
                "scale" => q.Options!.AsArray()[new[] { 4, 3, 4, 2, 3 }[(i + q.Position) % 5]]!.DeepClone(),
                "yes_no" => i == 2 ? "No" : "Yes",
                "multi" => new JsonArray(new[] { PickChoice(q, i), PickChoice(q, i, 2) }.Distinct().Select(x => (JsonNode)x!).ToArray()),
                _ => i == 2 ? "Start date slipped a week; nobody told me." : null,
            });
        foreach (var (s, k) in new[] { (ninety, 0), (farewell, 1) })
            for (var i = 0; i < 9; i++)
                Respond(s, customers[(10 * k + i) % customers.Count], i, "email", 2 + i * 3, q => q.Type switch
                {
                    "rating" => new[] { 9, 10, 8, 7, 9, 10, 6, 9, 8 }[i].ToString(),
                    "scale" => q.Options!.AsArray()[new[] { 4, 3, 4, 2, 3, 4, 1, 3, 4 }[i]]!.DeepClone(),
                    "single" => q.Options!.AsArray()[new[] { 0, 1, 0, 2, 4, 0, 1, 3, 4 }[i] % q.Options!.AsArray().Count]!.DeepClone(),
                    _ => new[] { null, "Great so far.", null, "The first bill was confusing.", null, null, "Rate went up at renewal.", null, null }[i],
                });
        await Save();
    }

    /// <summary>Rodeo campaigns and their sends, suppressions, Caboose refunds, drawing entries, test sends (MarketingRewardsSeeder, CampaignSeeder).</summary>
    private async Task SeedCampaigns()
    {
        var admin = await UserId("admin@example.com");
        var csr = await UserId("csr@example.com");
        var marketing = await UserId("marketing@example.com");
        var templates = await db.EmailTemplates.ToDictionaryAsync(t => t.Name, t => t.Id);
        var renewal = templates.GetValueOrDefault("Renewal Offer");
        var onFlow = await db.Customers.Where(c => c.Status == "Good - On Flow").OrderBy(c => c.Id).ToListAsync();

        var fall = new Campaign
        {
            Name = "Fall Renewal Reminder", Channel = "Email", EmailTemplateId = renewal, Audience = new JsonObject { ["statuses"] = new JsonArray("Good - On Flow") },
            Status = "sent", SentAt = now.AddDays(-6), CreatedBy = admin, Goal = "renewals", Description = "Remind customers ending in the next 60 days to renew.",
        };
        db.Campaigns.Add(fall);
        await db.SaveChangesAsync();
        foreach (var (c, i) in onFlow.Take(14).Select((c, i) => (c, i)))
        {
            var bounced = (i is 4 or 10) && c.Email != null && c.Account != "1219000000";
            var log = new ContactLog
            {
                CustomerId = c.Id, CampaignId = fall.Id, Channel = "Email", Template = "Renewal Offer", Status = "sent", SentAt = now.AddDays(-6),
                DroppedAt = bounced ? now.AddDays(-6).AddMinutes(2) : null, OpenedAt = !bounced && i % 2 == 1 ? now.AddDays(-5) : null,
                ClickedAt = !bounced && i % 4 == 1 ? now.AddDays(-5) : null, CreatedAt = now.AddDays(-6), Direction = "out",
            };
            db.ContactLogs.Add(log);
            if (log.ClickedAt != null)
                db.LinkClicks.Add(new LinkClick { ContactLog = log, CampaignId = fall.Id, EmailTemplateId = renewal, CustomerId = c.Id, Link = "renew-button", Url = "/myaccount/plan", Device = i % 3 == 0 ? "mobile" : "desktop", ClickedAt = now.AddDays(-5) });
            if (bounced) db.EmailSuppressions.Add(new EmailSuppression { Email = c.Email!, Reason = "bounce", CustomerId = c.Id, Note = "Mailbox does not exist (550)", CreatedAt = now.AddDays(-6) });
        }
        fall.Recipients = 14;
        var others = await db.Customers.Where(c => c.Email != null && c.Account != "1219000000").OrderByDescending(c => c.Id).Take(20).ToListAsync();
        foreach (var (reason, note, days, idx) in new[] { ("unsubscribe", "Clicked unsubscribe in a Renewal Offer email", 9, 9), ("complaint", "Marked a Winback Offer email as spam", 14, 14), ("manual", "Asked on the phone to stop all emails", 3, 3) })
            db.EmailSuppressions.Add(new EmailSuppression { Email = others[idx].Email!, Reason = reason, CustomerId = others[idx].Id, Note = note, UserId = reason == "manual" ? csr : null, CreatedAt = now.AddDays(-days) });
        db.EmailSuppressions.Add(new EmailSuppression { Email = "old-address@example.net", Reason = "bounce", Note = "Address from a closed account", CreatedAt = now.AddDays(-40) });

        db.Campaigns.Add(new Campaign { Name = "Peak Perks Enrollment", Channel = "Email", EmailTemplateId = templates.FirstOrDefault(t => t.Key.Contains("Peak Perks")).Value, Status = "draft", CreatedBy = admin,
            Audience = new JsonObject { ["statuses"] = new JsonArray("Good - On Flow"), ["without_product"] = "Peak Perks" }, Goal = "product", Product = "Peak Perks" });
        db.Campaigns.Add(new Campaign { Name = "Winter Bill Ready Text", Channel = "SMS", Message = "Your winter bill is ready in My Account. Reply STOP to opt out.", Status = "scheduled", ScheduledAt = now.AddDays(3), CreatedBy = marketing,
            Audience = new JsonObject { ["statuses"] = new JsonArray("Good - On Flow"), ["phone_types"] = new JsonArray("Mobile") }, Goal = "engagement", CapDays = 7 });
        db.Campaigns.Add(new Campaign { Name = "Winback - Churned Customers", Channel = "Email", EmailTemplateId = templates.GetValueOrDefault("Winback Offer"), Status = "draft", CreatedBy = marketing,
            Audience = new JsonObject { ["statuses"] = new JsonArray("Dropped - Churned") }, Goal = "winback", PromoCode = "WELCOME", Msid = "1001" });

        // Caboose: a refund waiting for approval, one approved, one paid
        var churned = await db.Customers.AsTracking().Where(c => c.Status == "Dropped - Churned").Take(3).ToListAsync();
        foreach (var (c, i) in churned.Select((c, i) => (c, i)))
        {
            c.Balance = -new[] { 42.18m, 17.50m, 88.00m }[i];
            db.Refunds.Add(new Refund
            {
                CustomerId = c.Id, Amount = -c.Balance, Reason = "Credit balance after final bill", Kind = "credit_balance", Method = i == 2 ? "ACH" : "Check",
                Status = new[] { "requested", "approved", "paid" }[i], RequestedBy = csr, DecidedBy = i > 0 ? admin : null, DecidedAt = i > 0 ? Ago(5) : null,
                PaidAt = i == 2 ? Ago(2) : null, CheckNumber = i == 1 ? "100" + rng.Next(100, 999) : null, CreatedAt = Ago(12, 6),
            });
        }
        foreach (var c in await db.Customers.AsTracking().Where(c => c.Status == "Good - On Flow").Take(8).ToListAsync()) c.DepositHeld = 150;

        // Bounty → Monthly Drawing entries this month and last month
        var all = await db.Customers.Select(c => c.Id).ToListAsync();
        var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var (m, count) in new[] { (first, 18), (first.AddMonths(-1), 31) })
            foreach (var cid in all.OrderBy(_ => rng.Next()).Take(count))
                db.HistoryItems.Add(new HistoryItem { CustomerId = cid, Model = "ItemDrawingEntry_model", Group = "Products", RecordId = cid, Action = "created", Summary = "Monthly drawing entry",
                    Data = new JsonObject { ["month"] = m.ToString("yyyy-MM") }, CreatedAt = m.AddMinutes(rng.Next(0, Math.Max(60, (int)Math.Min(43000, (now - m).TotalMinutes)))) });
        db.ReferenceRows.Add(new ReferenceRow { TableKey = "monthly-drawing", Position = 0, Cells = new JsonArray(first.AddMonths(-1).ToString("yyyy-MM"), "$100 bill credit", (await db.Customers.FirstAsync()).Account, first.AddDays(1).ToString("yyyy-MM-dd")) });

        if (templates.Count > 0)
            db.EmailTestSends.Add(new EmailTestSend { EmailTemplateId = templates.Values.Min(), UserId = marketing, Mode = "team", Target = "Content Editor team", Status = "logged", CreatedAt = now.AddDays(-1),
                Recipients = new JsonArray(new JsonObject { ["email"] = "marketing@example.com", ["name"] = "Marketing Demo", ["status"] = "logged" }) });
        await Save();
    }

    /// <summary>Sheriff: reference tables, job history, connection tests (SystemSeeder); custom admin apps.</summary>
    private async Task SeedSystem()
    {
        var tables = LoadObject("data_tables");
        foreach (var (key, def) in AppConfig.Get("admin")["reference_tables"]!.AsObject())
        {
            var name = (string)def![0]!;
            if (tables[name]?["rows"] is not JsonArray rows) continue;
            foreach (var (r, i) in rows.Select((r, i) => (r, i)))
                db.ReferenceRows.Add(new ReferenceRow { TableKey = key, Position = i, Cells = new JsonArray(r!.AsArray().Select(x => (JsonNode?)x?.ToString()).ToArray()) });
        }
        var jobs = AppConfig.Get("admin")["jobs"]!.AsObject().ToDictionary(kv => (string)kv.Value![0]!, kv => kv.Key);
        var admin = await UserId("admin@example.com");
        foreach (var c in Load("crons"))
        {
            if (!jobs.TryGetValue((string)c!["name"]!, out var command)) continue;
            var start = DateTime.Parse((string)c["last"]!);
            var status = (string)c["status"]!;
            db.JobRuns.Add(new JobRun { Command = command, Status = status, Message = status == "Failed" ? "Sample failure from seed data" : null, StartedAt = start, FinishedAt = start.AddMilliseconds((int)c["ms"]!), DurationMs = (int)c["ms"]! });
            for (var k = 1; k < 6; k++)
            {
                var s = start.AddHours(-6 * k);
                var ms = rng.Next(300, 9000);
                db.JobRuns.Add(new JobRun { Command = command, Status = k == 3 && command.Contains("sync") ? "Failed" : "OK", StartedAt = s, FinishedAt = s.AddMilliseconds(ms), DurationMs = ms, TriggeredBy = k == 5 ? admin : null });
            }
        }
        foreach (var (a, n) in Load("apis").Select((a, n) => (a, n)))
        {
            var key = ((string)a!["name"]!).ToLowerInvariant().Replace(" ", "").Replace(".", "");
            for (var k = 1; k <= 3; k++)
            {
                var ok = !(key == "innowatts" && k == 1) && !(key == "authnet" && k <= 2);
                db.IntegrationTests.Add(new IntegrationTest { Integration = key, Ok = ok, ResponseMs = rng.Next(90, 900), UserId = admin, CreatedAt = now.AddDays(-((k - 1) * 9 + n % 5)).AddHours(-n),
                    Message = ok ? "Connected (sample)" : key == "authnet" ? "Login ID not accepted (sample)" : "Timed out after 10 seconds (sample)" });
            }
        }
        // A custom admin app made with "+ New Admin App" on the launcher
        db.AdminApps.Add(new AdminApp { Key = "ops", Name = "Ops Desk", Description = "Shortcuts for the operations team: exception queues, ERCOT, reports and crons.", Icon = "bolt", Position = 1, CreatedBy = admin,
            Menu = new JsonArray(new JsonObject { ["heading"] = "Daily", ["items"] = new JsonArray(
                new JsonObject { ["label"] = "Exception Queues", ["path"] = "/corral/queues" }, new JsonObject { ["label"] = "ERCOT", ["path"] = "/corral/ercot" },
                new JsonObject { ["label"] = "Report Status", ["path"] = "/walker" }, new JsonObject { ["label"] = "Crons", ["path"] = "/sheriff/jobs" }) }) });
        await Save();
    }

    /// <summary>Walker: report runs in every state, uploads in each folder, saved reports (WalkerSeeder).</summary>
    private async Task SeedWalker()
    {
        var users = await db.Users.Where(u => u.Active).Select(u => u.Id).Take(6).ToListAsync();
        var reports = AppConfig.Get("walker")["reports"]!.AsObject().ToList();
        var formats = new[] { "xlsx", "csv", "tsv", "json" };
        for (var i = 0; i < 40; i++)
        {
            var (key, def) = reports[i % reports.Count];
            var status = i switch { < 2 => "running", < 4 => "queued", 5 or 13 => "failed", 9 => "timeout", _ => "completed" };
            var started = Ago(30);
            var ms = rng.Next(800, 60000);
            var rows = status == "completed" ? rng.Next(5, 4000) : 0;
            var fmt = formats[i % formats.Length];
            db.ReportRuns.Add(new ReportRun
            {
                UserId = users[i % users.Count], Report = key, Title = (string)def!["title"]!, Model = (string?)def["model"], App = i % 4 == 0 ? "corral" : "walker",
                Params = new JsonObject { ["from"] = today.AddDays(-30).ToString("yyyy-MM-dd"), ["to"] = today.ToString("yyyy-MM-dd") }, Rows = rows, Status = status, Format = fmt,
                StartedAt = started, FinishedAt = status is "completed" or "failed" ? started.AddMilliseconds(ms) : null, DurationMs = status == "completed" ? ms : null,
                File = status == "completed" ? $"reports/{key}-{started:yyyyMMddHHmm}.{fmt}" : null, FileSize = status == "completed" ? rows * 120 + 2048 : null,
                Error = status == "failed" ? "Database query timed out (sample)" : null, SeenAt = i % 3 == 0 ? null : started.AddMinutes(5), CreatedAt = started,
            });
        }
        foreach (var (cat, title) in new[] { ("etl", "Utilibill daily export"), ("legal", "Terms of Service 2026"), ("templates", "Welcome letter template"), ("finance", "October journal"), ("ercot", "ERCOT 867 usage file"), ("vendor", "Brightline invoice") })
            for (var k = 0; k < 3; k++)
                db.ReportUploads.Add(new ReportUpload { Title = $"{title} {k + 1}", Category = cat, Description = "Sample file", Path = $"uploads/{cat}/{Slug(title)}-{k + 1}.csv", OriginalName = $"{Slug(title)}-{k + 1}.csv", Size = rng.Next(2000, 900_000), UserId = users[k % users.Count], CreatedAt = Ago(60) });
        foreach (var (key, name) in new[] { ("payments", "Weekly failed payments"), ("orders", "Website orders this month"), ("receivables", "AR aging for Finance") })
            db.SavedReports.Add(new SavedReport { UserId = users[0], Name = name, Report = key, Params = new JsonObject { ["range"] = "last_30_days" }, Format = "xlsx", Shared = key != "orders", LastRunAt = Ago(7) });
        await Save();
    }

    /// <summary>Lando: visitors, blocked IPs/areas, cookie consents (SiteTrafficSeeder, CookieConsentSeeder).</summary>
    private async Task SeedSiteTraffic()
    {
        var pages = await db.Pages.Select(p => new { p.Id, p.Path }).ToListAsync();
        var customers = await db.Customers.Select(c => c.Id).Take(10).ToListAsync();
        var refs = new[] { null, "https://www.google.com/", "https://www.powertochoose.org/", "https://www.facebook.com/", null };
        var agents = new[] { "Mozilla/5.0 (Windows NT 10.0) Chrome/130", "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0) Safari/604.1", "Mozilla/5.0 (Macintosh) Safari/605.1" };
        for (var v = 0; v < 120; v++)
        {
            var visitor = Guid.NewGuid().ToString("N")[..24];
            var start = Ago(14);
            for (var k = 0; k < 1 + v % 4; k++)
            {
                var p = Pick(pages);
                db.SiteVisits.Add(new SiteVisit { Visitor = visitor, Ip = $"198.51.100.{v % 250}", Path = p.Path, PageId = p.Id, CustomerId = v % 15 == 0 ? Pick(customers) : null,
                    Referrer = k == 0 ? refs[v % refs.Length] : null, Agent = agents[v % agents.Length], Blocked = v % 40 == 7, SeenAt = start.AddMinutes(k * 2 + 1), CreatedAt = start.AddMinutes(k * 2) });
            }
            if (v % 3 == 0)
                db.CookieConsents.Add(new CookieConsent { Visitor = visitor, Choice = new[] { "all", "necessary", "custom" }[v % 3 == 0 ? (v / 3) % 3 : 0], Analytics = v % 2 == 0, Marketing = v % 4 == 0,
                    PolicyVersion = "2026-01", Ip = $"198.51.100.{v % 250}", UserAgent = agents[v % agents.Length], Page = "/", CustomerId = v % 15 == 0 ? Pick(customers) : null,
                    CookieJar = new JsonObject { ["et_vid"] = visitor }, CookiesSeenAt = start, CreatedAt = start });
        }
        var admin = await UserId("admin@example.com");
        db.SiteBlocks.Add(new SiteBlock { Type = "ip", Value = "203.0.113.66", Reason = "Scraping the plans page every second", Message = "This page isn't available.", Hits = 412, LastHitAt = Ago(1), UserId = admin });
        db.SiteBlocks.Add(new SiteBlock { Type = "ip", Value = "192.0.2.0/24", Reason = "Bot network", Hits = 77, LastHitAt = Ago(3), UserId = admin, Active = false });
        db.SiteBlocks.Add(new SiteBlock { Type = "area", Value = "/checkout", Reason = "Pause sign-ups during the billing system upgrade", Message = "Sign-up is paused for maintenance. Please call us.", Active = false, UserId = admin, ExpiresAt = now.AddDays(2) });
        await Save();
    }

    private async Task SeedContactMessages()
    {
        var topics = new[] { "general", "signup", "billing", "business", "rewards" };
        var msgs = new[] { "Do you serve my area in Katy?", "I'd like a quote for 3 small offices.", "Why was I charged a late fee?", "How do I redeem my stars?", "Can I get a 24-month plan for my new house?", "Please call me about switching." };
        var admin = await UserId("admin@example.com");
        for (var i = 0; i < 16; i++)
        {
            var first = new[] { "Pat", "Chris", "Dana", "Lee", "Robin", "Jesse", "Morgan", "Casey" }[i % 8];
            var at = Ago(30);
            db.ContactMessages.Add(new ContactMessage { FirstName = first, LastName = "Sample", Email = $"{first.ToLower()}.sample{i}@example.com", Phone = i % 2 == 0 ? $"(555) 555-01{i:00}" : null,
                Topic = topics[i % topics.Length], Message = msgs[i % msgs.Length], Ip = $"198.51.100.{i}", CreatedAt = at, UpdatedAt = at,
                HandledAt = i % 3 == 0 ? at.AddHours(4) : null, HandledBy = i % 3 == 0 ? admin : null, Reply = i % 3 == 0 ? "Thanks for reaching out! We've answered your question by email." : null, RepliedAt = i % 3 == 0 ? at.AddHours(4) : null, RepliedBy = i % 3 == 0 ? admin : null });
        }
        await Save();
    }

    /// <summary>Deputy: open-weight models, agents, conversations, automation runs (DeputySeeder).</summary>
    private async Task SeedDeputy()
    {
        var m = new Dictionary<string, AiModel>();
        foreach (var (k, name, provider, id, source, size, quant, ctx, status) in new (string, string, string, string, string, decimal?, string?, int, string)[] {
            ("kimi", "Kimi K2 Instruct", "hosted", "moonshotai/Kimi-K2-Instruct", "Moonshot AI, open weights on Hugging Face", null, null, 131072, "available"),
            ("qwen235", "Qwen3 235B A22B", "hosted", "Qwen/Qwen3-235B-A22B-Instruct-2507", "Alibaba Qwen, open weights on Hugging Face", null, null, 262144, "available"),
            ("deepseek", "DeepSeek V3", "hosted", "deepseek-ai/DeepSeek-V3", "DeepSeek, open weights on Hugging Face", null, null, 131072, "available"),
            ("qwen8", "Qwen3 8B", "local", "qwen3:8b", "ollama pull qwen3:8b", 5.2m, "Q4_K_M", 40960, "available"),
            ("qwen30", "Qwen3 30B A3B", "local", "qwen3:30b-a3b", "ollama pull qwen3:30b-a3b", 19.0m, "Q4_K_M", 40960, "downloading"),
            ("gptoss", "gpt-oss 20B", "local", "gpt-oss:20b", "ollama pull gpt-oss:20b", 14.0m, "MXFP4", 131072, "available"),
            ("llama", "Llama 3.1 8B", "local", "llama3.1:8b", "ollama pull llama3.1:8b", 4.9m, "Q4_K_M", 131072, "available"),
            ("mistral", "Mistral 7B", "local", "mistral:7b", "ollama pull mistral:7b", 4.1m, "Q4_0", 32768, "available"),
            ("billing", "Billing Assistant (fine-tuned Qwen3 8B)", "local", "billing-assistant:8b", "/models/billing-assistant-8b-q4_k_m.gguf, fine-tuned on sample billing questions", 5.2m, "Q4_K_M", 40960, "available") })
        { m[k] = new AiModel { Name = name, Provider = provider, ModelId = id, Source = source, SizeGb = size, Quantization = quant, ContextWindow = ctx, Status = status, CheckedAt = Ago(2) }; db.AiModels.Add(m[k]); }
        await db.SaveChangesAsync();

        const string basePrompt = "You are a customer service agent for a Texas retail electricity provider. Be friendly, brief and accurate. Never guess account details; if you are unsure or the customer is upset, hand the conversation to a person.";
        var agents = new Dictionary<string, AiAgent>();
        foreach (var (k, name, activity, desc, model, fallback, temp, max, active, schedule) in new (string, string, string, string, string, string?, decimal, int, bool, string?)[] {
            ("sms", "Text Responder", "sms", "Replies to customer texts", "qwen8", "llama", 0.3m, 600, true, null),
            ("chat", "My Account Chat", "chat", "Answers the chat bubble in My Account", "qwen235", "qwen8", 0.4m, 1500, true, null),
            ("queues", "Queue Triage", "queues", "Moves new orders into the right exception queue", "billing", "qwen8", 0.0m, 500, true, "*/15 * * * *"),
            ("enrollments", "Enrollment Fixer", "enrollments", "Retries enrollments stuck waiting on ERCOT and fixes address mismatches", "kimi", "qwen30", 0.0m, 800, true, "0 * * * *"),
            ("payments", "Payment Matcher", "payments", "Matches unapplied payments to the right account", "billing", "deepseek", 0.0m, 500, true, "30 6 * * *"),
            ("surveys", "Survey Tagger", "surveys", "Tags survey comments by topic and mood", "mistral", "gptoss", 0.1m, 300, true, "0 2 * * *"),
            ("reports", "Report Summarizer", "reports", "Writes a short summary of finished Walker reports", "gptoss", "qwen30", 0.3m, 1500, false, "*/30 * * * *") })
        {
            agents[k] = new AiAgent { Name = name, Activity = activity, Description = desc, AiModelId = m[model].Id, FallbackModelId = fallback is null ? null : m[fallback].Id, Temperature = temp, MaxTokens = max, Active = active, Schedule = schedule,
                SystemPrompt = basePrompt + "\n\nYour job: " + char.ToLower(desc[0]) + desc[1..] + "." + (k == "chat" ? " If the customer asks for a person, or you can't help, answer with HANDOFF on its own line." : "") };
            db.AiAgents.Add(agents[k]);
        }
        await db.SaveChangesAsync();

        var issues = new (string Topic, string Q, string A)[] {
            ("High bill", "Why is my bill so high this month?", "Your usage went up about 30% with the heat, and the rate is the same. I can set up a payment arrangement if that helps."),
            ("Payment arrangement", "Can I split this bill into two payments?", "Yes. I can split it over your next two bills; the first half is due on the usual date."),
            ("Moving", "I am moving next month, what do I do?", "I can start service at your new address and stop it at the old one on the same day. What is the new address?"),
            ("Outage", "My power is out.", "Outages are handled by your utility. I am texting you their outage number and will check back when power is restored."),
            ("Renewal", "My contract is ending, what are my options?", "You have three renewal plans available. The 12-month plan keeps your rate within half a cent."),
            ("AutoPay", "How do I turn on AutoPay?", "I can turn it on now with your saved payment method. You will also earn AutoPay stars each month."),
            ("Rewards", "How many stars do I have?", "You have stars ready to redeem for a bill credit or a gift card. Want me to redeem them?"),
            ("Deposit", "Why do I need a deposit?", "The credit check asked for a deposit. You can pay it now, or enroll in AutoPay and paperless billing to lower it.") };
        var customers = await db.Customers.Where(c => c.Phone != null && c.Password == null).Select(c => new { c.Id, c.FirstName }).ToListAsync();
        var csrs = (await db.Users.Include(u => u.Role).Where(u => u.Active && u.Company == null).ToListAsync()).Where(u => u.Role?.Perms.Contains("corral") == true).Select(u => new { u.Id, u.Name }).Take(8).ToList();
        foreach (var (k, count) in new[] { ("sms", 90), ("chat", 80) })
        {
            var agent = agents[k];
            for (var i = 0; i < count; i++)
            {
                var (topic, q, a) = Pick(issues);
                var roll = rng.Next(1, 101);
                var outcome = roll <= 70 ? "resolved" : roll <= 92 ? "handed_off" : "abandoned";
                var c = Pick(customers);
                var lines = new List<string> { $"Bot: Hi {c.FirstName}, thanks for reaching out. How can I help?", $"Customer: {q}", "Bot: Let me look at your account.", $"Bot: {a}" };
                int? handed = null;
                if (outcome == "handed_off")
                {
                    var h = Pick(csrs);
                    handed = h.Id;
                    lines.AddRange(["Customer: I would rather talk to a person.", "Bot: Of course. I am getting a person for you now and passing along what we covered.", $"System: {h.Name} took over.", $"{h.Name}: Hi {c.FirstName}, I have your account open. I can help with that.", "Customer: Thanks, that fixed it."]);
                }
                else if (outcome == "resolved") lines.AddRange(["Customer: That works, thank you.", "Bot: You are welcome. Have a great day!"]);
                var start = Ago(29).AddMinutes(-90);
                var open = i < 3 && k == "chat";
                db.AiConversations.Add(new AiConversation
                {
                    AiAgentId = agent.Id, AiModelId = rng.Next(12) == 0 && agent.FallbackModelId != null ? agent.FallbackModelId : agent.AiModelId, CustomerId = c.Id, Channel = k, Topic = topic,
                    Outcome = open ? "open" : outcome, HandedToId = handed, Transcript = string.Join("\n", lines), DurationSec = rng.Next(20, 600), InputTokens = rng.Next(400, 3000), OutputTokens = rng.Next(100, 900),
                    StartedAt = open ? now.AddMinutes(-rng.Next(2, 20)) : start, Status = open ? (i == 0 ? "waiting" : "bot") : "closed", LastMessageAt = open ? now.AddMinutes(-1) : start.AddMinutes(8),
                });
            }
        }
        foreach (var k in new[] { "queues", "enrollments", "payments", "surveys", "reports" })
            for (var i = 0; i < 12; i++)
            {
                var checkedN = rng.Next(5, 120);
                var fixedN = rng.Next(0, checkedN / 2);
                db.AiAutomationRuns.Add(new AiAutomationRun { AiAgentId = agents[k].Id, AiModelId = agents[k].AiModelId, Status = i == 4 ? "failed" : "completed", Checked = checkedN, Fixed = fixedN, Flagged = rng.Next(0, 6),
                    Summary = i == 4 ? "Model did not answer in time (sample)." : $"Checked {checkedN}, fixed {fixedN}.", DurationSec = rng.Next(4, 240), InputTokens = rng.Next(1000, 20000), OutputTokens = rng.Next(200, 3000), StartedAt = Ago(20) });
            }
        await Save();
    }
}
