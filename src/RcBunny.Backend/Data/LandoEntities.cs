namespace RcBunny.Data;

// ---- Lando: the website's pages, content blocks and traffic (ebunny's sites, templates, pages, content_blocks,
//      page_components, site_visits, site_blocks and cookie_consents tables) ----------------------------------------

/// <summary>A website the pages belong to (one for now: Energy Texas).</summary>
public class Website
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>The host name it answers on, e.g. www.energytexas.com.</summary>
    public string Domain { get; set; } = "";
    /// <summary>Overrides the brand phone on this site's pages.</summary>
    public string? Phone { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A page layout (default, article, location…).</summary>
public class Template
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>A layout file, e.g. shared/layouts/article.html (optional).</summary>
    public string? File { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class BlockCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>An HTML widget. With {{variables}} it's a module: each page passes its own text.</summary>
public class ContentBlock
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    /// <summary>Null = every site.</summary>
    public int? SiteId { get; set; }
    public int? CategoryId { get; set; }
    public BlockCategory? Category { get; set; }
    public string Html { get; set; } = "";
    public int? UpdatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A website page: a built-in page (File) or one built from content blocks on a template.</summary>
public class Page
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    /// <summary>"/" for home, else e.g. "locations/houston".</summary>
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public int? TemplateId { get; set; }
    public string? Redirect { get; set; }
    /// <summary>Published or Draft.</summary>
    public string Status { get; set; } = "Draft";
    /// <summary>Built-in pages: the page's own file (index.html, plans.html…).</summary>
    public string? File { get; set; }
    public string? PageTitle { get; set; }
    public string? HtmlTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? HeadContent { get; set; }
    public string? AddressBoxTitle { get; set; }
    public string? Phone { get; set; }
    public bool Canonical { get; set; } = true;
    public bool NoIndex { get; set; }
    public bool HardCache { get; set; }
    public string? PromoCode { get; set; }
    public string? MarketLabel { get; set; }
    public int? RepId { get; set; }
    public int? CopyFromId { get; set; }
    // The content block chosen for each content area (Lando → Content Blocks)
    public int? ContentPrimaryId { get; set; }
    public int? ContentSecondaryId { get; set; }
    public int? ContentParentId { get; set; }
    public int? ContentAuxiliaryId { get; set; }
    public int? ContentAmpId { get; set; }
    // Price grid: a plan group priced in the default market
    public string? PricegridType { get; set; }
    public string? PricegridHeader { get; set; }
    public int? PricegridGroupId { get; set; }
    public string? RatingFormula { get; set; }
    public int? MarketId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A content block placed in one of a page's zones (top, main, sidebar, bottom), with the page's text for its variables.</summary>
public class PageComponent
{
    public int Id { get; set; }
    public int PageId { get; set; }
    public Page? Page { get; set; }
    public int ContentBlockId { get; set; }
    public ContentBlock? Block { get; set; }
    public string Zone { get; set; } = "main";
    public int Position { get; set; }
    /// <summary>JSON: {variable: text}.</summary>
    public string? Vars { get; set; }
}

/// <summary>One website page view. seen_at moves on while the page stays open (the page checks in every minute).</summary>
public class SiteVisit
{
    public int Id { get; set; }
    /// <summary>A random id in the et_vid cookie, one per browser.</summary>
    public string Visitor { get; set; } = "";
    public string Ip { get; set; } = "";
    /// <summary>"/" or "plans", "myaccount/dashboard".</summary>
    public string Path { get; set; } = "";
    public int? PageId { get; set; }
    /// <summary>Signed in to My Account.</summary>
    public int? CustomerId { get; set; }
    public string? Referrer { get; set; }
    public string? Agent { get; set; }
    public bool Blocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime SeenAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A blocked IP address (or range ending in *) or area of the website (a path and everything under it).</summary>
public class SiteBlock
{
    public int Id { get; set; }
    /// <summary>ip or area.</summary>
    public string Type { get; set; } = "ip";
    public string Value { get; set; } = "";
    public string? Reason { get; set; }
    /// <summary>Shown to the visitor.</summary>
    public string? Message { get; set; }
    public bool Active { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public int Hits { get; set; }
    public DateTime? LastHitAt { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool InForce => Active && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
    public bool MatchesIp(string ip) => Value.EndsWith('*') ? ip.StartsWith(Value[..^1]) : ip == Value;
    /// <summary>An area covers itself and everything under it.</summary>
    public bool MatchesPath(string path) => path == Value || path.StartsWith(Value + "/");
}

/// <summary>One choice made in the website's cookie banner. The latest for a person (or browser) is the one in force.</summary>
public class CookieConsent
{
    public int Id { get; set; }
    public string Visitor { get; set; } = "";
    public int? CustomerId { get; set; }
    public int? UserId { get; set; }
    /// <summary>all, necessary or custom.</summary>
    public string Choice { get; set; } = "necessary";
    public bool Analytics { get; set; }
    public bool Marketing { get; set; }
    public string PolicyVersion { get; set; } = PolicyNow;
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? Page { get; set; }
    /// <summary>JSON: the cookies the browser sent ({name: value}, sign-in tokens cut short).</summary>
    public string? CookieJar { get; set; }
    public DateTime? CookiesSeenAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Bump when the cookie policy changes: everyone is asked again.</summary>
    public const string PolicyNow = "2026-10";

    /// <summary>The website's cookies: name → (category, purpose, lasts). Necessary ones are always on.</summary>
    public static readonly (string Name, string Category, string Purpose, string Lasts)[] Cookies =
    [
        ("site_session", "necessary", "Keeps you signed in and your cart between pages", "Session"),
        ("XSRF-TOKEN", "necessary", "Protects forms from cross-site requests", "Session"),
        ("cookie_consent", "necessary", "Remembers this cookie choice", "1 year"),
        ("et_vid", "necessary", "Tells visits from the same browser apart for site traffic (no personal details)", "1 year"),
        ("_ga", "analytics", "Google Analytics: tells visits apart", "2 years"),
        ("_ga_ID", "analytics", "Google Analytics: keeps the visit going", "2 years"),
        ("_clck", "analytics", "Clarity: heat maps of clicks on pages", "1 year"),
        ("_fbp", "marketing", "Meta: measures ads that brought you here", "3 months"),
        ("_gcl_au", "marketing", "Google Ads: measures ad conversions", "3 months"),
        ("li_fat_id", "marketing", "LinkedIn: measures business ads", "1 month"),
    ];

    /// <summary>The cookies this choice allows.</summary>
    public string[] Allowed() => Cookies.Where(c => c.Category == "necessary" || (c.Category == "analytics" && Analytics) || (c.Category == "marketing" && Marketing)).Select(c => c.Name).ToArray();

    public string Label => Choice switch { "all" => "Accepted all", "necessary" => "Necessary only", "custom" => "Chose", _ => Choice };
}

/// <summary>Lando's lists (ebunny's config/cms.php and Page::AREAS).</summary>
public static class Cms
{
    public static readonly Dictionary<string, string> Zones = new()
    {
        ["top"] = "Top of page (under the banner)", ["main"] = "Main content (after the page content)",
        ["sidebar"] = "Sidebar (beside the page content)", ["bottom"] = "Bottom of page (above the footer)",
    };
    /// <summary>Built-in pages: path → the website's own page for it.</summary>
    public static readonly Dictionary<string, string> Files = new()
    {
        ["/"] = "index.html", ["plans"] = "plans.html", ["byop"] = "build-your-own-plan.html", ["business"] = "business.html", ["contact-us"] = "contact-us.html",
    };
    public static readonly Dictionary<string, string> Areas = new()
    {
        ["primary"] = "Primary Content", ["secondary"] = "Secondary Content", ["parent"] = "Parent Content", ["auxiliary"] = "Auxiliary Content", ["amp"] = "AMP Content",
    };
    public static readonly Dictionary<string, string> PriceGrids = new() { ["cards"] = "Plan cards", ["table"] = "Price table (500 / 1,000 / 2,000 kWh)" };
    public static readonly Dictionary<string, string> RatingFormulas = new()
    {
        ["avg_1000"] = "Average price at 1,000 kWh", ["avg_500"] = "Average price at 500 kWh", ["avg_2000"] = "Average price at 2,000 kWh", ["energy"] = "Energy charge only",
    };
    public static readonly Dictionary<int, string> Reps = new() { [1] = "Energy Texas" };
    /// <summary>Paths a website page can never use.</summary>
    public static readonly string[] Reserved = ["admin", "site", "shared", "frontend", "backend", "contact", "up", "storage", "amp", "survey", "checkout", "myaccount", "shop"];
    /// <summary>Minutes a visitor counts as on the site after the page last checked in.</summary>
    public const int OnlineMinutes = 5;
}
