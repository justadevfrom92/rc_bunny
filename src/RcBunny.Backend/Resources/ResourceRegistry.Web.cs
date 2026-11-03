using Microsoft.EntityFrameworkCore;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Resources;

public static partial class ResourceRegistry
{
    private static IEnumerable<ResourceDef> Ranger()
    {
        const string app = "ranger";
        yield return new ResourceDef
        {
            Key = "support-tickets", App = app, Entity = typeof(SupportTicket), Title = "Tickets", Singular = "Ticket",
            List = ["Subject", "Name", "Status", "Priority", "Channel", "AssignedTo", "LastActivityAt"], Search = ["Subject", "Name", "Email"],
            Filters = ["Status", "Priority", "Topic", "Channel", "Rating"], Sort = "-LastActivityAt", Create = false,
            Options = new() { ["Status"] = ["new", "open", "waiting", "solved", "closed"], ["Priority"] = ["urgent", "high", "normal", "low"], ["Topic"] = ["billing", "service", "plan", "rewards", "account", "outage", "other"] },
            ReadOnly = ["FirstResponseAt", "SolvedAt", "Rating", "RatingComment", "RatedAt", "LastActivityAt", "ManagerRequestedAt", "ManagerRequestedBy", "ManagerRequestNote", "Name", "Email", "Channel"],
            Children = [("Messages", "support-ticket-messages", "SupportTicketId"), ("Internal Notes", "notes", "SupportTicketId"), ("Files", "support-ticket-attachments", "SupportTicketId"), ("Joined In", "support-ticket-watchers", "SupportTicketId")],
        };
        yield return Child<SupportTicketMessage>("support-ticket-messages", app, "Ticket Messages", ["CreatedAt", "Author", "Body", "Via", "UserId", "SupportTicketId"], ["Body"], ["Author", "Via"], "CreatedAt", create: false, edit: false);
        yield return Child<SupportTicketAttachment>("support-ticket-attachments", app, "Ticket Files", ["Name", "Mime", "Size", "FromCustomer", "CreatedAt", "SupportTicketId"], ["Name"], ["FromCustomer"], "-CreatedAt", create: false, edit: false);
        yield return Child<SupportTicketWatcher>("support-ticket-watchers", app, "Joined In", ["UserId", "JoinedAt", "SupportTicketId"], [], [], "-JoinedAt", create: false, edit: false);
        yield return new ResourceDef
        {
            Key = "ranger-agents", App = app, Entity = typeof(RangerAgent), Title = "Ticket Team", Singular = "Team Member",
            List = ["UserId", "Team", "Level", "Active", "CreatedAt"], Filters = ["Team", "Level", "Active"], Sort = "-Active",
            Options = new() { ["Team"] = ["office", "third_party"], ["Level"] = ["agent", "manager", "company_admin"] }, ReadOnly = ["AddedBy"],
            BeforeSave = async c =>
            {
                var a = Row<RangerAgent>(c);
                a.AddedBy ??= c.Me.UserId;
                if (await c.Db.RangerAgents.AnyAsync(x => x.UserId == a.UserId && x.Id != a.Id)) throw new ActionError("That person is already on the team.");
                // 3rd-party company admins only manage their own company's people
                if (c.Me.IsExternal && (await c.Db.Users.FirstAsync(u => u.Id == a.UserId)).Company != c.Me.Company) throw new ActionError("You can only add people from your own company.");
            },
        };
        yield return new ResourceDef
        {
            Key = "contact-messages", App = app, Entity = typeof(ContactMessage), Title = "Web Messages", Singular = "Message", Create = false,
            Description = "The website's Contact Us form.", List = ["CreatedAt", "FirstName", "LastName", "Email", "Topic", "HandledAt"], Search = ["FirstName", "LastName", "Email", "Message"],
            Filters = ["Topic"], Sort = "-CreatedAt", ReadOnly = ["FirstName", "LastName", "Email", "Phone", "Topic", "Message", "Ip", "HandledAt", "HandledBy", "RepliedAt", "RepliedBy", "Reply"],
            Actions =
            [
                new() { Key = "reply", Label = "Reply", Inputs = new() { ["note:reply"] = "Your reply (emailed to them)" }, Run = c =>
                {
                    var m = Row<ContactMessage>(c); m.Reply = In(c, "reply"); m.RepliedAt = Now; m.RepliedBy = c.Me.UserId; m.HandledAt ??= Now; m.HandledBy ??= c.Me.UserId;
                    return Task.FromResult($"Reply sent to {m.Email} (logged until mail is set up).");
                } },
                new() { Key = "handled", Label = "Mark Handled", When = r => ((ContactMessage)r).HandledAt == null, Run = c => { var m = Row<ContactMessage>(c); m.HandledAt = Now; m.HandledBy = c.Me.UserId; return Task.FromResult("Marked handled."); } },
            ],
        };
    }

    private static IEnumerable<ResourceDef> Lando()
    {
        const string app = "lando";
        var zones = Keys("cms.zones");
        yield return new ResourceDef
        {
            Key = "pages", App = app, Entity = typeof(Page), Title = "Pages", Singular = "Page",
            List = ["Path", "Title", "Status", "TemplateId", "UpdatedAt"], Search = ["Path", "Title", "HtmlTitle"], Filters = ["Status", "TemplateId"], Sort = "Path", Delete = true, WritePerm = null,
            Options = new() { ["Status"] = ["Published", "Draft", "Hidden"], ["PricegridType"] = Keys("cms.price_grids"), ["RatingFormula"] = Keys("cms.rating_formulas") },
            Children = [("Components", "page-components", "PageId"), ("Visits", "site-visits", "PageId")],
            BeforeSave = async c =>
            {
                var p = Row<Page>(c);
                p.Path = p.Path.Trim('/');
                if (p.Path.Length > 0 && Strs("cms.reserved").Contains(p.Path.Split('/')[0])) throw new ActionError("That address is used by the system.");
                if (await c.Db.Pages.AnyAsync(x => x.Path == p.Path && x.Id != p.Id)) throw new ActionError("Another page already uses that address.");
            },
        };
        yield return Child<PageComponent>("page-components", app, "Page Components", ["PageId", "ContentBlockId", "Zone", "Position"], [], ["Zone"], "Position", delete: true, options: new() { ["Zone"] = zones });
        yield return new ResourceDef { Key = "templates", App = app, Entity = typeof(Template), Title = "Page Templates", Singular = "Template", List = ["Name", "Description", "File"], Search = ["Name"], Sort = "Name", Children = [("Pages", "pages", "TemplateId")] };
        yield return new ResourceDef
        {
            Key = "content-blocks", App = app, Entity = typeof(ContentBlock), Title = "Content Blocks", Singular = "Content Block", Delete = true,
            List = ["Name", "Slug", "CategoryId", "UpdatedAt"], Search = ["Name", "Slug", "Html"], Filters = ["CategoryId"], Sort = "Name", ReadOnly = ["UpdatedBy"],
            BeforeSave = c => { var b = Row<ContentBlock>(c); b.UpdatedBy = c.Me.UserId; b.Slug ??= System.Text.RegularExpressions.Regex.Replace(b.Name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-'); return Task.CompletedTask; },
            Children = [("Used on", "page-components", "ContentBlockId")],
        };
        yield return new ResourceDef { Key = "block-categories", App = app, Entity = typeof(BlockCategory), Title = "Block Categories", Singular = "Category", List = ["Name"], Sort = "Name", Children = [("Blocks", "content-blocks", "CategoryId")] };
        foreach (var (key, type, title) in new[] { ("blocked-ips", "ip", "Blocked IPs"), ("blocked-areas", "area", "Blocked Areas") })
            yield return new ResourceDef
            {
                Key = key, App = app, Entity = typeof(SiteBlock), Title = title, Singular = type == "ip" ? "Blocked IP" : "Blocked Area", Delete = true,
                Description = type == "ip" ? "IP addresses (or ranges like 192.0.2.0/24) that can't open the website." : "Parts of the website (paths) switched off, with the message visitors see.",
                List = ["Value", "Reason", "Active", "Hits", "LastHitAt", "ExpiresAt"], Search = ["Value", "Reason"], Filters = ["Active"], Sort = "-CreatedAt",
                Fixed = new() { ["Type"] = type }, ReadOnly = ["Hits", "LastHitAt", "UserId"],
                BeforeSave = c => { Row<SiteBlock>(c).UserId ??= c.Me.UserId; return Task.CompletedTask; },
                Actions = [new() { Key = "toggle", Label = "Turn On/Off", Run = c => { var b = Row<SiteBlock>(c); b.Active = !b.Active; return Task.FromResult(b.Active ? "Block is on." : "Block is off."); } }],
            };
        yield return new ResourceDef
        {
            Key = "cookie-consents", App = app, Entity = typeof(CookieConsent), Title = "Cookie Consents", Singular = "Consent", Create = false, Edit = false,
            List = ["CreatedAt", "Choice", "Analytics", "Marketing", "PolicyVersion", "CustomerId", "Page"], Search = ["Visitor", "Ip"], Filters = ["Choice", "Analytics", "Marketing", "PolicyVersion"], Sort = "-CreatedAt",
        };
        yield return new ResourceDef
        {
            Key = "site-visits", App = app, Entity = typeof(SiteVisit), Title = "Visitors", Singular = "Visit", Create = false, Edit = false,
            List = ["CreatedAt", "Path", "Ip", "Referrer", "Blocked", "CustomerId"], Search = ["Path", "Ip", "Visitor", "Referrer"], Filters = ["Blocked"], Sort = "-CreatedAt",
        };
        yield return new ResourceDef { Key = "sites", App = app, Entity = typeof(Site), Title = "Sites", Singular = "Site", List = ["Name", "Domain", "Phone", "Status"], Sort = "Name" };
    }

    private static IEnumerable<ResourceDef> Astro()
    {
        const string app = "astro";
        yield return new ResourceDef
        {
            Key = "plans", App = app, Entity = typeof(Plan), Title = "Plans", Singular = "Plan",
            List = ["Name", "Internal", "Type", "Term", "Etf", "Mrc", "Green", "Active"], Search = ["Name", "Internal", "Slug"], Filters = ["Type", "Term", "Active"], Sort = "Name",
            Options = new() { ["Type"] = ["Resi", "Biz"] }, Children = [("Rates", "rates", "PlanId"), ("In Groups", "plan-group-plans", "PlanId")],
        };
        yield return new ResourceDef { Key = "plan-groups", App = app, Entity = typeof(PlanGroup), Title = "Plan Groups", Singular = "Plan Group", List = ["Name", "Slug"], Search = ["Name", "Slug"], Sort = "Name", Children = [("Plans", "plan-group-plans", "PlanGroupId")] };
        yield return Child<PlanGroupPlan>("plan-group-plans", app, "Plans in Groups", ["PlanGroupId", "PlanId", "Position"], [], [], "Position", delete: true);
        yield return new ResourceDef
        {
            Key = "rates", App = app, Entity = typeof(Rate), Title = "Rates", Singular = "Rate", Description = "Energy charge (¢/kWh) per plan and utility area. Saving adds a row with an effective date; the website switches on that date.",
            List = ["PlanId", "MarketId", "Energy", "EffectiveOn", "CreatedBy"], Filters = ["MarketId", "PlanId"], Sort = "-EffectiveOn", Edit = false, ReadOnly = ["CreatedBy"],
            BeforeSave = c => { Row<Rate>(c).CreatedBy = c.Me.UserId; return Task.CompletedTask; },
        };
        yield return new ResourceDef
        {
            Key = "tdsp-fees", App = app, Entity = typeof(TdspFee), Title = "TDSP Fees", Singular = "TDSP Fee", Description = "Utility delivery charges. History is kept: add a row with the date it takes effect.",
            List = ["MarketId", "PerKwh", "PerBill", "EffectiveOn"], Filters = ["MarketId"], Sort = "-EffectiveOn", Edit = false, ReadOnly = ["CreatedBy"],
            BeforeSave = c => { Row<TdspFee>(c).CreatedBy = c.Me.UserId; return Task.CompletedTask; },
        };
        yield return Child<TermDiscount>("term-discounts", app, "Term Discounts", ["Term", "Region", "Discount"], ["Region"], ["Region", "Term"], "Term");
        yield return Child<TermEtf>("term-etfs", app, "ETF by Term", ["Term", "Amount"], [], [], "Term");
        yield return new ResourceDef
        {
            Key = "byop-products", App = app, Entity = typeof(ByopProduct), Title = "Build Your Own Products", Singular = "Product",
            List = ["Name", "Key", "Type", "Step", "RateAdj", "Monthly", "Active"], Search = ["Name", "Key"], Filters = ["Step", "Type", "Active"], Sort = "Position",
            Options = new() { ["Step"] = ["Get Started", "Lower Your Bill", "Using Less", "Save More", "Protectin'"], ["Type"] = ["Discount", "Included", "Add-on", "Product", "Protection"] },
        };
        yield return new ResourceDef
        {
            Key = "rate-imports", App = app, Entity = typeof(RateImport), Title = "Rate Uploads", Singular = "Rate Upload", Create = false, Edit = false,
            List = ["CreatedAt", "FileName", "Status", "Rows", "Unchanged", "EffectiveOn", "AppliedAt"], Filters = ["Status"], Sort = "-CreatedAt",
            Actions =
            [
                new() { Key = "confirm", Label = "Confirm & Apply", When = r => ((RateImport)r).Status == "ready", Run = async c =>
                {
                    var imp = Row<RateImport>(c);
                    var plans = await c.Db.Plans.ToDictionaryAsync(p => p.Internal, p => p.Id);
                    var markets = await c.Db.Markets.ToDictionaryAsync(m => m.Name, m => m.Id);
                    var n = 0;
                    foreach (var ch in imp.Changes?.AsArray() ?? [])
                        if (plans.TryGetValue(ch!["plan"]!.ToString(), out var pid) && markets.TryGetValue(ch["market"]!.ToString(), out var mid))
                        { c.Db.Rates.Add(new Rate { PlanId = pid, MarketId = mid, Energy = (decimal)ch["to"]!, EffectiveOn = imp.EffectiveOn ?? DateOnly.FromDateTime(DateTime.Today), CreatedBy = c.Me.UserId }); n++; }
                    imp.Status = "applied"; imp.AppliedBy = c.Me.UserId; imp.AppliedAt = Now;
                    return $"{n} rates saved, effective {imp.EffectiveOn:yyyy-MM-dd}.";
                } },
                new() { Key = "discard", Label = "Discard", Tone = "danger", When = r => ((RateImport)r).Status == "ready", Run = c => { Row<RateImport>(c).Status = "discarded"; return Task.FromResult("Upload discarded."); } },
            ],
        };
        yield return new ResourceDef
        {
            Key = "markets", App = "sheriff", Entity = typeof(Market), Title = "Markets", Singular = "Market",
            List = ["Name", "Short", "Description", "Region", "Phone"], Search = ["Name", "Description"], Sort = "Id", Children = [("Zip Ranges", "zip-ranges", "MarketId"), ("TDSP Fees", "tdsp-fees", "MarketId")],
        };
        yield return Child<ZipRange>("zip-ranges", "sheriff", "Zip Ranges", ["ZipFrom", "ZipTo", "MarketId"], [], ["MarketId"], "ZipFrom", delete: true);
    }
}
