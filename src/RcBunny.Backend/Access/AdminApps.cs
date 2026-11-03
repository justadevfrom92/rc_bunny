namespace RcBunny.Backend.Access;

/// <summary>A sidebar link (a client route).</summary>
public record MenuItem(string Label, string Path, bool Ported = true);

public record MenuGroup(string? Heading, IReadOnlyList<MenuItem> Items);

public record AppDef(string Key, string Name, string Description, string Icon, string Home, IReadOnlyList<MenuGroup> Menu, bool Custom = false);

/// <summary>
/// Every built-in admin app and its menu (ebunny's config/admin.php). The launcher, sidebar, app switcher, Roles
/// matrix and permission checks all read from here: an app key is also the permission that opens it.
/// Links to /{app}/r/{resource} are the generic screens (Resources/ResourceRegistry*.cs); the rest are pages in ClientApp.
/// Apps made with "+ New Admin App" (admin_apps table) are added at runtime.
/// </summary>
public static class AdminApps
{
    private static MenuGroup G(string? heading, params MenuItem[] items) => new(heading, items);
    private static MenuItem P(string label, string path) => new(label, path);
    /// <summary>A generic resource screen in this app.</summary>
    private static MenuItem R(string label, string app, string resource, string? suffix = null) => new(label, $"/{app}/r/{resource}{suffix}");
    private static MenuItem[] Data(string app, params (string Key, string Name)[] tables) => tables.Select(t => new MenuItem(t.Name, $"/{app}/data/{t.Key}")).ToArray();

    public static readonly IReadOnlyList<AppDef> Builtin =
    [
        new("corral", "Corral", "Customer service and CRM: search accounts, create orders, work exception queues, the sales pipeline of leads, follow-ups and partners.", "users", "/corral/customers",
        [
            G(null, P("Customer Search", "/corral/customers"), P("ESIID Lookup", "/corral/esiid"), P("Create Order", "/corral/orders/new"), P("Create Order - Biz", "/corral/orders/new?type=biz"),
                R("SMS", "corral", "sms"), R("Phone Calls", "corral", "phonecalls"), R("All Accounts", "corral", "customers")),
            G("CRM", P("Pipeline", "/corral/crm"), R("Leads", "corral", "crm-leads"), R("Follow-ups", "corral", "crm-activities"), R("Partners & Brokers", "corral", "crm-partners")),
            G("Products", R("Products", "corral", "shop-products"), R("New Product", "corral", "shop-products", "/new"), R("Product Orders", "corral", "shop-orders")),
            G(null, R("ERCOT", "corral", "ercot-transactions"), R("History", "corral", "history-items")),
            G("Exceptions", P("Queues", "/corral/queues"), P("Exception Accounts", "/corral/customers?exception=*"), R("Work Items", "corral", "work-items")),
        ]),
        new("ranger", "Ranger", "Support tickets from My Account and the website contact form: the queue, replies and notes, take over and join in for managers, the ticket team (in-office and 3rd party) and approval ratings.", "ticket", "/ranger",
        [
            G("Tickets", P("Dashboard", "/ranger"), P("Tickets", "/ranger/tickets")),
            G("Website", R("Web Messages", "ranger", "contact-messages")),
            G("Team", R("Ticket Team", "ranger", "ranger-agents"), R("Add a Team Member", "ranger", "ranger-agents", "/new"), P("Approval Ratings", "/ranger/satisfaction"), P("Employees", "/ranger/employees")),
        ]),
        new("lando", "Lando", "Website CMS: site health and visitors, pages, page templates and content blocks.", "layout", "/lando",
        [
            G("Site", P("Dashboard", "/lando"), P("Site Health", "/lando/health"), R("Visitors", "lando", "site-visits"), R("Blocked Areas", "lando", "blocked-areas"), R("Blocked IPs", "lando", "blocked-ips"), R("Cookie Consents", "lando", "cookie-consents")),
            G("Pages", R("List Pages", "lando", "pages"), R("Add a Page", "lando", "pages", "/new"), R("Page Templates", "lando", "templates"), P("Update Sitemap", "/lando/sitemap")),
            G("Content", R("Content Blocks", "lando", "content-blocks"), R("Add a Content Block", "lando", "content-blocks", "/new"), R("Block Categories", "lando", "block-categories")),
            G("Design", P("Style Guide", "/lando/styles")),
            G("Data", Data("lando", ("ips", "IPs"))),
        ]),
        new("astro", "Astro", "Plans and pricing: plans, plan groups, rates, TDSP fees, term discounts, ETFs and Build Your Own Plan products.", "chart", "/astro/plans",
        [
            G("Plans", P("View Plans", "/astro/plans"), R("Edit Plans", "astro", "plans"), R("Add a Plan", "astro", "plans", "/new"), R("Plan Groups", "astro", "plan-groups"), R("Add a Group", "astro", "plan-groups", "/new")),
            G("Rates", P("View Rates", "/astro/rates"), P("Update Rates", "/astro/rates/edit"), R("Rate Uploads", "astro", "rate-imports"), R("Rate History", "astro", "rates"), R("TDSP Fees", "astro", "tdsp-fees")),
            G("Modifiers", P("Term", "/astro/terms"), R("ETF", "astro", "term-etfs"), R("Products", "astro", "byop-products")),
            G("Build Your Own", R("Build Your Own", "astro", "byop-products"), R("New Product", "astro", "byop-products", "/new")),
        ]),
        new("walker", "Walker", "Files & reporting: ETL feeds, legal documents, customer templates, and reports with their status.", "file", "/walker",
        [
            G("Reports", P("Report Status", "/walker"), P("Run a Report", "/walker/reports"), R("Saved Reports", "walker", "saved-reports"), P("Orders", "/walker/reports/orders"), P("Notes", "/walker/reports/notes"), P("Phonecalls", "/walker/reports/phonecalls"), R("All Runs", "walker", "report-runs")),
            G("Files", R("All Files", "walker", "report-uploads"), P("Upload a File", "/walker/upload"), P("ETL Feeds", "/walker/etl"),
                R("ETL Files", "walker", "report-uploads", "?f.category=etl"), R("Legal Documents", "walker", "report-uploads", "?f.category=legal"), R("Customer Templates", "walker", "report-uploads", "?f.category=templates"),
                R("Finance", "walker", "report-uploads", "?f.category=finance"), R("Utility / ERCOT", "walker", "report-uploads", "?f.category=ercot"), R("Vendor", "walker", "report-uploads", "?f.category=vendor")),
        ]),
        new("caboose", "Caboose", "Finance: payments, credits and debits approval, refunds, deposits, receivables aging, the journal export, MSIDs and promo codes.", "train", "/caboose",
        [
            G("Finance", P("Dashboard", "/caboose"), R("Payments", "caboose", "caboose-payments"), R("Credits & Debits", "caboose", "pending-ledger", "?f.status=pending"), R("Refunds", "caboose", "refunds"), P("Deposits", "/caboose/deposits")),
            G("Accounting", P("Receivables Aging", "/caboose/aging"), P("Journal", "/caboose/journal")),
            G("Tracking", R("MSIDs", "caboose", "marketing-channels"), R("Promo Codes", "caboose", "promo-codes")),
            G("Data", Data("caboose", ("deposit-thresholds", "Deposit Thresholds"), ("max-deposits", "Max Deposits"), ("tax-rates", "Tax Rates"), ("interest-rates", "Interest Rates"),
                ("bill-credits", "Bill Credits & Promos"), ("charge-codes", "Charge Codes"), ("rate-exports", "Rate Exports"), ("blackout-days", "Blackout Days"), ("fraud-indicators", "Fraud Indicators"), ("note-dispositions", "CIS Note Dispositions"))),
        ]),
        new("bounty", "Bounty", "Rewards: offers, how stars are earned, redemptions to fulfil, member balances and the monthly drawing.", "gift", "/bounty",
        [
            G("Rewards", P("Dashboard", "/bounty"), R("Offers", "bounty", "reward-offers"), R("Earning Rules", "bounty", "reward-rules")),
            G("Members", R("Redemptions", "bounty", "redemptions", "?f.fulfillment=pending"), R("Members", "bounty", "members"), P("Monthly Drawing", "/bounty/drawing"), new MenuItem("Drawing Winners", "/bounty/data/monthly-drawing")),
        ]),
        new("rodeo", "Rodeo", "Marketing: email and SMS campaigns, email templates, PDF templates (letters and notices made into PDFs), and customer surveys.", "megaphone", "/rodeo",
        [
            G("Marketing", P("Dashboard", "/rodeo"), R("Campaigns", "rodeo", "campaigns"), P("New Campaign", "/rodeo/campaigns/new")),
            G("Emails", P("Overview", "/rodeo/emails"), R("Sent Emails", "rodeo", "sent-emails"), R("Templates", "rodeo", "email-templates"), R("New Template", "rodeo", "email-templates", "/new"),
                R("Headers & Footers", "rodeo", "email-parts"), R("Categories", "rodeo", "email-categories"), R("Suppression List", "rodeo", "email-suppressions"), R("Test Sends", "rodeo", "email-test-sends")),
            G("PDFs", R("PDF Templates", "rodeo", "pdf-templates"), R("New PDF Template", "rodeo", "pdf-templates", "/new"), R("PDF Headers & Footers", "rodeo", "pdf-parts"), R("PDFs Made", "rodeo", "pdf-generations")),
            G("Surveys", R("All Surveys", "rodeo", "surveys"), P("Survey Emails", "/rodeo/survey-emails"), R("New Survey", "rodeo", "surveys", "/new"), R("Responses", "rodeo", "survey-responses"),
                R("Question Bank", "rodeo", "survey-bank"), R("Answer Sets", "rodeo", "survey-answer-sets"), R("Categories", "rodeo", "survey-categories")),
        ]),
        new("deputy", "Deputy", "AI agents on open-weight models (Qwen, Kimi, DeepSeek, Llama…): texts and the My Account chat with take-over by a person, models and testing.", "bolt", "/deputy",
        [
            G("AI Agents", P("Dashboard", "/deputy"), R("Conversations", "deputy", "ai-conversations"), R("Waiting for a Person", "deputy", "ai-conversations", "?f.status=waiting")),
            G("Setup", R("Agents", "deputy", "ai-agents"), R("New Agent", "deputy", "ai-agents", "/new"), R("Models", "deputy", "ai-models"), R("Add a Model", "deputy", "ai-models", "/new"), R("Automation Runs", "deputy", "ai-automation-runs")),
        ]),
        new("sheriff", "Sheriff", "Settings: the staff directory (profiles, permissions, log in as), roles, markets, API integrations, scheduled jobs, cron automations and the timeline of everything that ran.", "star", "/sheriff/users",
        [
            G("Heartbeat", P("Overview", "/sheriff/heartbeat"), R("Checks", "sheriff", "heartbeats"), R("Control Log", "sheriff", "heartbeat-actions")),
            G("Access", P("Staff Directory", "/sheriff/users"), P("Add a Person", "/sheriff/users/new"), P("Roles", "/sheriff/roles"), R("Custom Admin Apps", "sheriff", "admin-apps")),
            G("Config", P("Timeline", "/sheriff/timeline"), P("Crons", "/sheriff/jobs"), R("Cron Automations", "sheriff", "automations"), R("Job Runs", "sheriff", "job-runs"),
                P("Update Sitemap", "/sheriff/sitemap"), R("Cached Tables", "sheriff", "table-snapshots"), P("Caches", "/sheriff/cache"), R("Audit Log", "sheriff", "audit-entries"), R("History", "sheriff", "all-history"), P("Reference Data", "/sheriff/data")),
            G("Markets", R("View Markets", "sheriff", "markets"), R("Add a Market", "sheriff", "markets", "/new")),
            G("APIs", P("Integrations", "/sheriff/apis"), R("Connection Tests", "sheriff", "integration-tests"), R("API Calls", "sheriff", "api-calls")),
        ]),
    ];

    /// <summary>Your own pages (person menu), open to everyone signed in.</summary>
    public static readonly AppDef Account = new("account", "Account", "Your notifications, files and settings.", "user", "/account/notifications",
        [G("Account", P("Notifications", "/account/notifications"), P("My Files", "/account/files"), P("Settings", "/account/settings"))]);

    /// <summary>Kept for existing callers: the built-in apps.</summary>
    public static IReadOnlyList<AppDef> All => Builtin;

    /// <summary>Rights a role or person can hold besides app access.</summary>
    public static readonly IReadOnlyDictionary<string, string> Rights = new Dictionary<string, string>
    {
        ["refunds"] = "Refunds & reversals",
        ["delete"] = "Delete records",
    };

    public static readonly IReadOnlyList<string> Departments = ["Customer Service", "Pricing", "Marketing", "Finance", "AI Operations", "IT", "Management"];

    /// <summary>Custom app keys (admin_apps); refreshed by AccessService.</summary>
    public static volatile string[] CustomKeys = [];

    /// <summary>Every valid permission: built-in apps, custom apps, then rights.</summary>
    public static IEnumerable<string> AllPerms => Builtin.Select(a => a.Key).Concat(CustomKeys).Concat(Rights.Keys);

    public static bool IsPerm(string perm) => AllPerms.Contains(perm);

    public static string Label(string perm) =>
        Builtin.FirstOrDefault(a => a.Key == perm)?.Name ?? (Rights.TryGetValue(perm, out var r) ? r : perm);
}
