using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Resources;

public static partial class ResourceRegistry
{
    private static IEnumerable<ResourceDef> Walker()
    {
        const string app = "walker";
        yield return new ResourceDef
        {
            Key = "report-runs", App = app, Entity = typeof(ReportRun), Title = "Report Runs", Singular = "Report Run", Create = false, Edit = false, Delete = true,
            List = ["CreatedAt", "Title", "UserId", "Status", "Rows", "Format", "DurationMs"], Search = ["Title", "Report"], Filters = ["Status", "Report", "Format", "App"], Sort = "-CreatedAt",
            Actions = [new() { Key = "rerun", Label = "Rerun", Run = c =>
            {
                var r = Row<ReportRun>(c);
                c.Db.ReportRuns.Add(new ReportRun { UserId = c.Me.UserId, Report = r.Report, Title = r.Title, Model = r.Model, App = r.App, Params = r.Params?.DeepClone() ?? new JsonObject(), Format = r.Format, Summary = r.Summary, Status = "queued", RerunOf = r.Id, SavedReportId = r.SavedReportId });
                return Task.FromResult("Queued again; it shows under Report Status when it finishes.");
            } }],
        };
        yield return new ResourceDef
        {
            Key = "saved-reports", App = app, Entity = typeof(SavedReport), Title = "Saved Reports", Singular = "Saved Report", Delete = true,
            List = ["Name", "Report", "Format", "Shared", "UserId", "LastRunAt"], Search = ["Name", "Report"], Filters = ["Report", "Shared"], Sort = "Name", ReadOnly = ["UserId", "LastRunAt"],
            Options = new() { ["Report"] = Keys("walker.reports"), ["Format"] = ["xlsx", "csv", "tsv", "json"] },
            BeforeSave = c => { var s = Row<SavedReport>(c); if (s.UserId == 0) s.UserId = c.Me.UserId; return Task.CompletedTask; },
            Children = [("Runs", "report-runs", "SavedReportId")],
            Actions = [new() { Key = "run", Label = "Run Now", Run = c =>
            {
                var s = Row<SavedReport>(c);
                var title = AppConfig.At($"walker.reports.{s.Report}.title")?.ToString() ?? s.Report;
                c.Db.ReportRuns.Add(new ReportRun { UserId = c.Me.UserId, Report = s.Report, Title = $"{title} ({s.Name})", App = "walker", Params = s.Params?.DeepClone() ?? new JsonObject(), Format = s.Format, Summary = s.Summary, Status = "queued", SavedReportId = s.Id });
                s.LastRunAt = Now;
                return Task.FromResult("Queued.");
            } }],
        };
        yield return new ResourceDef
        {
            Key = "report-uploads", App = app, Entity = typeof(ReportUpload), Title = "All Files", Singular = "File", Create = false, Delete = true,
            List = ["Title", "Category", "OriginalName", "Size", "UserId", "CreatedAt"], Search = ["Title", "OriginalName", "Description"], Filters = ["Category"], Sort = "-CreatedAt",
            Options = new() { ["Category"] = Keys("walker.folders") }, ReadOnly = ["Path", "OriginalName", "Size", "UserId"],
        };
        yield return Child<ReportColumn>("report-columns", app, "Export Columns", ["Report", "UserId", "UpdatedAt"], ["Report"], ["Report"], "Report", options: new() { ["Report"] = Keys("walker.reports") });
    }

    private static IEnumerable<ResourceDef> Caboose()
    {
        const string app = "caboose";
        yield return new ResourceDef
        {
            Key = "refunds", App = app, Entity = typeof(Refund), Title = "Refunds", Singular = "Refund",
            Description = "Request → approve → paid. Approving, denying and paying need the Refunds right.",
            List = ["CreatedAt", "CustomerId", "Amount", "Reason", "Method", "Status", "RequestedBy", "DecidedBy"], Search = ["Reason", "CheckNumber"], Filters = ["Status", "Method", "Kind"], Sort = "-CreatedAt",
            Options = new() { ["Method"] = ["Check", "ACH", "Card"], ["Kind"] = ["credit_balance", "deposit", "overpayment", "goodwill"], ["Status"] = ["requested", "approved", "denied", "paid"] },
            ReadOnly = ["Status", "RequestedBy", "DecidedBy", "DecidedAt", "PaidAt", "DecisionNote"],
            BeforeSave = c => { var r = Row<Refund>(c); if (r.Id == 0) { r.Status = "requested"; r.RequestedBy = c.Me.UserId; if (r.Amount <= 0) throw new ActionError("Amount must be more than $0."); } return Task.CompletedTask; },
            Actions =
            [
                new() { Key = "approve", Label = "Approve", Perm = "refunds", When = r => ((Refund)r).Status == "requested", Run = c =>
                {
                    var r = Row<Refund>(c);
                    if (r.RequestedBy == c.Me.UserId) throw new ActionError("Someone other than the requester has to approve it.");
                    r.Status = "approved"; r.DecidedBy = c.Me.UserId; r.DecidedAt = Now; r.DecisionNote = c.Input["note"]?.ToString();
                    if (r.RequestedBy is { } who) Notify(c, who, "refund", $"Refund approved: ${r.Amount:N2}", null, $"/caboose/r/refunds/{r.Id}");
                    return Task.FromResult("Approved.");
                } },
                new() { Key = "deny", Label = "Deny", Perm = "refunds", Tone = "danger", Inputs = new() { ["note"] = "Why" }, When = r => ((Refund)r).Status == "requested", Run = c =>
                {
                    var r = Row<Refund>(c); r.Status = "denied"; r.DecidedBy = c.Me.UserId; r.DecidedAt = Now; r.DecisionNote = In(c, "note");
                    return Task.FromResult("Denied.");
                } },
                new() { Key = "paid", Label = "Mark Paid", Perm = "refunds", Inputs = new() { ["check_number"] = "Check number (for checks)" }, When = r => ((Refund)r).Status == "approved", Run = async c =>
                {
                    var r = Row<Refund>(c);
                    r.Status = "paid"; r.PaidAt = Now; r.CheckNumber = In(c, "check_number", r.Method == "Check");
                    var cust = await c.Db.Customers.AsTracking().FirstAsync(x => x.Id == r.CustomerId);
                    cust.Balance += r.Amount;
                    AccountNote(c, cust.Id, $"Refund of ${r.Amount:N2} paid by {r.Method}{(r.CheckNumber is { Length: > 0 } n ? " #" + n : "")}.", "Payment", "Duplicate Payment/Refund");
                    return "Marked paid; the balance is updated.";
                } },
            ],
        };
        yield return new ResourceDef
        {
            Key = "pending-ledger", App = app, Entity = typeof(LedgerEntry), Title = "Credits & Debits", Singular = "Credit/Debit",
            Description = "Credits and debits waiting to be applied to accounts.", List = ["CreatedAt", "CustomerId", "Kind", "Amount", "Description", "Status", "UserId"],
            Search = ["Description"], Filters = ["Status", "Kind"], Sort = "-CreatedAt", Create = false, Edit = false, Actions = LedgerActions(),
        };
        yield return new ResourceDef
        {
            Key = "caboose-payments", App = app, Entity = typeof(Payment), Title = "Payments", Singular = "Payment",
            Description = "Record a check, money order, cash or wire payment; it comes off the balance.",
            List = ["PaidOn", "Reference", "CustomerId", "Amount", "Method", "Source", "Status"], Search = ["Reference", "Confirmation"], Filters = ["Status", "Method", "Source"], Sort = "-PaidOn", Edit = false,
            Options = new() { ["Method"] = ["Check", "Money Order", "Cash", "Wire", "ACH", "Card"], ["Kind"] = ["Balance Payment", "Pre-Payment", "Deposit", "Purchase"] },
            ReadOnly = ["Status", "Source", "ReversedAt", "ReversedBy", "Confirmation"],
            BeforeSave = async c =>
            {
                var p = Row<Payment>(c);
                if (p.Amount <= 0) throw new ActionError("Amount must be more than $0.");
                p.Status = "Success"; p.Source = "Back Office"; p.Reference = string.IsNullOrEmpty(p.Reference) ? "BO-" + Now.ToString("yyMMddHHmmss") : p.Reference;
                if (p.PaidOn == default) p.PaidOn = DateOnly.FromDateTime(DateTime.Today);
                var cust = await c.Db.Customers.AsTracking().FirstOrDefaultAsync(x => x.Id == p.CustomerId) ?? throw new ActionError("Pick the account.");
                cust.Balance -= p.Amount;
                AccountNote(c, cust.Id, $"{p.Method} payment of ${p.Amount:N2} recorded ({p.Reference}).", "Payment", "Payment Made");
            },
            Actions = PaymentActions(),
        };
        yield return new ResourceDef
        {
            Key = "marketing-channels", App = app, Entity = typeof(MarketingChannel), Title = "MSIDs", Singular = "MSID",
            List = ["Msid", "Name", "Type", "Active"], Search = ["Msid", "Name"], Filters = ["Type", "Active"], Sort = "Msid", Options = new() { ["Type"] = ["Organic", "Agent", "Paid", "Partner", "Broker"] },
        };
        yield return new ResourceDef
        {
            Key = "promo-codes", App = app, Entity = typeof(PromoCode), Title = "Promo Codes", Singular = "Promo Code",
            List = ["Code", "Description", "Credit", "StartsOn", "EndsOn", "Active"], Search = ["Code", "Description"], Filters = ["Active"], Sort = "Code",
        };
        yield return new ResourceDef
        {
            Key = "reference-rows", App = "sheriff", Entity = typeof(ReferenceRow), Title = "Reference Data Rows", Singular = "Row",
            List = ["TableKey", "Position", "Cells"], Filters = ["TableKey"], Sort = "Position", Delete = true, Options = new() { ["TableKey"] = Keys("admin.reference_tables") },
        };
    }

    private static IEnumerable<ResourceDef> Bounty()
    {
        const string app = "bounty";
        yield return new ResourceDef
        {
            Key = "reward-offers", App = app, Entity = typeof(RewardOffer), Title = "Offers", Singular = "Offer",
            List = ["Name", "Stars", "Effect", "Value", "Active", "Position"], Search = ["Name", "Description"], Filters = ["Effect", "Active"], Sort = "Position",
            Options = new() { ["Effect"] = ["bill_credit", "gift_card", "drawing", "donation", "merch"] },
        };
        yield return new ResourceDef
        {
            Key = "reward-rules", App = app, Entity = typeof(RewardRule), Title = "Earning Rules", Singular = "Earning Rule",
            Description = "How stars are earned (used by the et:rewards-stars job). Per dollar: stars × amount paid.",
            List = ["Label", "Key", "Stars", "PerDollar", "Active"], Sort = "Id",
        };
        yield return new ResourceDef
        {
            Key = "redemptions", App = app, Entity = typeof(StarEntry), Title = "Redemptions", Singular = "Redemption", Create = false, Edit = false,
            Description = "Stars spent on offers; fulfil gift cards and credits here.", List = ["CreatedAt", "CustomerId", "RewardOfferId", "Stars", "Reason", "Fulfillment"],
            Search = ["Reason"], Filters = ["Fulfillment", "RewardOfferId"], Sort = "-CreatedAt",
            Actions = [new() { Key = "fulfil", Label = "Mark Fulfilled", When = r => ((StarEntry)r).Fulfillment == "pending", Run = c => { Row<StarEntry>(c).Fulfillment = "fulfilled"; return Task.FromResult("Fulfilled."); } }],
        };
        yield return new ResourceDef
        {
            Key = "members", App = app, Entity = typeof(Customer), Title = "Members", Singular = "Member", Create = false, Edit = false,
            List = ["Account", "Name", "Stars", "Status", "City"], Search = ["Account", "Name", "Email"], Filters = ["Status"], Sort = "-Stars",
            Children = [("Stars", "star-entries", "CustomerId"), ("Redemptions", "redemptions", "CustomerId")],
            Actions =
            [
                new() { Key = "adjust", Label = "Adjust Stars", Inputs = new() { ["stars"] = "Stars (+ to add, − to take away)", ["reason"] = "Reason" }, Run = c =>
                {
                    var cust = Row<Customer>(c);
                    if (!int.TryParse(In(c, "stars"), out var n) || n == 0) throw new ActionError("Enter a whole number of stars.");
                    if (cust.Stars + n < 0) throw new ActionError($"They only have {cust.Stars:N0} stars.");
                    cust.Stars += n;
                    c.Db.StarEntries.Add(new StarEntry { CustomerId = cust.Id, Stars = n, Reason = "Adjustment: " + In(c, "reason"), UserId = c.Me.UserId });
                    return Task.FromResult($"Stars now {cust.Stars:N0}.");
                } },
                new() { Key = "redeem", Label = "Redeem an Offer", Inputs = new() { ["offer_id"] = "Offer #" }, Run = async c =>
                {
                    var cust = Row<Customer>(c);
                    var offer = await c.Db.RewardOffers.FirstOrDefaultAsync(o => o.Id.ToString() == In(c, "offer_id") && o.Active) ?? throw new ActionError("No active offer with that number.");
                    if (cust.Stars < offer.Stars) throw new ActionError($"{offer.Name} needs {offer.Stars:N0} stars; they have {cust.Stars:N0}.");
                    cust.Stars -= offer.Stars;
                    c.Db.StarEntries.Add(new StarEntry { CustomerId = cust.Id, Stars = -offer.Stars, Reason = "Redeemed: " + offer.Name, RewardOfferId = offer.Id, Fulfillment = offer.Effect == "bill_credit" ? "fulfilled" : "pending", UserId = c.Me.UserId });
                    if (offer.Effect == "bill_credit" && decimal.TryParse(offer.Value, out var credit)) cust.Balance -= credit;
                    return $"Redeemed {offer.Name}.";
                } },
            ],
        };
    }

    private static IEnumerable<ResourceDef> Rodeo()
    {
        const string app = "rodeo";
        yield return new ResourceDef
        {
            Key = "campaigns", App = app, Entity = typeof(Campaign), Title = "Campaigns", Singular = "Campaign",
            List = ["Name", "Channel", "Status", "Recipients", "SentAt", "ScheduledAt", "CreatedBy"], Search = ["Name", "Description"], Filters = ["Channel", "Status", "Goal"], Sort = "-CreatedAt",
            Options = new() { ["Channel"] = ["Email", "SMS"], ["Status"] = ["draft", "scheduled", "sent"], ["Goal"] = ["renewals", "product", "winback", "engagement", "survey"] },
            ReadOnly = ["Status", "SentAt", "Recipients", "Skipped", "CreatedBy"], Children = [("Sent", "contact-logs", "CampaignId"), ("Clicks", "link-clicks", "CampaignId")],
            BeforeSave = c => { var x = Row<Campaign>(c); x.CreatedBy ??= c.Me.UserId; x.Audience ??= new JsonObject(); if (x.Id == 0) x.Status = "draft"; return Task.CompletedTask; },
            Actions =
            [
                new() { Key = "send", Label = "Send Now", Confirm = "Send this campaign to everyone in its audience now?", When = r => ((Campaign)r).Status != "sent", Run = async c => await Campaigns.SendAsync(c.Db, Row<Campaign>(c)) },
                new() { Key = "schedule", Label = "Schedule", Inputs = new() { ["at"] = "Send at (YYYY-MM-DD HH:MM)" }, When = r => ((Campaign)r).Status == "draft", Run = c =>
                {
                    if (!DateTime.TryParse(In(c, "at"), out var at) || at < DateTime.Now) throw new ActionError("Pick a time in the future.");
                    var x = Row<Campaign>(c); x.ScheduledAt = at.ToUniversalTime(); x.Status = "scheduled";
                    return Task.FromResult($"Scheduled for {at:MMM d, h:mm tt}; the et:campaigns job sends it.");
                } },
                new() { Key = "unschedule", Label = "Unschedule", When = r => ((Campaign)r).Status == "scheduled", Run = c => { var x = Row<Campaign>(c); x.Status = "draft"; x.ScheduledAt = null; return Task.FromResult("Back to draft."); } },
                new() { Key = "duplicate", Label = "Duplicate", Run = c =>
                {
                    var x = Row<Campaign>(c);
                    c.Db.Campaigns.Add(new Campaign { Name = x.Name + " (copy)", Channel = x.Channel, EmailTemplateId = x.EmailTemplateId, Message = x.Message, Audience = x.Audience?.DeepClone() ?? new JsonObject(), Status = "draft", CreatedBy = c.Me.UserId, Goal = x.Goal, Description = x.Description, Product = x.Product, Msid = x.Msid, PromoCode = x.PromoCode, CapDays = x.CapDays });
                    return Task.FromResult("Copied as a new draft.");
                } },
            ],
        };
        yield return new ResourceDef
        {
            Key = "email-templates", App = app, Entity = typeof(EmailTemplate), Title = "Email Templates", Singular = "Email Template",
            Description = "Placeholders: {{first_name}}, {{name}}, {{account}}, {{plan}}, {{balance}}, {{due_date}}, {{survey:<slug>}}.",
            List = ["Name", "Subject", "EmailCategoryId", "EmailHeaderId", "EmailFooterId", "UpdatedAt"], Search = ["Name", "Subject", "Body"], Filters = ["EmailCategoryId"], Sort = "Name",
            Children = [("Test Sends", "email-test-sends", "EmailTemplateId"), ("Campaigns", "campaigns", "EmailTemplateId"), ("Clicks", "link-clicks", "EmailTemplateId")],
        };
        yield return new ResourceDef { Key = "email-parts", App = app, Entity = typeof(EmailPart), Title = "Headers & Footers", Singular = "Header/Footer", List = ["Name", "Kind", "IsDefault", "UpdatedAt"], Filters = ["Kind"], Sort = "Kind", Delete = true, Options = new() { ["Kind"] = ["header", "footer"] } };
        yield return new ResourceDef { Key = "email-categories", App = app, Entity = typeof(EmailCategory), Title = "Email Categories", Singular = "Category", List = ["Name", "Description", "Color", "Position"], Sort = "Position", Delete = true, Children = [("Templates", "email-templates", "EmailCategoryId")] };
        yield return new ResourceDef
        {
            Key = "email-suppressions", App = app, Entity = typeof(EmailSuppression), Title = "Suppression List", Singular = "Suppressed Address", Edit = false, Delete = true,
            Description = "Addresses that never get marketing email: bounces, unsubscribes, complaints and manual requests.",
            List = ["Email", "Reason", "Note", "CustomerId", "CreatedAt"], Search = ["Email", "Note"], Filters = ["Reason"], Sort = "-CreatedAt",
            Options = new() { ["Reason"] = ["bounce", "unsubscribe", "complaint", "manual"] }, ReadOnly = ["UserId"],
            BeforeSave = c => { var s = Row<EmailSuppression>(c); s.UserId ??= c.Me.UserId; s.Email = s.Email.Trim().ToLowerInvariant(); return Task.CompletedTask; },
        };
        yield return new ResourceDef
        {
            Key = "sent-emails", App = app, Entity = typeof(ContactLog), Title = "Sent Emails", Singular = "Email", Create = false, Edit = false,
            List = ["SentAt", "CustomerId", "Template", "Status", "OpenedAt", "ClickedAt", "DroppedAt"], Search = ["Template"], Filters = ["Status", "CampaignId"], Sort = "-CreatedAt",
            Fixed = new() { ["Channel"] = "Email" },
        };
        yield return Child<EmailTestSend>("email-test-sends", app, "Test Sends", ["CreatedAt", "EmailTemplateId", "UserId", "Mode", "Target", "Status"], ["Target"], ["Mode", "Status"], "-CreatedAt", create: false, edit: false);
        yield return Child<LinkClick>("link-clicks", app, "Link Clicks", ["ClickedAt", "Link", "Url", "Device", "CustomerId", "CampaignId"], ["Link", "Url"], ["Device"], "-ClickedAt", create: false, edit: false);
        yield return new ResourceDef
        {
            Key = "pdf-templates", App = app, Entity = typeof(PdfTemplate), Title = "PDF Templates", Singular = "PDF Template", Delete = true,
            List = ["Name", "Category", "Title", "Status", "PdfHeaderId", "PdfFooterId"], Search = ["Name", "Title", "Body"], Filters = ["Category", "Status"], Sort = "Name",
            Options = new() { ["Status"] = ["active", "draft"] }, Children = [("PDFs Made", "pdf-generations", "PdfTemplateId"), ("Test Sends", "pdf-test-sends", "PdfTemplateId")],
            Actions = [new() { Key = "make", Label = "Make a PDF", Inputs = new() { ["account"] = "Account number" }, Run = async c => await Pdfs.MakeAsync(c.Db, Row<PdfTemplate>(c), In(c, "account"), c.Me.UserId) }],
        };
        yield return new ResourceDef { Key = "pdf-parts", App = app, Entity = typeof(PdfPart), Title = "PDF Headers & Footers", Singular = "PDF Header/Footer", List = ["Name", "Kind", "Title", "Color", "IsDefault"], Filters = ["Kind"], Sort = "Kind", Delete = true, Options = new() { ["Kind"] = ["header", "footer"] } };
        yield return Child<PdfGeneration>("pdf-generations", app, "PDFs Made", ["CreatedAt", "FileName", "PdfTemplateId", "CustomerId", "UserId", "Pages", "Size"], ["FileName", "Title"], ["PdfTemplateId"], "-CreatedAt", create: false, edit: false);
        yield return Child<PdfTestSend>("pdf-test-sends", app, "PDF Test Sends", ["CreatedAt", "PdfTemplateId", "Recipients", "Status"], [], ["Status"], "-CreatedAt", create: false, edit: false);
        // Surveys
        var qtypes = new[] { "rating", "scale", "single", "multi", "yes_no", "text" };
        yield return new ResourceDef
        {
            Key = "surveys", App = app, Entity = typeof(Survey), Title = "Surveys", Singular = "Survey", Delete = true,
            List = ["Title", "Slug", "Status", "OpensOn", "ClosesOn"], Search = ["Title", "Slug"], Filters = ["Status"], Sort = "-CreatedAt",
            Options = new() { ["Status"] = ["draft", "active", "closed"] }, Children = [("Questions", "survey-questions", "SurveyId"), ("Responses", "survey-responses", "SurveyId")],
            Actions = [new() { Key = "copy", Label = "Copy", Run = async c =>
            {
                var s = Row<Survey>(c);
                var copy = new Survey { Title = s.Title + " (copy)", Slug = s.Slug + "-copy-" + Now.ToString("HHmmss"), Intro = s.Intro, ThankYou = s.ThankYou, Status = "draft" };
                foreach (var q in await c.Db.SurveyQuestions.Where(q => q.SurveyId == s.Id).OrderBy(q => q.Position).ToListAsync())
                    copy.Questions.Add(new SurveyQuestion { Question = q.Question, Type = q.Type, Options = q.Options?.DeepClone(), Position = q.Position, SurveyCategoryId = q.SurveyCategoryId, SurveyAnswerSetId = q.SurveyAnswerSetId, SurveyBankQuestionId = q.SurveyBankQuestionId, Required = q.Required, Help = q.Help });
                c.Db.Surveys.Add(copy);
                return "Copied as a draft.";
            } }],
        };
        yield return Child<SurveyQuestion>("survey-questions", app, "Survey Questions", ["Position", "Question", "Type", "Required", "SurveyCategoryId", "SurveyId"], ["Question"], ["Type", "SurveyCategoryId"], "Position", delete: true, options: new() { ["Type"] = qtypes });
        yield return Child<SurveyResponse>("survey-responses", app, "Survey Responses", ["CreatedAt", "SurveyId", "CustomerId", "Source"], [], ["SurveyId", "Source"], "-CreatedAt", create: false, edit: false);
        yield return new ResourceDef { Key = "survey-bank", App = app, Entity = typeof(SurveyBankQuestion), Title = "Question Bank", Singular = "Question", List = ["Question", "Type", "SurveyCategoryId", "SurveyAnswerSetId"], Search = ["Question"], Filters = ["Type", "SurveyCategoryId"], Sort = "Question", Delete = true, Options = new() { ["Type"] = qtypes } };
        yield return new ResourceDef { Key = "survey-answer-sets", App = app, Entity = typeof(SurveyAnswerSet), Title = "Answer Sets", Singular = "Answer Set", List = ["Name", "Group", "Type", "Options"], Search = ["Name"], Filters = ["Group", "Type"], Sort = "Name", Delete = true, Options = new() { ["Type"] = ["scale", "single", "multi"] } };
        yield return new ResourceDef { Key = "survey-categories", App = app, Entity = typeof(SurveyCategory), Title = "Survey Categories", Singular = "Category", List = ["Name", "Description", "Color", "Position"], Sort = "Position", Delete = true };
    }

    private static IEnumerable<ResourceDef> Deputy()
    {
        const string app = "deputy";
        yield return new ResourceDef
        {
            Key = "ai-conversations", App = app, Entity = typeof(AiConversation), Title = "Conversations", Singular = "Conversation", Create = false,
            List = ["StartedAt", "Channel", "Topic", "CustomerId", "AiAgentId", "Status", "Outcome", "HandedToId"], Search = ["Topic", "Transcript"], Filters = ["Channel", "Status", "Outcome", "AiAgentId"], Sort = "-StartedAt",
            ReadOnly = ["Transcript", "InputTokens", "OutputTokens", "DurationSec", "StartedAt", "LastMessageAt", "Status", "HandedToId", "UserId"],
            Actions =
            [
                new() { Key = "take-over", Label = "Take Over", When = r => ((AiConversation)r).Status is "bot" or "waiting", Run = c =>
                {
                    var x = Row<AiConversation>(c); x.Status = "agent"; x.HandedToId = c.Me.UserId; x.UserId = c.Me.UserId; x.Outcome = "handed_off";
                    x.Transcript += $"\nSystem: {c.Me.Name} took over."; x.LastMessageAt = Now;
                    return Task.FromResult("You're answering now; the bot is paused.");
                } },
                new() { Key = "reply", Label = "Reply", Inputs = new() { ["note:message"] = "Message" }, When = r => ((AiConversation)r).Status == "agent", Run = c =>
                {
                    var x = Row<AiConversation>(c); x.Transcript += $"\n{c.Me.Name}: {In(c, "message")}"; x.LastMessageAt = Now;
                    return Task.FromResult("Sent.");
                } },
                new() { Key = "hand-back", Label = "Hand Back to Bot", When = r => ((AiConversation)r).Status == "agent", Run = c => { var x = Row<AiConversation>(c); x.Status = "bot"; x.Transcript += $"\nSystem: {c.Me.Name} handed back to the bot."; return Task.FromResult("The bot is answering again."); } },
                new() { Key = "close", Label = "Close", When = r => ((AiConversation)r).Status != "closed", Run = c => { var x = Row<AiConversation>(c); x.Status = "closed"; if (x.Outcome == "open") x.Outcome = "resolved"; return Task.FromResult("Closed."); } },
            ],
        };
        yield return new ResourceDef
        {
            Key = "ai-agents", App = app, Entity = typeof(AiAgent), Title = "Agents", Singular = "Agent",
            List = ["Name", "Activity", "AiModelId", "FallbackModelId", "Temperature", "Schedule", "Active"], Search = ["Name", "Description"], Filters = ["Activity", "Active"], Sort = "Name",
            Options = new() { ["Activity"] = Keys("deputy.activities") }, Children = [("Conversations", "ai-conversations", "AiAgentId"), ("Automation Runs", "ai-automation-runs", "AiAgentId")],
            Actions = [new() { Key = "test", Label = "Test", Inputs = new() { ["note:message"] = "Customer message" }, Run = async c => await RcBunny.Backend.Deputy.TestAsync(c, Row<AiAgent>(c), In(c, "message")) }],
        };
        yield return new ResourceDef
        {
            Key = "ai-models", App = app, Entity = typeof(AiModel), Title = "Models", Singular = "Model",
            List = ["Name", "Provider", "ModelId", "SizeGb", "Quantization", "ContextWindow", "Status", "CheckedAt"], Search = ["Name", "ModelId"], Filters = ["Provider", "Status"], Sort = "Name",
            Options = new() { ["Provider"] = Keys("deputy.providers"), ["Status"] = ["available", "downloading", "offline", "error"] }, ReadOnly = ["CheckedAt"],
            Actions = [new() { Key = "check", Label = "Check Connection", Run = async c => await RcBunny.Backend.Deputy.CheckAsync(c, Row<AiModel>(c)) }],
        };
        yield return Child<AiAutomationRun>("ai-automation-runs", app, "Automation Runs", ["StartedAt", "AiAgentId", "Status", "Checked", "Fixed", "Flagged", "DurationSec"], ["Summary"], ["Status", "AiAgentId"], "-StartedAt", create: false, edit: false);
    }

    private static IEnumerable<ResourceDef> Sheriff()
    {
        const string app = "sheriff";
        yield return new ResourceDef
        {
            Key = "job-runs", App = app, Entity = typeof(JobRun), Title = "Job Runs", Singular = "Job Run", Create = false, Edit = false,
            List = ["StartedAt", "Command", "Status", "DurationMs", "TriggeredBy", "Message"], Search = ["Command", "Message"], Filters = ["Command", "Status"], Sort = "-StartedAt",
        };
        yield return new ResourceDef
        {
            Key = "automations", App = app, Entity = typeof(AiAgent), Title = "Cron Automations", Singular = "Automation",
            Description = "Deputy agents that run on a schedule (cron expression) to work queues, enrollments, payments, surveys and reports.",
            List = ["Name", "Activity", "Schedule", "AiModelId", "Active"], Search = ["Name"], Filters = ["Activity", "Active"], Sort = "Name",
            Options = new() { ["Activity"] = Keys("deputy.activities") }, Children = [("Runs", "ai-automation-runs", "AiAgentId")],
            Actions = [new() { Key = "toggle", Label = "Turn On/Off", Run = c => { var a = Row<AiAgent>(c); a.Active = !a.Active; return Task.FromResult(a.Active ? "On." : "Off."); } }],
        };
        yield return Child<IntegrationTest>("integration-tests", app, "Connection Tests", ["CreatedAt", "Integration", "Ok", "ResponseMs", "Message", "UserId"], ["Integration", "Message"], ["Integration", "Ok"], "-CreatedAt", create: false, edit: false);
        yield return Child<Heartbeat>("heartbeats", app, "Heartbeat Checks", ["CheckedAt", "Status", "DurationMs"], [], ["Status"], "-CheckedAt", create: false, edit: false);
        yield return Child<HeartbeatAction>("heartbeat-actions", app, "Control Log", ["CreatedAt", "CheckKey", "Action", "Ok", "UserId", "DurationMs"], ["CheckKey", "Output"], ["CheckKey", "Ok"], "-CreatedAt", create: false, edit: false);
        yield return new ResourceDef
        {
            Key = "admin-apps", App = app, Entity = typeof(AdminApp), Title = "Custom Admin Apps", Singular = "Admin App", Delete = true,
            Description = "Apps made with + New Admin App: name, icon, sidebar links (Menu is a list of {heading, items: [{label, path}]}). Who can open one is set in Roles.",
            List = ["Name", "Key", "Description", "Position"], Search = ["Name", "Key"], Sort = "Position", ReadOnly = ["CreatedBy"],
            BeforeSave = c =>
            {
                var a = Row<AdminApp>(c);
                a.CreatedBy ??= c.Me.UserId; a.Menu ??= new JsonArray();
                if (!System.Text.RegularExpressions.Regex.IsMatch(a.Key, "^[a-z0-9][a-z0-9-]*$")) throw new ActionError("Key: lowercase letters, numbers and dashes.");
                if (RcBunny.Backend.Access.AdminApps.Builtin.Any(b => b.Key == a.Key)) throw new ActionError("That key is a built-in app.");
                return Task.CompletedTask;
            },
        };
        yield return new ResourceDef
        {
            Key = "table-snapshots", App = app, Entity = typeof(TableSnapshot), Title = "Cached Tables", Singular = "Cached Table", Create = false, Edit = false, Delete = true,
            List = ["CreatedAt", "Title", "App", "Kind", "RowCount", "UserId"], Search = ["Title", "Key"], Filters = ["App", "Kind"], Sort = "-CreatedAt",
        };
        yield return new ResourceDef
        {
            Key = "audit-entries", App = app, Entity = typeof(AuditEntry), Title = "Audit Log", Singular = "Entry", Create = false, Edit = false,
            List = ["CreatedAt", "Actor", "Action", "Subject", "Detail", "Ip"], Search = ["Actor", "Action", "Subject", "Detail"], Filters = ["Action"], Sort = "-CreatedAt",
        };
        yield return new ResourceDef
        {
            Key = "all-history", App = app, Entity = typeof(HistoryItem), Title = "History", Singular = "History Entry", Create = false, Edit = false,
            List = ["CreatedAt", "UserId", "Model", "Action", "Summary", "CustomerId"], Search = ["Summary", "Model"], Filters = ["Group", "Action", "Model"], Sort = "-CreatedAt",
        };
        yield return Child<ApiLog>("api-calls", app, "API Calls", ["CreatedAt", "Api", "Action", "Status", "ResponseMs", "CustomerId"], ["Api", "Action", "Endpoint", "Request", "Response"], ["Api", "Status"], "-CreatedAt", create: false, edit: false);
    }
}
