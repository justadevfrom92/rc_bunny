using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RcBunny.Data;

// ---- Rodeo: marketing (ebunny's campaigns, email_templates, email_parts, email_categories, email_suppressions,
//      email_test_sends, link_clicks, pdf_templates, pdf_parts, pdf_generations, pdf_test_sends and the survey tables) ----

/// <summary>An email or text sent to a group of customers (Rodeo → Campaigns).</summary>
public class Campaign
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Email or SMS.</summary>
    public string Channel { get; set; } = "Email";
    /// <summary>renewal, product, winback, rewards or announcement (Rodeo.Goals).</summary>
    public string Goal { get; set; } = "announcement";
    public string? Description { get; set; }
    /// <summary>The product a "product" campaign promotes.</summary>
    public string? Product { get; set; }
    public int? EmailTemplateId { get; set; }
    public EmailTemplate? Template { get; set; }
    /// <summary>The text message (SMS).</summary>
    public string? Message { get; set; }
    /// <summary>JSON: the audience filters (statuses, type, market_ids, product, without_product, autopay, balance, contract_ends_within).</summary>
    public string Audience { get; set; } = "{}";
    public string? Msid { get; set; }
    public string? PromoCode { get; set; }
    /// <summary>draft, scheduled or sent.</summary>
    public string Status { get; set; } = "draft";
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    /// <summary>Skip anyone who got a campaign in the last N days (0 = no limit).</summary>
    public int CapDays { get; set; } = 7;
    public int Recipients { get; set; }
    /// <summary>Left out at send: suppressed or contacted recently.</summary>
    public int Skipped { get; set; }
    public int? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string GoalLabel => Rodeo.Goals.TryGetValue(Goal, out var g) ? g.Label : Goal;
}

/// <summary>An email customers get (Rodeo → Templates). {{first_name}}, {{account}}, {{balance}}, {{plan}} and {{survey:slug}} are filled in.</summary>
public class EmailTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public int? EmailCategoryId { get; set; }
    public EmailCategory? Category { get; set; }
    public int? EmailHeaderId { get; set; }
    public EmailPart? Header { get; set; }
    public int? EmailFooterId { get; set; }
    public EmailPart? Footer { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An email header or footer. Templates pick one of each; when they don't, the default is used.</summary>
public class EmailPart
{
    public int Id { get; set; }
    /// <summary>header or footer.</summary>
    public string Kind { get; set; } = "header";
    public string Name { get; set; } = "";
    public string Html { get; set; } = "";
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A group email templates are filed under (Rodeo → Emails → Categories), e.g. Billing.</summary>
public class EmailCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Color { get; set; } = "#00AEEF";
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An address no email goes to (Rodeo → Emails → Suppression List). Kept lowercase.</summary>
public class EmailSuppression
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    /// <summary>bounce, unsubscribe, complaint or manual (Rodeo.SuppressionReasons).</summary>
    public string Reason { get; set; } = "manual";
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? Note { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An email template sent as a test (Rodeo → Templates → Test), and who it went to.</summary>
public class EmailTestSend
{
    public int Id { get; set; }
    public int EmailTemplateId { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    /// <summary>emails, accounts, team or category.</summary>
    public string Mode { get; set; } = "emails";
    /// <summary>What was picked, e.g. "Marketing team".</summary>
    public string? Target { get; set; }
    public string? Subject { get; set; }
    public string? Note { get; set; }
    /// <summary>JSON: the options it was sent with, for Send Again.</summary>
    public string? Params { get; set; }
    /// <summary>JSON: [{email, name?, account?, status, error?}].</summary>
    public string Recipients { get; set; } = "[]";
    /// <summary>sent, logged, failed or partial.</summary>
    public string Status { get; set; } = "logged";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One click on a link in an email or campaign (Rodeo → heat maps).</summary>
public class LinkClick
{
    public int Id { get; set; }
    public int? ContactLogId { get; set; }
    public int? CampaignId { get; set; }
    public int? EmailTemplateId { get; set; }
    public int? CustomerId { get; set; }
    /// <summary>Which link: header-1, body-2, footer-3…</summary>
    public string Link { get; set; } = "";
    public string? Url { get; set; }
    public string? Device { get; set; }
    public DateTime ClickedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A letter or notice made into a PDF for an account (Rodeo → PDFs), like an email template is for email.</summary>
public class PdfTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    /// <summary>The big title on the first page.</summary>
    public string Title { get; set; } = "";
    /// <summary>Simple markup (see Rodeo.PdfMarkup).</summary>
    public string Body { get; set; } = "";
    /// <summary>Its own small print on every page; blank uses the footer picked, else the default.</summary>
    public string? Footer { get; set; }
    public bool ShowAddress { get; set; } = true;
    /// <summary>Active or Draft.</summary>
    public string Status { get; set; } = "Active";
    public int? PdfHeaderId { get; set; }
    public PdfPart? Header { get; set; }
    public int? PdfFooterId { get; set; }
    public PdfPart? FooterPart { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A PDF header (the band across the top of page 1) or footer (small print on every page).</summary>
public class PdfPart
{
    public int Id { get; set; }
    public string Kind { get; set; } = "header";
    public string Name { get; set; } = "";
    /// <summary>Header: the big text in the band (fill-ins work).</summary>
    public string? Title { get; set; }
    /// <summary>Header: the line under it (blank = the PDF's title).</summary>
    public string? Subtitle { get; set; }
    /// <summary>Header band: navy, cyan or white.</summary>
    public string Color { get; set; } = "navy";
    /// <summary>Footer: small print on every page.</summary>
    public string? Body { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One PDF made from a template for an account, with a copy of the wording it was made from.</summary>
public class PdfGeneration
{
    public int Id { get; set; }
    public int PdfTemplateId { get; set; }
    public PdfTemplate? Template { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string FileName { get; set; } = "";
    public int Pages { get; set; } = 1;
    public int Size { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? Footer { get; set; }
    public bool ShowAddress { get; set; } = true;
    /// <summary>JSON: the header as it was when made {title, subtitle, color}.</summary>
    public string? Header { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A PDF template sent by email as a test (the PDF attached), and whether it went.</summary>
public class PdfTestSend
{
    public int Id { get; set; }
    public int PdfTemplateId { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    /// <summary>Whose details filled it in.</summary>
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Recipients { get; set; } = "";
    /// <summary>sent or failed.</summary>
    public string Status { get; set; } = "sent";
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A customer survey (Rodeo → Surveys), answered on the website at /survey/{slug}.</summary>
public class Survey
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Intro { get; set; }
    public string? ThankYou { get; set; }
    /// <summary>active or closed.</summary>
    public string Status { get; set; } = "active";
    public DateOnly? OpensOn { get; set; }
    public DateOnly? ClosesOn { get; set; }
    public List<SurveyQuestion> Questions { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>open, scheduled, ended or closed.</summary>
    public string State
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return Status == "closed" ? "closed" : OpensOn > today ? "scheduled" : ClosesOn < today ? "ended" : "open";
        }
    }

    public string StateLabel => State switch
    {
        "open" => "Open", "scheduled" => $"Opens {OpensOn:MMM d}", "ended" => $"Ended {ClosesOn:MMM d}", _ => "Closed",
    };
}

public class SurveyQuestion
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public int? SurveyCategoryId { get; set; }
    public SurveyCategory? Category { get; set; }
    public int? SurveyAnswerSetId { get; set; }
    public int? SurveyBankQuestionId { get; set; }
    public string Question { get; set; } = "";
    /// <summary>rating (NPS 0–10), scale, single, multi, yes_no or text.</summary>
    public string Type { get; set; } = "text";
    /// <summary>JSON list of answers for scale, single and multi.</summary>
    public string? Options { get; set; }
    public bool Required { get; set; }
    public string? Help { get; set; }
    public int Position { get; set; }

    /// <summary>The answers a respondent can pick.</summary>
    public List<string> Choices() => Type switch
    {
        "rating" => Enumerable.Range(0, 11).Select(i => i.ToString()).ToList(),
        "yes_no" => ["Yes", "No"],
        "text" => [],
        _ => Options == null ? [] : System.Text.Json.JsonSerializer.Deserialize<List<string>>(Options)!,
    };
}

public class SurveyResponse
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public Survey? Survey { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    /// <summary>JSON {questionId: answer (string, or a list for Pick any)}.</summary>
    public string Answers { get; set; } = "{}";
    /// <summary>email, website or my-account.</summary>
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A reusable list of answers (Rodeo → Surveys → Answer Sets), e.g. a five-point satisfaction scale.</summary>
public class SurveyAnswerSet
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Group { get; set; } = "Other";
    /// <summary>scale (ordered, scored 1..n), single or multi.</summary>
    public string Type { get; set; } = "single";
    /// <summary>JSON list of answers.</summary>
    public string Options { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A ready-made question (Rodeo → Surveys → Question Bank) that can be added to any survey.</summary>
public class SurveyBankQuestion
{
    public int Id { get; set; }
    public int? SurveyCategoryId { get; set; }
    public SurveyCategory? Category { get; set; }
    public string Question { get; set; } = "";
    public string Type { get; set; } = "text";
    public int? SurveyAnswerSetId { get; set; }
    public SurveyAnswerSet? AnswerSet { get; set; }
    public string? Help { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A topic survey questions are filed under (Rodeo → Surveys → Categories).</summary>
public class SurveyCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Color { get; set; } = "#00AEEF";
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Rodeo's lists (ebunny's model constants) and the email fill-ins, shared with the website.</summary>
public static class Rodeo
{
    public const string Brand = "Energy Texas";
    public const string Phone = "1-800-555-0100";

    /// <summary>What a campaign is for: label, and what counts as a conversion.</summary>
    public static readonly Dictionary<string, (string Label, string? Converts)> Goals = new()
    {
        ["renewal"] = ("Renewal", "renewed or signed a new plan"), ["product"] = ("Product sign-up", "added the product"),
        ["winback"] = ("Win back", "came back with a new plan"), ["rewards"] = ("Rewards", "redeemed stars"), ["announcement"] = ("Announcement", null),
    };
    /// <summary>How many days after sending a conversion still counts.</summary>
    public const int ConversionWindow = 30;

    public static readonly Dictionary<string, string> SuppressionReasons = new() { ["bounce"] = "Hard bounce", ["unsubscribe"] = "Unsubscribed", ["complaint"] = "Spam complaint", ["manual"] = "Added by hand" };
    public static readonly Dictionary<string, string> PartKinds = new() { ["header"] = "Header", ["footer"] = "Footer" };
    public static readonly string[] PdfCategories = ["Billing", "Collections", "Service", "Welcome", "Rewards", "Legal"];
    public static readonly Dictionary<string, string> PdfColors = new() { ["navy"] = "Navy band, white text", ["cyan"] = "Cyan band, white text", ["white"] = "White, navy text with a cyan line" };
    public static readonly Dictionary<string, string> QuestionTypes = new() { ["rating"] = "NPS 0–10", ["scale"] = "Scale", ["single"] = "Pick one", ["multi"] = "Pick any", ["yes_no"] = "Yes / No", ["text"] = "Written answer" };
    /// <summary>Types whose answers come from a list (an answer set or the question's own options).</summary>
    public static readonly string[] Listed = ["scale", "single", "multi"];
    public static readonly string[] AnswerSetGroups = ["Satisfaction", "Agreement", "Likelihood", "Frequency", "Ease", "Yes / No", "Channels", "Other"];
    public static readonly Dictionary<string, string> AnswerSetTypes = new() { ["scale"] = "Scale (ordered, scored)", ["single"] = "Pick one", ["multi"] = "Pick any" };
    public static readonly Dictionary<string, string> ResponseSources = new() { ["email"] = "Email link", ["website"] = "Website", ["my-account"] = "My Account" };

    // ---- email fill-ins -----------------------------------------------------------------------------------------

    /// <summary>{{first_name}}, {{account}}, {{balance}}, {{plan}} and {{survey:slug}} (a signed link to the website survey for this account).</summary>
    public static string FillIns(string text, Customer c, string? planName, string websiteUrl) =>
        Regex.Replace(text, @"\{\{\s*(first_name|account|balance|plan|survey:([a-z0-9-]+))\s*\}\}", m => m.Groups[1].Value switch
        {
            "first_name" => WebUtility.HtmlEncode(c.FirstName ?? ""),
            "account" => WebUtility.HtmlEncode(c.Account),
            "balance" => c.Balance.ToString("$#,##0.00"),
            "plan" => WebUtility.HtmlEncode(planName ?? ""),
            _ => SurveyLink(websiteUrl, m.Groups[2].Value, c.Account),
        });

    /// <summary>The whole email as the customer gets it: header, body, footer, with the fill-ins.</summary>
    public static string Render(EmailTemplate t, EmailPart? header, EmailPart? footer, Customer c, string? planName, string websiteUrl) =>
        FillIns((header?.Html ?? "") + t.Body + (footer?.Html ?? ""), c, planName, websiteUrl);

    // A survey link is signed, so a response is only tied to the account the email was sent to
    private const string SurveyKey = "rc-bunny-survey-links";

    public static string Sign(string slug, string account) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(SurveyKey), Encoding.UTF8.GetBytes($"{slug}|{account}")))[..32].ToLowerInvariant();

    public static string SurveyLink(string websiteUrl, string slug, string account) =>
        $"{websiteUrl.TrimEnd('/')}/survey/{slug}?c={Uri.EscapeDataString(account)}&signature={Sign(slug, account)}";

    /// <summary>The links in HTML, in order: (key "section-n", label, url).</summary>
    public static List<(string Key, string Label, string Url)> Links(string section, string html)
    {
        var list = new List<(string, string, string)>();
        var i = 0;
        foreach (Match m in Regex.Matches(html, "<a\\s[^>]*href=\"([^\"]*)\"[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            i++;
            var text = Regex.Replace(Regex.Replace(m.Groups[2].Value, "<[^>]+>", ""), @"\s+", " ").Trim();
            list.Add(($"{section}-{i}", $"{char.ToUpper(section[0])}{section[1..]}: {(text != "" ? WebUtility.HtmlDecode(text) : $"Link {i}")}", m.Groups[1].Value));
        }
        return list;
    }
}
