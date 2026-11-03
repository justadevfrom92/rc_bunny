using Microsoft.EntityFrameworkCore;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Resources;

public static partial class ResourceRegistry
{
    private static IEnumerable<ResourceDef> Corral()
    {
        const string app = "corral";
        yield return new ResourceDef
        {
            Key = "customers", App = app, Entity = typeof(Customer), Title = "Accounts", Singular = "Account",
            Description = "Every field of every account, and each section of the account (the original Corral item pages).",
            List = ["Account", "Name", "Type", "City", "Status", "Exception", "Balance"], Search = ["Account", "Name", "Email", "Phone", "Esiid", "Address", "Ticket"],
            Filters = ["Status", "Type", "Exception", "Source"], Sort = "-CreatedAt", Create = false, Delete = true, WritePerm = null,
            ReadOnly = ["Account", "Ticket", "Balance", "Stars", "Username", "TecScore", "Ip", "IpLocation", "ReferralCode", "DepositHeld"],
            Options = new() { ["Status"] = Keys("admin.customer_statuses"), ["Exception"] = Strs("admin.exceptions"), ["Type"] = ["Residential", "Small Business"] },
            Children =
            [
                ("Payments", "payments", "CustomerId"), ("Bills", "bills", "CustomerId"), ("Notes", "notes", "CustomerId"), ("Flags", "customer-flags", "CustomerId"),
                ("Products", "customer-products", "CustomerId"), ("Credits & Debits", "ledger-entries", "CustomerId"), ("Payment Methods", "payment-methods", "CustomerId"),
                ("Plan Terms", "plan-terms", "CustomerId"), ("Service Addresses", "service-addresses", "CustomerId"), ("ERCOT Transactions", "ercot-transactions", "CustomerId"),
                ("Queues", "queue-logs", "CustomerId"), ("Files", "customer-files", "CustomerId"), ("Emails & Texts", "contact-logs", "CustomerId"),
                ("Rewards Stars", "star-entries", "CustomerId"), ("API Calls", "api-logs", "CustomerId"), ("Phone Calls", "phonecalls", "CustomerId"),
                ("Support Tickets", "support-tickets", "CustomerId"), ("Exception Work Items", "work-items", "CustomerId"), ("History", "history-items", "CustomerId"),
                ("Shop Orders", "shop-orders", "CustomerId"), ("Refunds", "refunds", "CustomerId"), ("Survey Responses", "survey-responses", "CustomerId"),
            ],
            Actions =
            [
                new() { Key = "recalculate-stars", Label = "Recalculate Stars", Run = async c =>
                {
                    var cust = Row<Customer>(c);
                    var total = await c.Db.StarEntries.Where(s => s.CustomerId == cust.Id).SumAsync(s => (double)s.Stars);
                    cust.Stars = (int)Math.Max(0, total);
                    return $"Stars recalculated: {cust.Stars:N0}.";
                } },
                new() { Key = "add-flag", Label = "Add Flag", Inputs = new() { ["flag"] = "Flag" }, Run = c =>
                {
                    var cust = Row<Customer>(c);
                    var flag = In(c, "flag");
                    if (!Strs("corral.flags").Contains(flag)) throw new ActionError("Pick a flag from the list: " + string.Join(", ", Strs("corral.flags")));
                    c.Db.CustomerFlags.Add(new CustomerFlag { CustomerId = cust.Id, Flag = flag, UserId = c.Me.UserId, Author = c.Me.Name });
                    return Task.FromResult($"Flag {flag} added.");
                } },
                new() { Key = "add-to-queue", Label = "Move to Queue", Inputs = new() { ["queue"] = "Queue" }, Run = c =>
                {
                    var cust = Row<Customer>(c);
                    var q = In(c, "queue");
                    c.Db.QueueLogs.Add(new QueueLog { CustomerId = cust.Id, Queue = q, EnteredAt = Now, UserId = c.Me.UserId, Note = "Moved by " + c.Me.Name });
                    return Task.FromResult($"Moved into {q}.");
                } },
            ],
        };
        yield return Child<Payment>("payments", app, "Payments", ["PaidOn", "Reference", "Amount", "Method", "Source", "Status", "CustomerId"], ["Reference", "Confirmation"], ["Status", "Method", "Source"], "-PaidOn",
            create: false, edit: false, actions: PaymentActions());
        yield return Child<Bill>("bills", app, "Bills", ["BilledOn", "Invoice", "Kwh", "Amount", "DueOn", "AmountPaid", "CustomerId"], ["Invoice", "Reference"], ["BillType"], "-BilledOn", create: false);
        yield return Child<Note>("notes", app, "Notes", ["CreatedAt", "Author", "Category", "Action", "Priority", "Body", "CustomerId"], ["Body", "Author"], ["Category", "Priority"], "-CreatedAt",
            options: new() { ["Category"] = Keys("corral.note_categories"), ["Priority"] = Strs("corral.priorities") }, readOnly: ["Author", "UserId"],
            before: c => { var n = Row<Note>(c); if (n.Id == 0) { n.Author = c.Me.Name; n.UserId = c.Me.UserId; } return Task.CompletedTask; });
        yield return Child<CustomerFlag>("customer-flags", app, "Account Flags", ["Flag", "Author", "CreatedAt", "RemovedAt", "CustomerId"], ["Flag"], ["Flag"], "-CreatedAt",
            options: new() { ["Flag"] = Strs("corral.flags") }, readOnly: ["Author", "UserId"], delete: true,
            before: c => { var f = Row<CustomerFlag>(c); f.Author ??= c.Me.Name; f.UserId ??= c.Me.UserId; return Task.CompletedTask; },
            actions: [new() { Key = "remove", Label = "Remove", When = r => ((CustomerFlag)r).RemovedAt == null, Run = c => { Row<CustomerFlag>(c).RemovedAt = Now; return Task.FromResult("Flag removed."); } }]);
        yield return Child<CustomerProduct>("customer-products", app, "Account Products", ["Product", "CreatedAt", "RemovedAt", "CustomerId"], ["Product"], ["Product"], "-CreatedAt",
            actions: [new() { Key = "remove", Label = "Remove", When = r => ((CustomerProduct)r).RemovedAt == null, Run = c => { Row<CustomerProduct>(c).RemovedAt = Now; return Task.FromResult("Product removed."); } }]);
        yield return Child<LedgerEntry>("ledger-entries", app, "Credits & Debits", ["CreatedAt", "Kind", "Amount", "Description", "Status", "CustomerId"], ["Description"], ["Status", "Kind"], "-CreatedAt",
            options: new() { ["Kind"] = ["credit", "debit"], ["Status"] = ["pending", "applied", "denied", "deleted"] }, readOnly: ["Status", "DecidedBy", "DecidedAt", "UserId"],
            before: c => { var e = Row<LedgerEntry>(c); if (e.Id == 0) { e.Status = "pending"; e.UserId = c.Me.UserId; } return Task.CompletedTask; },
            actions: LedgerActions());
        yield return Child<PaymentMethod>("payment-methods", app, "Payment Methods", ["Type", "Nickname", "Last4", "Expires", "Vendor", "Autopay", "RemovedAt", "CustomerId"], ["Last4", "Nickname"], ["Type", "Vendor"], "-Id",
            create: false, actions:
            [
                new() { Key = "autopay", Label = "Use for AutoPay", When = r => !((PaymentMethod)r).Autopay && ((PaymentMethod)r).RemovedAt == null, Run = async c =>
                {
                    var m = Row<PaymentMethod>(c);
                    foreach (var o in await c.Db.PaymentMethods.AsTracking().Where(x => x.CustomerId == m.CustomerId && x.Id != m.Id).ToListAsync()) o.Autopay = false;
                    m.Autopay = true;
                    (await c.Db.Customers.AsTracking().FirstAsync(x => x.Id == m.CustomerId)).Autopay = true;
                    return "AutoPay now uses this method.";
                } },
                new() { Key = "remove", Label = "Remove", Tone = "danger", When = r => ((PaymentMethod)r).RemovedAt == null, Run = c => { var m = Row<PaymentMethod>(c); m.RemovedAt = Now; m.Autopay = false; return Task.FromResult("Payment method removed."); } },
            ]);
        yield return Child<PlanTerm>("plan-terms", app, "Plan Terms", ["PlanId", "RateClass", "Rate2000", "ContractStart", "ContractEnd", "Status", "CustomerId"], [], ["Status"], "-OrderedAt",
            options: new() { ["Status"] = ["current", "not started", "ended", "cancelled"] });
        yield return Child<ServiceAddress>("service-addresses", app, "Service Addresses", ["Street", "City", "Zip", "Esiid", "ServiceStart", "ServiceEnd", "CustomerId"], ["Street", "Esiid"], [], "-OrderedAt");
        yield return Child<ErcotTransaction>("ercot-transactions", app, "ERCOT Transactions", ["TransDate", "TransType", "Label", "Purpose", "Esiid", "Tracking", "Status", "CustomerId"], ["Esiid", "Tracking", "TransType"], ["Purpose", "Status", "TransType"], "-TransDate",
            create: false, options: new() { ["Status"] = ["linked", "unlinked", "cancelled"] },
            actions: [new() { Key = "unlink", Label = "Unlink", When = r => ((ErcotTransaction)r).Status == "linked", Run = c => { Row<ErcotTransaction>(c).Status = "unlinked"; return Task.FromResult("Transaction unlinked."); } },
                      new() { Key = "cancel", Label = "Cancel", Tone = "danger", When = r => ((ErcotTransaction)r).Status != "cancelled", Run = c => { Row<ErcotTransaction>(c).Status = "cancelled"; return Task.FromResult("Transaction cancelled."); } }]);
        yield return Child<QueueLog>("queue-logs", app, "Queue Logs", ["Queue", "EnteredAt", "ExitedAt", "Note", "CustomerId"], ["Queue", "Note"], ["Queue"], "-EnteredAt",
            options: new() { ["Queue"] = Strs("corral.queues") },
            actions: [new() { Key = "exit", Label = "Take Out of Queue", When = r => ((QueueLog)r).ExitedAt == null, Run = c => { Row<QueueLog>(c).ExitedAt = Now; return Task.FromResult("Taken out of the queue."); } }]);
        yield return Child<CustomerFile>("customer-files", app, "Account Files", ["Name", "Kind", "GeneratedAt", "Size", "CustomerId"], ["Name"], ["Kind"], "-CreatedAt",
            actions: [new() { Key = "regenerate", Label = "Regenerate", Run = c => { var f = Row<CustomerFile>(c); f.GeneratedAt = Now; f.Regenerated++; return Task.FromResult($"{f.Name} regenerated."); } }]);
        yield return Child<ContactLog>("contact-logs", app, "Emails & Texts", ["SentAt", "Channel", "Template", "Status", "OpenedAt", "ClickedAt", "CustomerId"], ["Template", "Body", "Phone"], ["Channel", "Status", "Direction"], "-CreatedAt", create: false);
        yield return Child<StarEntry>("star-entries", "bounty", "Stars", ["CreatedAt", "Reason", "Stars", "RewardOfferId", "Fulfillment", "CustomerId"], ["Reason"], ["Fulfillment"], "-CreatedAt", create: false);
        yield return Child<ApiLog>("api-logs", app, "API Calls", ["CreatedAt", "Api", "Action", "Status", "ResponseMs", "CustomerId"], ["Api", "Action", "Endpoint"], ["Api", "Status"], "-CreatedAt", create: false, edit: false);
        yield return Child<Phonecall>("phonecalls", app, "Phone Calls", ["StartedAt", "Phone", "Direction", "DurationSec", "Disposition", "UserId", "CustomerId"], ["Phone", "AgentId", "Transcript"], ["Direction", "Disposition"], "-StartedAt",
            options: new() { ["Direction"] = ["inbound", "outbound"], ["Disposition"] = Keys("corral.resolutions") },
            before: c => { var p = Row<Phonecall>(c); if (p.Id == 0) { p.UserId ??= c.Me.UserId; if (p.StartedAt == default) p.StartedAt = Now; } return Task.CompletedTask; });
        yield return new ResourceDef
        {
            Key = "sms", App = app, Entity = typeof(ContactLog), Title = "SMS", Singular = "Text", Description = "Texts to and from customers. New Text sends one (logged until Twilio keys are set).",
            List = ["SentAt", "Direction", "Phone", "Body", "Status", "CustomerId"], Search = ["Phone", "Body"], Filters = ["Direction", "Status"], Sort = "-CreatedAt", Edit = false,
            Fixed = new() { ["Channel"] = "SMS" }, Hidden = ["OpenedAt", "ClickedAt", "DroppedAt", "CampaignId"],
            BeforeSave = async c =>
            {
                var l = Row<ContactLog>(c);
                if (string.IsNullOrWhiteSpace(l.Body)) throw new ActionError("Write the message.");
                var cust = await c.Db.Customers.FirstOrDefaultAsync(x => x.Id == l.CustomerId) ?? throw new ActionError("Pick the account.");
                l.Phone ??= cust.Phone; l.Direction = "out"; l.Template = "Custom SMS"; l.UserId = c.Me.UserId; l.SentAt = Now;
                l.Status = Environment.GetEnvironmentVariable("TWILIO_SID") is null ? "logged" : "sent";
            },
        };
        yield return new ResourceDef
        {
            Key = "work-items", App = app, Entity = typeof(WorkItem), Title = "Exception Work Items", Singular = "Work Item",
            List = ["Queue", "CustomerId", "Summary", "CreatedAt", "ResolvedAt", "ResolvedBy"], Search = ["Summary"], Filters = ["Queue"], Sort = "-CreatedAt",
            Options = new() { ["Queue"] = Keys("admin.queues") }, ReadOnly = ["ResolvedAt", "ResolvedBy"],
            Actions = [new() { Key = "resolve", Label = "Resolve", When = r => ((WorkItem)r).ResolvedAt == null, Run = c =>
            {
                var w = Row<WorkItem>(c); w.ResolvedAt = Now; w.ResolvedBy = c.Me.UserId;
                if (w.CustomerId is { } cid) AccountNote(c, cid, $"Exception work item resolved ({w.Queue}).");
                return Task.FromResult("Resolved.");
            } }],
        };
        // CRM
        var stages = Strs("corral.crm.stages") is { Length: > 0 } s ? s : ["new", "contacted", "qualified", "quote", "negotiation", "won", "lost"];
        yield return new ResourceDef
        {
            Key = "crm-leads", App = app, Entity = typeof(CrmLead), Title = "Leads", Singular = "Lead",
            List = ["Name", "Company", "Kind", "Stage", "EstValue", "OwnerId", "NextFollowUpOn"], Search = ["Name", "Company", "Email", "Phone", "City"], Filters = ["Stage", "Kind", "Source"], Sort = "-CreatedAt",
            Options = new() { ["Stage"] = stages, ["Kind"] = ["Residential", "Commercial"], ["Source"] = ["Website", "Referral", "Broker", "Event", "Cold call", "Partner"] },
            ReadOnly = ["StageChangedAt", "ClosedAt"], Children = [("Activities", "crm-activities", "LeadId")],
            BeforeSave = c => { var l = Row<CrmLead>(c); l.OwnerId ??= c.Me.UserId; if (l.Id == 0) l.StageChangedAt = Now; return Task.CompletedTask; },
            Actions =
            [
                new() { Key = "stage", Label = "Move Stage", Inputs = new() { ["stage"] = "New stage (" + string.Join(", ", stages) + ")" }, Run = c =>
                {
                    var l = Row<CrmLead>(c); var st = In(c, "stage");
                    if (!stages.Contains(st)) throw new ActionError("Pick one of: " + string.Join(", ", stages));
                    c.Db.CrmActivities.Add(new CrmActivity { LeadId = l.Id, UserId = c.Me.UserId, Type = "stage", Subject = $"Stage: {l.Stage} → {st}", DoneAt = Now });
                    l.Stage = st; l.StageChangedAt = Now; l.ClosedAt = st is "won" or "lost" ? Now : null;
                    return Task.FromResult($"Moved to {st}.");
                } },
                new() { Key = "log", Label = "Log Activity", Inputs = new() { ["type"] = "Type (call, email, meeting, task)", ["subject"] = "Subject", ["note:body"] = "Details", ["due_on"] = "Follow-up date (optional, YYYY-MM-DD)" }, Run = c =>
                {
                    var l = Row<CrmLead>(c);
                    DateOnly? due = DateOnly.TryParse(In(c, "due_on", false), out var d) ? d : null;
                    c.Db.CrmActivities.Add(new CrmActivity { LeadId = l.Id, UserId = c.Me.UserId, Type = In(c, "type"), Subject = In(c, "subject"), Body = In(c, "body", false), DueOn = due, DoneAt = due is null ? Now : null });
                    if (due is not null) l.NextFollowUpOn = due;
                    return Task.FromResult("Activity logged.");
                } },
            ],
        };
        yield return new ResourceDef
        {
            Key = "crm-activities", App = app, Entity = typeof(CrmActivity), Title = "Follow-ups", Singular = "Activity",
            List = ["DueOn", "Type", "Subject", "LeadId", "PartnerId", "UserId", "DoneAt"], Search = ["Subject", "Body"], Filters = ["Type"], Sort = "DueOn",
            Options = new() { ["Type"] = ["call", "email", "meeting", "task", "stage"] },
            Actions = [new() { Key = "done", Label = "Mark Done", When = r => ((CrmActivity)r).DoneAt == null, Run = c => { Row<CrmActivity>(c).DoneAt = Now; return Task.FromResult("Done."); } }],
        };
        yield return new ResourceDef
        {
            Key = "crm-partners", App = app, Entity = typeof(CrmPartner), Title = "Partners & Brokers", Singular = "Partner",
            List = ["Name", "Kind", "ContactName", "City", "CommissionRate", "CommissionBasis", "Status", "AgreementEndsOn"], Search = ["Name", "ContactName", "Email"], Filters = ["Kind", "Status"], Sort = "Name",
            Options = new() { ["Kind"] = ["broker", "partner"], ["CommissionBasis"] = ["per_kwh", "per_signup", "percent"], ["Status"] = ["active", "paused", "ended"] },
            Children = [("Leads", "crm-leads", "PartnerId"), ("Activities", "crm-activities", "PartnerId")],
        };
        // Products (website Shop) and their orders
        yield return new ResourceDef
        {
            Key = "shop-products", App = app, Entity = typeof(ShopProduct), Title = "Products", Singular = "Product",
            List = ["Sku", "Name", "Cat", "Price", "Was", "Stars", "Stock", "Active"], Search = ["Sku", "Name", "Blurb"], Filters = ["Cat", "Active", "Stock"], Sort = "Position",
            Options = new() { ["Cat"] = Keys("shop.categories") },
        };
        yield return new ResourceDef
        {
            Key = "shop-orders", App = app, Entity = typeof(ShopOrder), Title = "Product Orders", Singular = "Order", Create = false,
            List = ["Number", "CustomerId", "Total", "PayWith", "Status", "PaymentStatus", "CreatedAt"], Search = ["Number"], Filters = ["Status", "PaymentStatus"], Sort = "-CreatedAt",
            Options = new() { ["Status"] = ["placed", "shipped", "delivered", "cancelled"], ["PaymentStatus"] = ["paid", "pending", "refunded", "on bill"] },
            ReadOnly = ["Number", "Items", "Subtotal", "Tax", "Shipping", "Total", "Monthly", "Stars", "PlacedBy"],
        };
        yield return new ResourceDef
        {
            Key = "history-items", App = app, Entity = typeof(HistoryItem), Title = "History", Singular = "History Entry", Create = false, Edit = false,
            List = ["CreatedAt", "Model", "Group", "Action", "Summary", "UserId", "CustomerId"], Search = ["Summary", "Model"], Filters = ["Group", "Action", "Model"], Sort = "-CreatedAt",
        };
    }

    private static List<ActionDef> PaymentActions() =>
    [
        new() { Key = "reverse", Label = "Reverse", Perm = "refunds", Tone = "danger", Confirm = "Reverse this payment? The amount goes back on the balance.", When = r => ((Payment)r).Status == "Success", Run = async c =>
        {
            var p = Row<Payment>(c);
            var cust = await c.Db.Customers.AsTracking().FirstAsync(x => x.Id == p.CustomerId);
            p.Status = "Reversed"; p.ReversedAt = Now; p.ReversedBy = c.Me.UserId;
            cust.Balance += p.Amount;
            AccountNote(c, cust.Id, $"Payment {p.Reference} for ${p.Amount:N2} reversed.", "Payment", "Payment Dispute");
            return $"Payment {p.Reference} reversed.";
        } },
    ];

    private static List<ActionDef> LedgerActions() =>
    [
        new() { Key = "apply", Label = "Apply", When = r => ((LedgerEntry)r).Status == "pending", Run = async c =>
        {
            var e = Row<LedgerEntry>(c);
            var cust = await c.Db.Customers.AsTracking().FirstAsync(x => x.Id == e.CustomerId);
            cust.Balance += e.Kind == "credit" ? -e.Amount : e.Amount;
            e.Status = "applied"; e.DecidedBy = c.Me.UserId; e.DecidedAt = Now; e.DecisionNote = c.Input["note"]?.ToString();
            AccountNote(c, cust.Id, $"{(e.Kind == "credit" ? "Credit" : "Debit")} of ${e.Amount:N2} applied: {e.Description}.", "Billing", "Fees");
            return $"{e.Kind} applied to the balance.";
        } },
        new() { Key = "deny", Label = "Deny", Tone = "danger", Inputs = new() { ["note"] = "Why" }, When = r => ((LedgerEntry)r).Status == "pending", Run = c =>
        {
            var e = Row<LedgerEntry>(c); e.Status = "denied"; e.DecidedBy = c.Me.UserId; e.DecidedAt = Now; e.DecisionNote = In(c, "note");
            return Task.FromResult("Denied.");
        } },
    ];

    /// <summary>A screen for one section of the account (or any table with a few columns).</summary>
    private static ResourceDef Child<T>(string key, string app, string title, string[] list, string[] search, string[] filters, string sort,
        bool create = true, bool edit = true, bool delete = false, Dictionary<string, string[]>? options = null, string[]? readOnly = null,
        Func<ActionContext, Task>? before = null, List<ActionDef>? actions = null) => new()
    {
        Key = key, App = app, Entity = typeof(T), Title = title, List = list, Search = search, Filters = filters, Sort = sort, Create = create, Edit = edit,
        Delete = delete, Options = options ?? [], ReadOnly = readOnly ?? [], BeforeSave = before, Actions = actions ?? [],
    };
}
