namespace RcBunny.Backend.Access;

/// <summary>A sidebar link. Path is the client route; Ported=false shows a "not ported yet" page.</summary>
public record MenuItem(string Label, string Path, bool Ported = false);

public record MenuGroup(string? Heading, IReadOnlyList<MenuItem> Items);

public record AdminApp(string Key, string Name, string Description, string Icon, string Home, IReadOnlyList<MenuGroup> Menu);

/// <summary>
/// Every admin app (ebunny's config/admin.php). The launcher, sidebar, app switcher, the Roles matrix
/// and every permission check read from here: an app key is also the permission that opens it.
/// </summary>
public static class AdminApps
{
    private static MenuGroup G(string? heading, params MenuItem[] items) => new(heading, items);
    private static MenuItem P(string label, string path) => new(label, path, true);
    private static MenuItem T(string label, string path) => new(label, path);

    public static readonly IReadOnlyList<AdminApp> All =
    [
        new("corral", "Corral", "Customer service and CRM: search accounts, create orders, work exception queues, the sales pipeline of leads, follow-ups and partners.", "users", "/corral/customers",
        [
            G(null, P("Customer Search", "/corral/customers"), P("ESIID Lookup", "/corral/esiid"), T("Create Order", "/corral/orders/create"), T("Create Order - Biz", "/corral/orders/create-biz"), T("SMS", "/corral/sms"), T("Phone Calls", "/corral/calls")),
            G("CRM", T("Pipeline", "/corral/crm/pipeline"), T("Leads", "/corral/crm/leads"), T("Follow-ups", "/corral/crm/tasks"), T("Partners & Brokers", "/corral/crm/partners")),
            G("Products", T("Products", "/corral/shop"), T("New Product", "/corral/shop/create"), T("Product Orders", "/corral/shop/orders")),
            G("Exceptions", P("Exception Accounts", "/corral/customers?exception=*")),
        ]),
        new("ranger", "Ranger", "Support tickets from My Account and the website contact form: the queue, replies and notes, the ticket team (in-office and 3rd party) and approval ratings.", "ticket", "/ranger",
        [
            G("Tickets", T("Dashboard", "/ranger"), T("Tickets", "/ranger/tickets")),
            G("Website", P("Web Messages", "/ranger/messages")),
            G("Team", T("Ticket Team", "/ranger/team"), T("Approval Ratings", "/ranger/satisfaction")),
        ]),
        new("lando", "Lando", "Website CMS: site health and visitors, pages, page templates and content blocks.", "layout", "/lando",
        [
            G("Site", T("Dashboard", "/lando"), T("Site Health", "/lando/health"), T("Visitors", "/lando/visitors")),
            G("Pages", T("List Pages", "/lando/pages"), T("Page Templates", "/lando/templates")),
            G("Content", T("Content Blocks", "/lando/blocks")),
            G("Design", T("Style Guide", "/lando/styles")),
        ]),
        new("astro", "Astro", "Plans and pricing: plans, plan groups, rates, TDSP fees, term discounts, ETFs and Build Your Own Plan products.", "chart", "/astro/plans",
        [
            G("Plans", P("View Plans", "/astro/plans"), T("Plan Groups", "/astro/groups")),
            G("Rates", T("View Rates", "/astro/rates"), T("TDSP Fees", "/astro/fees")),
            G("Modifiers", T("Term", "/astro/terms"), T("ETF", "/astro/etfs")),
            G("Build Your Own", T("Build Your Own", "/astro/products")),
        ]),
        new("walker", "Walker", "Files & reporting: ETL feeds, legal documents, customer templates, and reports with their status.", "file", "/walker",
        [
            G("Reports", T("Report Status", "/walker"), T("Run a Report", "/walker/reports"), T("Saved Reports", "/walker/saved")),
            G("Files", T("All Files", "/walker/uploads"), T("ETL Feeds", "/walker/etl")),
        ]),
        new("caboose", "Caboose", "Finance: payments, credits and debits approval, refunds, deposits, receivables aging, the journal export, MSIDs and promo codes.", "train", "/caboose",
        [
            G("Finance", T("Dashboard", "/caboose"), T("Payments", "/caboose/payments"), T("Credits & Debits", "/caboose/ledger"), T("Refunds", "/caboose/refunds"), T("Deposits", "/caboose/deposits")),
            G("Accounting", T("Receivables Aging", "/caboose/aging"), T("Journal", "/caboose/journal")),
        ]),
        new("bounty", "Bounty", "Rewards: offers, how stars are earned, redemptions to fulfil, member balances and the monthly drawing.", "gift", "/bounty",
        [
            G("Rewards", T("Dashboard", "/bounty"), T("Offers", "/bounty/offers"), T("Earning Rules", "/bounty/rules")),
            G("Members", T("Redemptions", "/bounty/redemptions"), T("Members", "/bounty/members"), T("Monthly Drawing", "/bounty/drawing")),
        ]),
        new("rodeo", "Rodeo", "Marketing: email and SMS campaigns, email templates, PDF templates and customer surveys.", "megaphone", "/rodeo",
        [
            G("Marketing", T("Dashboard", "/rodeo"), T("Campaigns", "/rodeo/campaigns")),
            G("Emails", T("Templates", "/rodeo/templates"), T("Sent Emails", "/rodeo/emails/sent")),
            G("Surveys", T("All Surveys", "/rodeo/surveys"), T("Responses", "/rodeo/surveys/responses")),
        ]),
        new("deputy", "Deputy", "AI agents on open-weight models: texts and the My Account chat with take-over by a person, models and testing.", "bolt", "/deputy",
        [
            G("AI Agents", T("Dashboard", "/deputy"), T("Conversations", "/deputy/conversations")),
            G("Setup", T("Agents", "/deputy/agents"), T("Models", "/deputy/models")),
        ]),
        new("sheriff", "Sheriff", "Settings: the staff directory (profiles, permissions, log in as), roles, the timeline of everything that ran, and caches.", "star", "/sheriff/users",
        [
            G("Access", P("Staff Directory", "/sheriff/users"), P("Add a Person", "/sheriff/users/new"), P("Roles", "/sheriff/roles")),
            G("Config", P("Timeline", "/sheriff/timeline"), P("Caches", "/sheriff/cache"), T("Crons", "/sheriff/jobs"), T("APIs", "/sheriff/apis")),
        ]),
    ];

    /// <summary>Rights a role or person can hold besides app access.</summary>
    public static readonly IReadOnlyDictionary<string, string> Rights = new Dictionary<string, string>
    {
        ["refunds"] = "Refunds & reversals",
    };

    public static readonly IReadOnlyList<string> Departments =
        ["Customer Service", "Pricing", "Marketing", "Finance", "AI Operations", "IT", "Management"];

    /// <summary>Every valid permission: app keys then rights.</summary>
    public static IEnumerable<string> AllPerms => All.Select(a => a.Key).Concat(Rights.Keys);

    public static bool IsPerm(string perm) => AllPerms.Contains(perm);

    public static string Label(string perm) =>
        All.FirstOrDefault(a => a.Key == perm)?.Name ?? (Rights.TryGetValue(perm, out var r) ? r : perm);
}
