using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Backend.Access;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Api;

[Route("api/caboose"), RequirePerm("caboose")]
public class CabooseController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var since = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var payments = await db.Payments.Where(p => p.PaidOn >= since).Select(p => new { p.Amount, p.Status, p.Method, p.PaidOn }).ToListAsync();
        var customers = await db.Customers.Select(c => new { c.Balance, c.DepositDue, c.DepositHeld, c.DueDate }).ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        return Ok(new
        {
            collected30 = payments.Where(p => p.Status == "Success").Sum(p => p.Amount), payments30 = payments.Count(p => p.Status == "Success"),
            failed30 = payments.Count(p => p.Status == "Failed"), reversed30 = payments.Count(p => p.Status == "Reversed"),
            byMethod = payments.Where(p => p.Status == "Success").GroupBy(p => p.Method).Select(g => new { method = g.Key, amount = g.Sum(p => p.Amount), n = g.Count() }),
            byDay = payments.Where(p => p.Status == "Success").GroupBy(p => p.PaidOn).OrderBy(g => g.Key).Select(g => new { day = g.Key, amount = g.Sum(p => p.Amount) }),
            receivables = customers.Where(c => c.Balance > 0).Sum(c => c.Balance), credits = customers.Where(c => c.Balance < 0).Sum(c => -c.Balance),
            pastDue = customers.Where(c => c.Balance > 0 && c.DueDate < today).Sum(c => c.Balance),
            depositsHeld = customers.Sum(c => c.DepositHeld), depositsDue = customers.Sum(c => c.DepositDue),
            pendingLedger = await db.LedgerEntries.CountAsync(l => l.Status == "pending"), refundsRequested = await db.Refunds.CountAsync(r => r.Status == "requested"),
            refundsApproved = await db.Refunds.CountAsync(r => r.Status == "approved"), canDecide = Can("refunds"),
        });
    }

    [HttpGet("deposits")]
    public async Task<IActionResult> Deposits() => Ok(await db.Customers.Where(c => c.DepositDue > 0 || c.DepositHeld > 0).OrderByDescending(c => c.DepositDue)
        .Select(c => new { c.Id, c.Account, c.Name, c.Status, c.DepositDue, c.DepositHeld, c.TecScore, c.CreatedAt }).ToListAsync());

    [HttpGet("aging")]
    public async Task<IActionResult> Aging()
    {
        var asOf = DateOnly.FromDateTime(DateTime.Today);
        var list = (await db.Customers.Include(c => c.Market).ToListAsync()).Where(c => c.Balance > 0).ToList();
        return Ok(new
        {
            buckets = Reports.Buckets.Select(b => { var g = list.Where(c => Reports.Bucket(Reports.DaysPastDue(c, asOf)) == b).ToList(); return new { bucket = b, accounts = g.Count, balance = g.Sum(c => c.Balance) }; }),
            accounts = list.OrderByDescending(c => Reports.DaysPastDue(c, asOf)).Take(100).Select(c => new { c.Id, c.Account, c.Name, c.Status, market = c.Market?.Short, c.Balance, c.DueDate, days = Reports.DaysPastDue(c, asOf), bucket = Reports.Bucket(Reports.DaysPastDue(c, asOf)) }),
        });
    }

    /// <summary>Journal: payments, reversals, applied credits/debits and paid refunds as debit/credit lines for the GL. ?format=csv|xlsx downloads.</summary>
    [HttpGet("journal")]
    public async Task<IActionResult> Journal([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? format)
    {
        var f = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30)); var t = to ?? DateOnly.FromDateTime(DateTime.Today);
        var fd = f.ToDateTime(TimeOnly.MinValue); var td = t.ToDateTime(TimeOnly.MaxValue);
        var lines = new List<object?[]>();
        foreach (var p in await db.Payments.Include(x => x.Customer).Where(x => x.PaidOn >= f && x.PaidOn <= t && x.Status != "Failed").ToListAsync())
        {
            lines.Add([p.PaidOn, "1010 Cash", p.Amount, 0m, $"Payment {p.Reference} {p.Customer?.Account}"]);
            lines.Add([p.PaidOn, "1200 Accounts Receivable", 0m, p.Amount, $"Payment {p.Reference} {p.Customer?.Account}"]);
            if (p.Status == "Reversed" && p.ReversedAt is { } rv)
            {
                lines.Add([DateOnly.FromDateTime(rv), "1200 Accounts Receivable", p.Amount, 0m, $"Reversal {p.Reference}"]);
                lines.Add([DateOnly.FromDateTime(rv), "1010 Cash", 0m, p.Amount, $"Reversal {p.Reference}"]);
            }
        }
        foreach (var l in await db.LedgerEntries.Include(x => x.Customer).Where(x => x.Status == "applied" && x.DecidedAt >= fd && x.DecidedAt <= td).ToListAsync())
        {
            var d = DateOnly.FromDateTime(l.DecidedAt!.Value);
            lines.Add(l.Kind == "credit" ? [d, "4900 Credits & Adjustments", l.Amount, 0m, $"{l.Description} {l.Customer?.Account}"] : [d, "1200 Accounts Receivable", l.Amount, 0m, $"{l.Description} {l.Customer?.Account}"]);
            lines.Add(l.Kind == "credit" ? [d, "1200 Accounts Receivable", 0m, l.Amount, $"{l.Description} {l.Customer?.Account}"] : [d, "4100 Fee Revenue", 0m, l.Amount, $"{l.Description} {l.Customer?.Account}"]);
        }
        foreach (var r in await db.Refunds.Include(x => x.Customer).Where(x => x.Status == "paid" && x.PaidAt >= fd && x.PaidAt <= td).ToListAsync())
        {
            var d = DateOnly.FromDateTime(r.PaidAt!.Value);
            lines.Add([d, "1200 Accounts Receivable", r.Amount, 0m, $"Refund {r.Customer?.Account} {r.Method}"]);
            lines.Add([d, "1010 Cash", 0m, r.Amount, $"Refund {r.Customer?.Account} {r.Method}"]);
        }
        lines = lines.OrderBy(l => (DateOnly)l[0]!).ToList();
        string[] cols = ["Date", "Account", "Debit", "Credit", "Memo"];
        if (format is "csv" or "xlsx")
        {
            var (bytes, mime, ext) = TableExport.Write(format, cols, lines, "Journal");
            return File(bytes, mime, $"journal-{f:yyyy-MM-dd}-to-{t:yyyy-MM-dd}.{ext}");
        }
        return Ok(new { from = f, to = t, columns = cols, lines, debits = lines.Sum(l => (decimal)l[2]!), credits = lines.Sum(l => (decimal)l[3]!) });
    }
}

[Route("api/bounty"), RequirePerm("bounty")]
public class BountyController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var entries = await db.StarEntries.Where(e => e.CreatedAt > since).Select(e => new { e.Stars, e.Reason }).ToListAsync();
        return Ok(new
        {
            outstanding = await db.Customers.SumAsync(c => c.Stars), members = await db.Customers.CountAsync(c => c.Stars > 0),
            earned30 = entries.Where(e => e.Stars > 0).Sum(e => e.Stars), spent30 = -entries.Where(e => e.Stars < 0).Sum(e => e.Stars),
            pendingRedemptions = await db.StarEntries.CountAsync(e => e.Fulfillment == "pending"),
            byReason = entries.Where(e => e.Stars > 0).GroupBy(e => e.Reason).Select(g => new { reason = g.Key, stars = g.Sum(e => e.Stars) }).OrderByDescending(x => x.stars).Take(8),
            top = await db.Customers.OrderByDescending(c => c.Stars).Take(10).Select(c => new { c.Id, c.Account, c.Name, c.Stars }).ToListAsync(),
            offers = await db.RewardOffers.Where(o => o.Active).OrderBy(o => o.Position).Select(o => new { o.Id, o.Name, o.Stars, o.Effect }).ToListAsync(),
        });
    }

    private static string Month(DateTime d) => d.ToString("yyyy-MM");

    /// <summary>Monthly Drawing: this month's entries (from the drawing offer), and drawing a winner for a month.</summary>
    [HttpGet("drawing")]
    public async Task<IActionResult> Drawing([FromQuery] string? month)
    {
        var m = month ?? Month(DateTime.UtcNow);
        var entries = (await db.HistoryItems.Where(h => h.Model == "ItemDrawingEntry_model").Include(h => h.Customer).ToListAsync())
            .Where(h => h.Data?["month"]?.ToString() == m).Select(h => new { h.Id, h.CustomerId, account = h.Customer?.Account, name = h.Customer?.Name, h.CreatedAt }).ToList();
        var winners = await db.ReferenceRows.Where(r => r.TableKey == "monthly-drawing").OrderByDescending(r => r.Position).Select(r => r.Cells).ToListAsync();
        return Ok(new { month = m, entries, winners, drawn = winners.Any(w => w![0]!.ToString() == m) });
    }

    public record DrawRequest(string Month, string Prize);

    [HttpPost("drawing")]
    public async Task<IActionResult> Draw(DrawRequest r)
    {
        var entries = (await db.HistoryItems.Where(h => h.Model == "ItemDrawingEntry_model").ToListAsync()).Where(h => h.Data?["month"]?.ToString() == r.Month && h.CustomerId != null).ToList();
        if (entries.Count == 0) return Problem422("Nobody entered that month.");
        if ((await db.ReferenceRows.Where(x => x.TableKey == "monthly-drawing").ToListAsync()).Any(x => x.Cells?[0]?.ToString() == r.Month)) return Problem422("That month has already been drawn.");
        var win = entries[System.Security.Cryptography.RandomNumberGenerator.GetInt32(entries.Count)];
        var c = await db.Customers.FirstAsync(x => x.Id == win.CustomerId);
        var pos = await db.ReferenceRows.Where(x => x.TableKey == "monthly-drawing").Select(x => (int?)x.Position).MaxAsync() ?? -1;
        db.ReferenceRows.Add(new ReferenceRow { TableKey = "monthly-drawing", Position = pos + 1, Cells = new JsonArray(r.Month, r.Prize, c.Account, DateTime.Today.ToString("yyyy-MM-dd")) });
        db.Notes.Add(new Note { CustomerId = c.Id, UserId = Me.UserId, Author = Me.Name, Category = "Rewards", Action = "Gift Card", Body = $"Won the {r.Month} monthly drawing: {r.Prize}." });
        Audit(db, "drawing", r.Month, $"{c.Account} won {r.Prize} ({entries.Count} entries)");
        await db.SaveChangesAsync();
        return Ok(new { winner = new { c.Id, c.Account, c.Name }, entries = entries.Count });
    }
}

[Route("api/rodeo"), RequirePerm("rodeo")]
public class RodeoController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var logs = await db.ContactLogs.Where(l => l.CreatedAt > since && l.Direction == "out").Select(l => new { l.Channel, l.OpenedAt, l.ClickedAt, l.DroppedAt, l.CampaignId }).ToListAsync();
        var emails = logs.Where(l => l.Channel == "Email").ToList();
        return Ok(new
        {
            emails30 = emails.Count, texts30 = logs.Count(l => l.Channel == "SMS"),
            openRate = emails.Count == 0 ? 0 : Math.Round(100.0 * emails.Count(e => e.OpenedAt != null) / emails.Count, 1),
            clickRate = emails.Count == 0 ? 0 : Math.Round(100.0 * emails.Count(e => e.ClickedAt != null) / emails.Count, 1),
            bounceRate = emails.Count == 0 ? 0 : Math.Round(100.0 * emails.Count(e => e.DroppedAt != null) / emails.Count, 1),
            campaigns = await db.Campaigns.OrderByDescending(c => c.CreatedAt).Take(8).Select(c => new { c.Id, c.Name, c.Channel, c.Status, c.Recipients, c.SentAt, c.ScheduledAt }).ToListAsync(),
            surveys = await db.Surveys.Where(s => s.Status == "active").Select(s => new { s.Id, s.Title, responses = db.SurveyResponses.Count(r => r.SurveyId == s.Id) }).ToListAsync(),
            suppressed = await db.EmailSuppressions.CountAsync(),
        });
    }

    /// <summary>How many customers an audience reaches right now (the campaign builder updates this live).</summary>
    [HttpPost("audience-count")]
    public async Task<IActionResult> AudienceCount([FromBody] JsonObject body)
    {
        var channel = body["channel"]?.ToString() ?? "Email";
        var q = Campaigns.Audience(db, body["audience"], channel);
        return Ok(new { count = await q.CountAsync(), sample = await q.OrderBy(c => c.Id).Take(5).Select(c => new { c.Account, c.Name }).ToListAsync() });
    }

    [HttpGet("audience-options")]
    public async Task<IActionResult> AudienceOptions() => Ok(new
    {
        statuses = AppConfig.Keys("admin.customer_statuses"), types = new[] { "Residential", "Small Business" }, phoneTypes = new[] { "Mobile", "Home", "Work" },
        markets = await db.Markets.Select(m => m.Name).ToListAsync(), products = await db.CustomerProducts.Select(p => p.Product).Distinct().ToListAsync(),
        templates = await db.EmailTemplates.OrderBy(t => t.Name).Select(t => new { t.Id, t.Name }).ToListAsync(),
    });

    [HttpGet("emails")]
    public async Task<IActionResult> EmailOverview()
    {
        var since = DateTime.UtcNow.AddDays(-90);
        var logs = await db.ContactLogs.Where(l => l.Channel == "Email" && l.CreatedAt > since).Select(l => new { l.Template, l.OpenedAt, l.ClickedAt, l.DroppedAt }).ToListAsync();
        return Ok(new
        {
            byTemplate = logs.GroupBy(l => l.Template).Select(g => new { template = g.Key, sent = g.Count(), opened = g.Count(x => x.OpenedAt != null), clicked = g.Count(x => x.ClickedAt != null), bounced = g.Count(x => x.DroppedAt != null) }).OrderByDescending(x => x.sent),
            suppressions = await db.EmailSuppressions.GroupBy(s => s.Reason).Select(g => new { reason = g.Key, n = g.Count() }).ToListAsync(),
            mail = "Emails are logged until mail and Salesforce are set up.",
        });
    }

    [HttpGet("survey-emails")]
    public async Task<IActionResult> SurveyEmailsDue()
    {
        var (ninety, farewell) = await SurveyEmails.DueAsync(db);
        var sent = await db.ContactLogs.Where(l => l.Template.StartsWith("Survey - ")).OrderByDescending(l => l.CreatedAt).Take(30).Select(l => new { l.Template, l.SentAt, l.Customer!.Account, l.Customer.Name }).ToListAsync();
        return Ok(new { ninetyDay = ninety.Select(c => new { c.Id, c.Account, c.Name, c.CreatedAt }), farewell = farewell.Select(c => new { c.Id, c.Account, c.Name, c.Status }), sent });
    }

    [HttpPost("survey-emails/send")]
    public async Task<IActionResult> SendSurveyEmails()
    {
        var (_, msg) = await SurveyEmails.SendDueAsync(db);
        await db.SaveChangesAsync();
        return Ok(new { message = msg });
    }

    /// <summary>Survey results by category: NPS, scale averages, answer breakdowns and written answers.</summary>
    [HttpGet("surveys/{id:int}/results")]
    public async Task<IActionResult> Results(int id)
    {
        var s = await db.Surveys.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        var qs = await db.SurveyQuestions.Include(q => q.SurveyCategory).Where(q => q.SurveyId == id).OrderBy(q => q.Position).ToListAsync();
        var responses = await db.SurveyResponses.Where(r => r.SurveyId == id).Select(r => r.Answers).ToListAsync();
        object Summ(SurveyQuestion q)
        {
            var answers = responses.Select(r => r?[q.Id.ToString()]).Where(a => a != null).ToList();
            var choices = q.Options?.AsArray().Select(o => o!.ToString()).ToList() ?? [];
            switch (q.Type)
            {
                case "rating":
                    var nums = answers.Select(a => int.TryParse(a!.ToString(), out var n) ? n : -1).Where(n => n >= 0).ToList();
                    var nps = nums.Count == 0 ? (int?)null : (int)Math.Round(100.0 * (nums.Count(n => n >= 9) - nums.Count(n => n <= 6)) / nums.Count);
                    return new { q.Id, q.Question, q.Type, category = q.SurveyCategory?.Name, answered = nums.Count, nps, average = nums.Count == 0 ? 0 : Math.Round(nums.Average(), 1), counts = Enumerable.Range(0, 11).Select(n => new { label = n.ToString(), n = nums.Count(x => x == n) }) };
                case "scale":
                    var idx = answers.Select(a => choices.IndexOf(a!.ToString())).Where(i => i >= 0).ToList();
                    return new { q.Id, q.Question, q.Type, category = q.SurveyCategory?.Name, answered = idx.Count, average = idx.Count == 0 ? 0 : Math.Round(idx.Average() + 1, 2), counts = choices.Select((c, i) => new { label = c, n = idx.Count(x => x == i) }) };
                case "multi":
                    var picks = answers.SelectMany(a => a is JsonArray arr ? arr.Select(x => x!.ToString()) : [a!.ToString()]).ToList();
                    return new { q.Id, q.Question, q.Type, category = q.SurveyCategory?.Name, answered = answers.Count, counts = choices.Select(c => new { label = c, n = picks.Count(p => p == c) }) };
                case "text":
                    return new { q.Id, q.Question, q.Type, category = q.SurveyCategory?.Name, answered = answers.Count, texts = answers.Select(a => a!.ToString()).TakeLast(50) };
                default:
                    var vals = answers.Select(a => a!.ToString()).ToList();
                    var labels = q.Type == "yes_no" ? ["Yes", "No"] : choices;
                    return new { q.Id, q.Question, q.Type, category = q.SurveyCategory?.Name, answered = vals.Count, counts = labels.Select(c => new { label = c, n = vals.Count(v => v == c) }) };
            }
        }
        return Ok(new { survey = new { s.Id, s.Title, s.Slug, s.Status }, responses = responses.Count, questions = qs.Select(Summ) });
    }

    [HttpGet("surveys/{id:int}/export")]
    public async Task<IActionResult> Export(int id)
    {
        var s = await db.Surveys.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        var qs = await db.SurveyQuestions.Where(q => q.SurveyId == id).OrderBy(q => q.Position).ToListAsync();
        var rs = await db.SurveyResponses.Include(r => r.Customer).Where(r => r.SurveyId == id).OrderBy(r => r.CreatedAt).ToListAsync();
        var rows = rs.Select(r => new object?[] { r.CreatedAt.ToString("yyyy-MM-dd HH:mm"), r.Customer?.Account, r.Source }.Concat(qs.Select(q => r.Answers?[q.Id.ToString()] is JsonArray a ? string.Join("; ", a) : r.Answers?[q.Id.ToString()]?.ToString())).ToArray()).ToList();
        var (bytes, mime, _) = TableExport.Write("csv", new[] { "Submitted", "Account", "Source" }.Concat(qs.Select(q => q.Question)).ToList(), rows);
        return File(bytes, mime, $"{s.Slug}-responses.csv");
    }

    /// <summary>A template filled in for one account (or a sample), with its header and footer.</summary>
    [HttpGet("templates/{id:int}/preview")]
    public async Task<IActionResult> Preview(int id, [FromQuery] string? account)
    {
        var t = await db.EmailTemplates.Include(x => x.EmailHeader).Include(x => x.EmailFooter).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var c = account is null ? await db.Customers.Include(x => x.Plan).OrderBy(x => x.Id).FirstAsync() : await db.Customers.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Account == account);
        if (c is null) return Problem422("No account with that number.");
        var header = t.EmailHeader ?? await db.EmailParts.FirstOrDefaultAsync(p => p.Kind == "header" && p.IsDefault);
        var footer = t.EmailFooter ?? await db.EmailParts.FirstOrDefaultAsync(p => p.Kind == "footer" && p.IsDefault);
        string R(string? s) => TemplateText.Render(s ?? "", c, c.Plan?.Name, slug => SurveyLinks.For(slug, c.Id));
        return Ok(new { subject = R(t.Subject), html = R(header?.Html) + $"<div style=\"font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:16px 24px\">{R(t.Body)}</div>" + R(footer?.Html), to = c.Email, account = c.Account });
    }

    public record TestRequest(string Mode, string? Emails, string? Accounts, string? Note, string? SubjectPrefix);

    /// <summary>Send Test: just you, typed addresses, account numbers, or your bookmarks (up to 25). Logged until mail is set up.</summary>
    [HttpPost("templates/{id:int}/test")]
    public async Task<IActionResult> SendTest(int id, TestRequest r)
    {
        var t = await db.EmailTemplates.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var recipients = new JsonArray();
        void Add(string email, string name) => recipients.Add(new JsonObject { ["email"] = email, ["name"] = name, ["status"] = "logged" });
        switch (r.Mode)
        {
            case "me": Add(Me.Email, Me.Name); break;
            case "emails":
                foreach (var e in (r.Emails ?? "").Split([',', ';', '\n', ' '], StringSplitOptions.RemoveEmptyEntries))
                    if (System.Net.Mail.MailAddress.TryCreate(e, out _)) Add(e, e); else return Problem422($"{e} isn't an email address.");
                break;
            case "accounts":
                var accts = (r.Accounts ?? "").Split([',', ';', '\n', ' '], StringSplitOptions.RemoveEmptyEntries).ToList();
                foreach (var c in await db.Customers.Where(c => accts.Contains(c.Account) && c.Email != null).ToListAsync()) Add(c.Email!, c.Name);
                break;
            case "bookmarks":
                foreach (var c in await db.Bookmarks.Where(b => b.UserId == Me.UserId).Select(b => b.Customer!).Where(c => c.Email != null).ToListAsync()) Add(c.Email!, c.Name);
                break;
            default: return Problem422("Pick who gets the test.");
        }
        if (recipients.Count == 0) return Problem422("Nobody to send to.");
        if (recipients.Count > 25) return Problem422("Tests go to 25 people at most.");
        db.EmailTestSends.Add(new EmailTestSend { EmailTemplateId = id, UserId = Me.UserId, Mode = r.Mode, Target = r.Mode == "me" ? Me.Email : $"{recipients.Count} recipients", Recipients = recipients, Status = "logged", Subject = (r.SubjectPrefix ?? "[TEST] ") + t.Subject, Note = r.Note });
        await db.SaveChangesAsync();
        return Ok(new { message = $"Test logged for {recipients.Count} recipient(s) (mail isn't set up, so nothing was delivered)." });
    }

    [HttpGet("pdfs/{id:int}/preview")]
    public async Task<IActionResult> PdfPreview(int id, [FromQuery] string? account)
    {
        var t = await db.PdfTemplates.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        var c = account is null ? await db.Customers.Include(x => x.Plan).OrderBy(x => x.Id).FirstAsync() : await db.Customers.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Account == account);
        var (bytes, _, _) = await Pdfs.RenderAsync(db, t, c);
        return File(bytes, "application/pdf");
    }

    [HttpGet("pdfs/made/{id:int}/pdf")]
    public async Task<IActionResult> MadePdf(int id)
    {
        var g = await db.PdfGenerations.FirstOrDefaultAsync(x => x.Id == id);
        if (g is null) return NotFound();
        var pdf = SimplePdf.Letter(g.Header?["title"]?.ToString(), null, g.Header?["color"]?.ToString() ?? "#102247", null, g.Title, g.Body + (g.Footer is { } f ? "\n\n" + f : ""), null);
        return File(pdf.Output(g.Title), "application/pdf", g.FileName);
    }
}

[Route("api/deputy"), RequirePerm("deputy")]
public class DeputyController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var convs = await db.AiConversations.Where(c => c.StartedAt > since && c.Channel != "test").Select(c => new { c.Outcome, c.Channel, c.InputTokens, c.OutputTokens, c.AiAgentId, c.Topic, c.DurationSec }).ToListAsync();
        var agents = await db.AiAgents.Include(a => a.AiModel).ToListAsync();
        return Ok(new
        {
            conversations = convs.Count, waiting = await db.AiConversations.CountAsync(c => c.Status == "waiting"), live = await db.AiConversations.CountAsync(c => c.Status == "bot" || c.Status == "agent"),
            resolvedRate = convs.Count == 0 ? 0 : Math.Round(100.0 * convs.Count(c => c.Outcome == "resolved") / convs.Count, 1),
            handoffRate = convs.Count == 0 ? 0 : Math.Round(100.0 * convs.Count(c => c.Outcome == "handed_off") / convs.Count, 1),
            tokens = convs.Sum(c => (long)c.InputTokens + c.OutputTokens),
            byOutcome = convs.GroupBy(c => c.Outcome).Select(g => new { outcome = g.Key, n = g.Count() }),
            byTopic = convs.GroupBy(c => c.Topic).Select(g => new { topic = g.Key, n = g.Count(), handedOff = g.Count(x => x.Outcome == "handed_off") }).OrderByDescending(x => x.n),
            agents = agents.Select(a => new { a.Id, a.Name, a.Activity, a.Active, a.Schedule, model = a.AiModel?.Name, modelStatus = a.AiModel?.Status, conversations = convs.Count(c => c.AiAgentId == a.Id) }),
            runs = await db.AiAutomationRuns.OrderByDescending(r => r.StartedAt).Take(10).Select(r => new { r.Id, agent = r.AiAgent!.Name, r.Status, r.Checked, r.Fixed, r.Flagged, r.StartedAt, r.Summary }).ToListAsync(),
        });
    }
}

[Route("api/sheriff"), RequirePerm("sheriff")]
public class SheriffExtrasController(AppDbContext db, FileStore files, IConfiguration config) : ApiControllerBase
{
    [HttpGet("heartbeat")]
    public async Task<IActionResult> Heartbeat()
    {
        var latest = await db.Heartbeats.OrderByDescending(h => h.CheckedAt).FirstOrDefaultAsync();
        var history = await db.Heartbeats.OrderByDescending(h => h.CheckedAt).Take(48).Select(h => new { h.CheckedAt, h.Status, h.DurationMs }).ToListAsync();
        return Ok(new
        {
            latest, history, checks = Health.Checks,
            controls = await db.HeartbeatControls.Include(c => c.User).Select(c => new { c.Key, c.State, c.Reason, c.ChangedAt, who = c.User != null ? c.User.Name : null }).ToListAsync(),
            pulses = await db.HeartbeatPulses.ToListAsync(),
        });
    }

    [HttpPost("heartbeat/check")]
    public async Task<IActionResult> Check(CancellationToken ct) => Ok(await Health.CheckAsync(db, files, config, ct));

    public record ControlRequest(string? Reason);

    /// <summary>Controls: put the website in maintenance (or back), restart the queue, clear caches, send a test email.</summary>
    [HttpPost("heartbeat/{key}/{control}")]
    public async Task<IActionResult> Control(string key, string control, ControlRequest r, [FromServices] Microsoft.Extensions.Caching.Hybrid.HybridCache cache)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string output; var ok = true;
        switch (control)
        {
            case "down" or "up":
                var c = await db.HeartbeatControls.AsTracking().FirstOrDefaultAsync(x => x.Key == key);
                if (c is null) db.HeartbeatControls.Add(c = new HeartbeatControl { Key = key });
                c.State = control; c.Reason = r.Reason; c.UserId = Me.UserId; c.ChangedAt = DateTime.UtcNow;
                output = key == "website" ? (control == "down" ? "The website shows the maintenance page to visitors (signed-in staff still see it)." : "The website is back for everyone.") : $"{key} marked {control}.";
                break;
            case "clear_cache":
                await cache.RemoveByTagAsync("*");
                output = "Server caches cleared.";
                break;
            case "run_queue":
                var queued = await db.ReportRuns.AsTracking().Where(x => x.Status == "queued").ToListAsync();
                foreach (var run in queued) await Reports.ExecuteAsync(db, files, run, HttpContext.RequestAborted);
                output = $"Ran {queued.Count} queued reports.";
                break;
            case "send_test":
                ok = !string.IsNullOrEmpty(config["Mail:Host"]);
                output = ok ? $"Test email sent to {Me.Email}." : "Mail is set to log, nothing sent. Set Mail:Host to deliver.";
                break;
            default: return Problem422("Unknown control.");
        }
        db.HeartbeatActions.Add(new HeartbeatAction { CheckKey = key, Action = control, UserId = Me.UserId, Ok = ok, Output = output, DurationMs = (int)sw.ElapsedMilliseconds });
        Audit(db, "heartbeat_control", key, control);
        await db.SaveChangesAsync();
        return Ok(new { message = output });
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> JobsList()
    {
        var runs = await db.JobRuns.OrderByDescending(r => r.StartedAt).Take(500).ToListAsync();
        return Ok(Jobs.All.Select(j =>
        {
            var mine = runs.Where(r => r.Command == j.Command).ToList();
            return new { j.Command, j.Name, j.Schedule, last = mine.FirstOrDefault(), failures = mine.Take(10).Count(r => r.Status == "Failed"), recent = mine.Take(10).Select(r => new { r.Id, r.Status, r.StartedAt, r.DurationMs, r.Message }) };
        }));
    }

    [HttpPost("jobs/{command}/run")]
    public async Task<IActionResult> RunJob(string command, CancellationToken ct)
    {
        if (Jobs.All.All(j => j.Command != command)) return NotFound();
        var run = await Jobs.RunAsync(db, files, command, Me.UserId, ct);
        return Ok(new { run.Id, run.Status, run.Message, run.DurationMs });
    }

    private static readonly Dictionary<string, string[]> IntegrationEnv = new()
    {
        ["aig"] = ["AIG_ENDPOINT", "AIG_PARTNER_ID", "AIG_API_KEY"], ["amazon"] = ["AWS_DEFAULT_REGION", "AWS_BUCKET", "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY"],
        ["stripe"] = ["STRIPE_KEY", "STRIPE_SECRET"], ["twilio"] = ["TWILIO_SID", "TWILIO_TOKEN", "TWILIO_FROM"], ["salesforce"] = ["SALESFORCE_CLIENT_ID", "SALESFORCE_CLIENT_SECRET"],
        ["utilibill"] = ["UTILIBILL_API_KEY", "UTILIBILL_URL"], ["experian"] = ["EXPERIAN_USER", "EXPERIAN_PASSWORD"], ["authnet"] = ["AUTHNET_LOGIN_ID", "AUTHNET_TRANSACTION_KEY"],
    };

    private static string Key(string name) => name.ToLowerInvariant().Replace(" ", "").Replace(".", "");

    /// <summary>Integrations (seed apis.json): whether each key is set (never the value) and the latest connection tests.</summary>
    [HttpGet("apis")]
    public async Task<IActionResult> Apis()
    {
        var tests = await db.IntegrationTests.OrderByDescending(t => t.CreatedAt).Take(300).ToListAsync();
        var apis = JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "data", "apis.json")))!.AsArray();
        return Ok(apis.Select(a =>
        {
            var key = Key(a!["name"]!.ToString());
            var env = IntegrationEnv.GetValueOrDefault(key) ?? a["fields"]!.AsArray().Select(f => $"{key.ToUpperInvariant()}_{f!.ToString().ToUpperInvariant()}").ToArray();
            return new
            {
                key, name = a["name"]!.ToString(), purpose = a["purpose"]?.ToString(),
                settings = env.Select(e => new { name = e, set = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(e)) }),
                last = tests.FirstOrDefault(t => t.Integration == key), calls = 0,
            };
        }));
    }

    [HttpPost("apis/{key}/test")]
    public async Task<IActionResult> TestApi(string key)
    {
        var env = IntegrationEnv.GetValueOrDefault(key) ?? [];
        var missing = env.Where(e => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(e))).ToList();
        var ok = env.Length > 0 && missing.Count == 0;
        var t = new IntegrationTest { Integration = key, Ok = ok, UserId = Me.UserId, ResponseMs = 0, Message = ok ? "Keys are set; the live call isn't written yet." : $"Not configured: {string.Join(", ", missing.DefaultIfEmpty("keys"))}." };
        db.IntegrationTests.Add(t);
        await db.SaveChangesAsync();
        return Ok(t);
    }

    [HttpPost("apis/test-all")]
    public async Task<IActionResult> TestAll()
    {
        foreach (var key in IntegrationEnv.Keys) await TestApi(key);
        return Ok(new { message = $"Tested {IntegrationEnv.Count} integrations." });
    }

    /// <summary>Timeline: everything that ran or changed, newest first: sign-ins and access changes, jobs, reports, history, API calls.</summary>
    [HttpGet("timeline/all")]
    public async Task<IActionResult> Timeline([FromQuery] string? q, [FromQuery] int? userId, [FromQuery] string? kind, [FromQuery] int days = 7)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 90));
        var items = new List<(DateTime At, string Kind, string Who, string What, string? Detail, string? Link)>();
        if (kind is null or "admin")
            items.AddRange((await db.AuditEntries.Where(a => a.CreatedAt > since && (userId == null || a.UserId == userId)).ToListAsync()).Select(a => (a.CreatedAt, "admin", a.Actor, $"{a.Action.Replace('_', ' ')} {a.Subject}", a.Detail, (string?)null)));
        if (kind is null or "job" && userId is null)
            items.AddRange((await db.JobRuns.Where(j => j.StartedAt > since).ToListAsync()).Select(j => (j.StartedAt, "job", "Scheduler", $"{j.Command}: {j.Status}", j.Message, (string?)"/sheriff/jobs")));
        if (kind is null or "report")
            items.AddRange((await db.ReportRuns.Include(r => r.User).Where(r => r.CreatedAt > since && (userId == null || r.UserId == userId)).ToListAsync()).Select(r => (r.CreatedAt, "report", r.User?.Name ?? "", $"{r.Title}: {r.Status}", (string?)$"{r.Rows} rows", (string?)$"/walker/runs/{r.Id}")));
        if (kind is null or "history")
            items.AddRange((await db.HistoryItems.Include(h => h.User).Where(h => h.CreatedAt > since && (userId == null || h.UserId == userId)).ToListAsync()).Select(h => (h.CreatedAt, "history", h.User?.Name ?? "System", $"{h.Model} {h.Action}", h.Summary, h.CustomerId is { } c ? $"/corral/customers/{c}" : null)));
        if (kind is null or "api" && userId is null)
            items.AddRange((await db.ApiLogs.Where(a => a.CreatedAt > since).ToListAsync()).Select(a => (a.CreatedAt, "api", a.Api, $"{a.Action}: {a.Status}", (string?)$"{a.ResponseMs} ms", (string?)$"/sheriff/r/api-calls/{a.Id}")));
        if (!string.IsNullOrWhiteSpace(q)) items = items.Where(i => $"{i.Who} {i.What} {i.Detail}".Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        return Ok(items.OrderByDescending(i => i.At).Take(300).Select(i => new { at = i.At, i.Kind, i.Who, i.What, i.Detail, i.Link }));
    }
}

/// <summary>The person menu: notifications, My Files (your report runs), settings and password.</summary>
[Route("api/account")]
public class AccountController(AppDbContext db, AccessService access) : ApiControllerBase
{
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications([FromQuery] bool unread = false)
    {
        var q = db.AdminNotifications.Where(n => n.UserId == Me.UserId);
        if (unread) q = q.Where(n => n.ReadAt == null);
        return Ok(new { unread = await db.AdminNotifications.CountAsync(n => n.UserId == Me.UserId && n.ReadAt == null), rows = await q.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync() });
    }

    [HttpPost("notifications/{id:int}/read")]
    public async Task<IActionResult> Read(int id)
    {
        await db.AdminNotifications.Where(n => n.Id == id && n.UserId == Me.UserId).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
        return Ok(new { ok = true });
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> ReadAll()
    {
        await db.AdminNotifications.Where(n => n.UserId == Me.UserId && n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
        return Ok(new { ok = true });
    }

    [HttpGet("files")]
    public async Task<IActionResult> Files() => Ok(await db.ReportRuns.Where(r => r.UserId == Me.UserId).OrderByDescending(r => r.CreatedAt).Take(200)
        .Select(r => new { r.Id, r.Title, r.Status, r.Rows, r.Format, r.FileSize, r.CreatedAt, r.FinishedAt, r.SeenAt, r.App }).ToListAsync());

    private static readonly string[] Kinds = ["ticket", "report", "email", "refund", "campaign", "system"];

    [HttpGet("settings")]
    public async Task<IActionResult> Settings()
    {
        var u = await db.Users.FirstAsync(x => x.Id == Me.UserId);
        return Ok(new { kinds = Kinds, prefs = Kinds.ToDictionary(k => k, k => u.NotificationPrefs?[k]?.GetValue<bool>() ?? true), u.Name, u.Email, u.PhoneExt, u.Location });
    }

    public record SettingsRequest(Dictionary<string, bool> Prefs);

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(SettingsRequest r)
    {
        var u = await db.Users.AsTracking().FirstAsync(x => x.Id == Me.UserId);
        u.NotificationPrefs = new JsonObject(r.Prefs.Where(p => Kinds.Contains(p.Key)).Select(p => new KeyValuePair<string, JsonNode?>(p.Key, p.Value)));
        await db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    public record PasswordRequest(string Current, string New);

    [HttpPut("password")]
    public async Task<IActionResult> Password(PasswordRequest r)
    {
        var u = await db.Users.AsTracking().FirstAsync(x => x.Id == Me.UserId);
        if (!Passwords.Verify(u.Password, r.Current)) return Problem422("Your current password isn't right.", "current");
        if (r.New.Length < 12) return Problem422("Use at least 12 characters.", "new");
        u.Password = Passwords.Hash(r.New);
        u.SecurityStamp = Guid.NewGuid().ToString("N");   // signs out your other sessions
        Audit(db, "password_changed", u.Name);
        await db.SaveChangesAsync();
        await access.InvalidateAsync(u.Id);
        return Ok(new { message = "Password changed. Sign in again with the new one." });
    }
}
