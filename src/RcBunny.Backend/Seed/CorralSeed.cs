using System.Text.Json;
using RcBunny.Data;

namespace RcBunny.Seed;

/// <summary>Sample data for Corral: every customer from ebunny's sample data with a full account, then the CRM and the Shop.</summary>
public static partial class Seeder
{
    private static readonly Random R = new(1219);

    private static async Task SeedCustomersAsync(AppDbContext db, Dictionary<string, Market> markets, Dictionary<string, Plan> plans, string password)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var admin = db.Users.Local.First(u => u.Email == "admin@example.com");
        var csr = db.Users.Local.First(u => u.Email == "csr@example.com");
        var zones = new Dictionary<string, string> { ["TX-E-CENTERPOINT"] = "LZ_HOUSTON", ["TX-E-ONCOR"] = "LZ_NORTH", ["TX-E-AEPCENTRAL"] = "LZ_SOUTH", ["TX-E-AEPNORTH"] = "LZ_WEST", ["TX-E-TNMP"] = "LZ_SOUTH" };
        // Which work queue each sample exception goes in
        var exceptionQueues = new Dictionary<string, string> { ["No ESIID"] = "unprocessed-orders", ["Not sent to Utility"] = "unprocessed-orders", ["Possible Duplicate"] = "duplicate-ips", ["Switch Hold"] = "ercot-exceptions" };
        var issues = new[] { "Billing question", "High bill", "Payment arrangement", "Move out", "Plan renewal", "Outage report" };
        var index = 0;

        foreach (var j in Load("customers"))
        {
            var created = DateTime.Parse(S(j, "created"));
            var name = S(j, "name");
            var parts = name.Split(' ', 2);
            var planCode = S(j, "planCode");
            var plan = plans.GetValueOrDefault(planCode);
            var market = markets.GetValueOrDefault(S(j, "market"));
            var onFlow = S(j, "status").StartsWith("Good");
            var c = new Customer
            {
                Account = S(j, "account"), Ticket = S(j, "ticket"), Name = name, FirstName = parts[0], LastName = parts.ElementAtOrDefault(1) ?? "",
                Email = S(j, "email"), Phone = S(j, "phone"), PhoneType = index % 3 == 0 ? "Home" : "Mobile", Language = index % 5 == 4 ? "Spanish" : "English",
                SsnLast4 = R.Next(1000, 9999).ToString(), TecScore = R.Next(560, 820), Ip = $"10.{R.Next(1, 250)}.{R.Next(1, 250)}.{R.Next(1, 250)}", IpLocation = $"{S(j, "city")}, TX",
                Address = S(j, "address"), City = S(j, "city"), Zip = S(j, "zip"), MarketId = market?.Id, Esiid = S(j, "esiid") is { Length: > 0 } e ? e : null,
                MeterNumber = R.Next(10000000, 99999999).ToString(), LoadProfile = $"{(S(j, "type") == "Residential" ? "RESLOWR" : "BUSMEDLF")}_{(zones.GetValueOrDefault(S(j, "market")) ?? "LZ_NORTH")[3..]}_NIDR_NWS",
                LoadZone = zones.GetValueOrDefault(S(j, "market")), MeterType = "AMSR",
                BillingStreet = S(j, "address"), BillingCity = S(j, "city"), BillingState = "TX", BillingZip = S(j, "zip"),
                Type = S(j, "type"), Status = S(j, "status"), StatusInfo = onFlow ? "Active" : "New", Exception = S(j, "exception") is { Length: > 0 } x ? x : null,
                MoveSwitch = index % 4 == 0 ? "move" : "switch", RequestedStart = DateOnly.FromDateTime(created.AddDays(7)), ActualStart = onFlow ? DateOnly.FromDateTime(created.AddDays(9)) : null,
                Source = S(j, "source") is { Length: > 0 } src ? src : "Phone", PlanId = plan?.Id, RateClass = plan == null ? null : $"{created:yyyyMMdd}_{(plan.TermMonths <= 1 ? "V" : "F")}_{plan.Code}",
                Balance = D(j, "balance"), DueDate = today.AddDays(20), Autopay = j["autopay"]?.GetValue<bool>() ?? false, Paperless = j["paperless"]?.GetValue<bool>() ?? false,
                Channel = S(j, "source") == "Website" ? "1 - Organic (default)" : "2 - Phone", Msid = S(j, "source") == "Website" ? "1001" : "2001",
                Stars = j["stars"]?.GetValue<int>() ?? 0, CreatedAt = created, UpdatedAt = created,
            };
            if (index == 0) { c.Username = "demo.customer"; c.PasswordHash = password; c.LinkedAccounts = null; c.AuthorizedUsers = $"Jamie Sample|(555) 555-0177|{today.AddMonths(-2):yyyy-MM-dd}"; }
            db.Customers.Add(c);
            await db.SaveChangesAsync();
            var id = c.Id;

            // Payment method, then six months of bills and payments
            var method = new PaymentMethod
            {
                CustomerId = id, Type = index % 4 == 3 ? "Bank" : "Card", Last4 = R.Next(1000, 9999).ToString(), Expires = index % 4 == 3 ? null : $"{R.Next(1, 12):00}/{DateTime.Today.Year % 100 + R.Next(1, 4)}",
                Nickname = index % 4 == 3 ? "Checking" : "Visa", Vendor = index % 4 == 3 ? "Authorize.net" : "Stripe", Autopay = c.Autopay, CreatedAt = created,
            };
            db.PaymentMethods.Add(method);
            await db.SaveChangesAsync();
            var bills = onFlow ? 6 : 0;
            var running = 0m;
            for (var i = bills; i >= 1; i--)
            {
                var billed = today.AddMonths(-i + 1).AddDays(-10);
                var kwh = R.Next(700, 1900);
                var amount = i == 1 && c.Balance > 0 ? c.Balance : Math.Round(kwh * 0.15m, 2);
                var paid = i > 1;
                running += amount - (paid ? amount : 0);
                var bill = new Bill
                {
                    CustomerId = id, Reference = $"BL-{c.Account[^6..]}-{billed:yyMM}", BillType = "Regular", BilledOn = billed, DueOn = billed.AddDays(16),
                    PeriodStart = billed.AddMonths(-1), PeriodEnd = billed.AddDays(-1), Kwh = kwh, Amount = amount, AmountPaid = paid ? amount : 0,
                    PaidOn = paid ? billed.AddDays(12) : null, BalanceAfter = running, Invoice = $"INV{c.Account[^5..]}{billed:yyMM}", Ontime = !(i == 3 && index % 6 == 5),
                };
                db.Bills.Add(bill);
                if (paid)
                    db.Payments.Add(new Payment
                    {
                        CustomerId = id, Reference = $"PAY-{c.Account[^4..]}{billed:MMdd}", PaidOn = billed.AddDays(12), PaidTime = new TimeOnly(R.Next(7, 21), R.Next(0, 59), 0),
                        Amount = amount, Method = method.Type, PaymentMethodId = method.Id, Source = c.Autopay ? "AutoPay" : "My Account",
                        Confirmation = $"C{R.Next(100000, 999999)}", Status = "Success",
                    });
            }
            if (onFlow && index % 7 == 2)
                db.Payments.Add(new Payment { CustomerId = id, Reference = $"PAY-{c.Account[^4..]}FAIL", PaidOn = today.AddDays(-40), Amount = 54.10m, Method = method.Type, PaymentMethodId = method.Id, Source = "My Account", Status = "Failed" });

            // Plan term, service address, order-flow queue, products, flags
            if (plan != null)
                db.PlanTerms.Add(new PlanTerm
                {
                    CustomerId = id, PlanId = plan.Id, RateClass = c.RateClass!, Rate2000 = Math.Round(13.5m + (decimal)R.NextDouble() * 3, 1),
                    EnergyCharge = Math.Round(0.08m + (decimal)R.NextDouble() * 0.04m, 8), OrderedAt = created, Status = onFlow ? "current" : "not started",
                    ContractStart = c.ActualStart ?? c.RequestedStart, ContractEnd = plan.TermMonths > 1 ? (c.ActualStart ?? c.RequestedStart)?.AddMonths(plan.TermMonths) : null,
                });
            db.ServiceAddresses.Add(new ServiceAddress { CustomerId = id, Esiid = c.Esiid, Street = c.Address, City = c.City, Zip = c.Zip, OrderedAt = created, ServiceStart = c.ActualStart });
            db.QueueLogs.Add(new QueueLog { CustomerId = id, Queue = "QueueCartSubmit", EnteredAt = created, ExitedAt = created.AddHours(2), Who = "System" });
            db.QueueLogs.Add(new QueueLog { CustomerId = id, Queue = onFlow ? "QueueCartFulfilledFlowing" : "QueueCartFulfilledRequested", EnteredAt = created.AddHours(2), Who = "System" });
            if (c.Exception != null) db.QueueLogs.Add(new QueueLog { CustomerId = id, Queue = "QueueCartException", EnteredAt = created.AddHours(3), Note = c.Exception, Who = "System" });
            if (c.Autopay) db.CustomerProducts.Add(new CustomerProduct { CustomerId = id, Product = "AutoPay", Author = "System", CreatedAt = created });
            if (c.Paperless) db.CustomerProducts.Add(new CustomerProduct { CustomerId = id, Product = "Paperless Billing", Author = "System", CreatedAt = created });
            if (c.Type == "Residential") db.CustomerProducts.Add(new CustomerProduct { CustomerId = id, Product = "Rangler Rewards", Author = "System", CreatedAt = created });
            if (index == 3) db.CustomerFlags.Add(new CustomerFlag { CustomerId = id, Flag = "SOLAR CUSTOMER", Author = "Casey Service", CreatedAt = created.AddDays(20) });
            if (index == 5) db.CustomerFlags.Add(new CustomerFlag { CustomerId = id, Flag = "VIP - Critical Care", Author = "Admin Demo", CreatedAt = created.AddDays(30) });
            if (index == 8) db.CustomerProducts.Add(new CustomerProduct { CustomerId = id, Product = "Surge Protection Program", Author = "Casey Service", CreatedAt = created.AddDays(40) });

            // ERCOT: the switch request and ERCOT's answer
            if (c.Esiid != null)
            {
                var (type, label) = c.MoveSwitch == "move" ? ("814_16", "MoveIn") : ("814_01", "Switch");
                db.ErcotTransactions.Add(new ErcotTransaction { CustomerId = id, Esiid = c.Esiid, TransDate = DateOnly.FromDateTime(created.AddDays(1)), Purpose = "request", ScheduledOn = c.RequestedStart, TransType = type, Label = label, Tracking = $"TRK{R.Next(1000000, 9999999)}", CreatedAt = created.AddDays(1) });
                if (onFlow) db.ErcotTransactions.Add(new ErcotTransaction { CustomerId = id, Esiid = c.Esiid, TransDate = DateOnly.FromDateTime(created.AddDays(2)), Purpose = "response", TransType = c.MoveSwitch == "move" ? "814_17" : "814_02", Label = "Accept", Tracking = $"TRK{R.Next(1000000, 9999999)}", CreatedAt = created.AddDays(2) });
            }

            // Files, emails, texts, calls, notes, exception work items
            foreach (var (kind, file) in new[] { ("welcome", $"welcome-packet-{c.Ticket}.pdf"), ("efl", "electricity-facts-label.pdf"), ("tos", "terms-of-service.pdf"), ("yrac", "your-rights-as-a-customer.pdf") })
                db.CustomerFiles.Add(new CustomerFile { CustomerId = id, Name = file, Kind = kind, CreatedAt = created.AddMinutes(5) });
            if (onFlow) db.CustomerFiles.Add(new CustomerFile { CustomerId = id, Name = $"invoice-{today.AddMonths(-1):yyyy-MM}.pdf", Kind = "bill", CreatedAt = DateTime.UtcNow.AddDays(-12) });
            db.ContactLogs.Add(new ContactLog { CustomerId = id, Channel = "Email", Template = "Welcome Letter", Status = "sent", SentAt = created.AddMinutes(10), CreatedAt = created.AddMinutes(10), Who = "System" });
            if (c.Phone is { Length: > 0 } phone && index % 3 == 0)
            {
                var t0 = DateTime.UtcNow.AddDays(-R.Next(2, 20));
                db.ContactLogs.Add(new ContactLog { CustomerId = id, Channel = "SMS", Template = "Bill ready", Body = $"Energy Texas: your bill of {c.Balance:C} is ready. Reply HELP for help.", Phone = phone, Status = "sent", SentAt = t0, CreatedAt = t0, Who = "System" });
                db.ContactLogs.Add(new ContactLog { CustomerId = id, Channel = "SMS", Template = "Reply", Body = "Can I pay it next week?", Phone = phone, Direction = "in", Status = "received", CreatedAt = t0.AddHours(3) });
                if (index % 2 == 0)
                    db.ContactLogs.Add(new ContactLog { CustomerId = id, Channel = "SMS", Template = "Agent text", Body = "Yes, your due date is the 20th, so next week is fine.", Phone = phone, Status = "not sent", Who = "Casey Service", CreatedAt = t0.AddHours(4), Resolution = "Resolved", ResolvedBy = "Casey Service" });
            }
            if (c.Phone is { Length: > 0 } p2 && index % 2 == 0)
            {
                var start = DateTime.UtcNow.AddDays(-R.Next(1, 30)).AddHours(-R.Next(1, 8));
                db.PhoneCalls.Add(new PhoneCall
                {
                    CustomerId = id, Phone = p2, Direction = index % 4 == 0 ? "inbound" : "outbound", StartedAt = start, DurationSec = R.Next(90, 900),
                    Issue = issues[index % issues.Length], UserId = index % 4 == 0 ? csr.Id : admin.Id, AgentId = $"A{R.Next(100, 999)}",
                    Transcript = $"Agent: Thanks for calling Energy Texas, this is {(index % 4 == 0 ? "Casey" : "Admin")}.\nCustomer: Hi, I have a question about my account.\nAgent: Happy to help. I see your balance is {c.Balance:C}.\nCustomer: Great, that's all I needed.",
                    Resolution = index % 6 == 0 ? "Follow-up Needed" : "Resolved", ResolvedBy = "Casey Service",
                });
            }
            db.Notes.Add(new Note { CustomerId = id, Author = "System", Category = "Order", Action = "New Enrollment", Priority = "Low", Body = $"Enrollment submitted via {c.Source}.", CreatedAt = created });
            if (index % 3 == 1) db.Notes.Add(new Note { CustomerId = id, Author = "Casey Service", Category = "Billing", Action = "Balance Inquiry", Priority = "Medium", Body = "Customer asked why the bill went up. Explained the summer usage.", CreatedAt = created.AddDays(45) });
            if (c.Exception != null && exceptionQueues.TryGetValue(c.Exception, out var queue))
                db.WorkItems.Add(new WorkItem { CustomerId = id, Queue = queue, Summary = $"{c.Exception}: {c.Name}", CreatedAt = created.AddHours(3) });
            if (index % 9 == 4) db.ApiLogs.Add(new ApiLog { CustomerId = id, Api = "Experian", Action = "credit.check", Status = "skipped", Request = "{\"account\":\"" + c.Account + "\"}", Response = "Experian isn't set up: nothing was sent.", CreatedAt = created });
            if (j["bookmarked"]?.GetValue<bool>() == true) db.Bookmarks.Add(new Bookmark { UserId = admin.Id, CustomerId = id });
            index++;
        }
        await db.SaveChangesAsync();

        // A couple of extra work items and pending credits so the queues aren't empty
        var some = db.Customers.Local.Where(c => c.Status.StartsWith("Good")).Take(3).ToList();
        if (some.Count == 3)
        {
            db.WorkItems.Add(new WorkItem { CustomerId = some[0].Id, Queue = "unapplied-payments", Summary = "Check payment not posted to billing" });
            db.WorkItems.Add(new WorkItem { CustomerId = some[1].Id, Queue = "unbilled-orders", Summary = "On flow 35 days with no first bill" });
            db.WorkItems.Add(new WorkItem { CustomerId = some[2].Id, Queue = "unapplied-credits", Summary = "HOWDY50 promo credit to post", ResolvedAt = DateTime.UtcNow.AddDays(-2), ResolvedBy = "Frankie Finance" });
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedCrmAsync(AppDbContext db, User owner, Dictionary<string, Market> markets)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var partners = new[]
        {
            new CrmPartner { Name = "Lone Star Brokers", Kind = "Broker", ContactName = "Dana Ruiz", Email = "dana@lonestarbrokers.example", Phone = "(555) 555-0141", City = "Houston", CommissionBasis = "kwh", CommissionRate = 0.004m, OwnerId = owner.Id, AgreementEndsOn = today.AddMonths(10) },
            new CrmPartner { Name = "Gulf Coast Energy Advisors", Kind = "Broker", ContactName = "Lee Park", Email = "lee@gcea.example", Phone = "(555) 555-0142", City = "Corpus Christi", CommissionBasis = "kwh", CommissionRate = 0.003m, OwnerId = owner.Id },
            new CrmPartner { Name = "Hill Country Realty", Kind = "Realtor", ContactName = "Morgan Hale", Email = "morgan@hcrealty.example", Phone = "(555) 555-0143", City = "Austin", CommissionBasis = "flat", CommissionRate = 0.05m },
            new CrmPartner { Name = "Bayou Property Management", Kind = "Property Manager", ContactName = "Kai Thomas", City = "Houston", CommissionBasis = "flat", CommissionRate = 0.04m, Status = "Paused" },
            new CrmPartner { Name = "Prairie Homes", Kind = "Home Builder", ContactName = "Jo Bell", City = "Dallas", CommissionBasis = "flat", CommissionRate = 0.03m, Status = "Ended" },
        };
        db.CrmPartners.AddRange(partners);
        await db.SaveChangesAsync();

        var leads = new (string Name, string? Company, string Kind, string Source, string Stage, int Kwh, int? Partner)[]
        {
            ("Riley Grant", "Grant Dental Group", "Small Business", "Website", "new", 48000, null),
            ("Jesse Moreno", "Moreno Tacos", "Small Business", "Referral", "contacted", 36000, null),
            ("Taylor Kim", null, "Residential", "Website", "new", 14000, null),
            ("Avery Brooks", "Brooks Self Storage", "Commercial", "Broker", "qualified", 240000, 0),
            ("Quinn Patel", "Patel Motels", "Multi-site", "Broker", "proposal", 610000, 1),
            ("Drew Larson", null, "Residential", "Event", "contacted", 16000, null),
            ("Sky Nguyen", "Nguyen Nails", "Small Business", "Outbound", "qualified", 30000, null),
            ("Rowan Diaz", "Diaz Machine Shop", "Commercial", "Partner", "proposal", 380000, 2),
            ("Emerson Cole", "Cole Family Dentistry", "Small Business", "Website", "won", 52000, null),
            ("Parker Shaw", "Shaw Offices", "Commercial", "Broker", "won", 210000, 0),
            ("Harper Lane", "Lane Bakery", "Small Business", "Referral", "lost", 26000, null),
            ("Sage Rivera", "Rivera Apartments", "Multi-site", "Partner", "proposal", 920000, 3),
        };
        var marketIds = markets.Values.Select(m => m.Id).ToList();
        for (var i = 0; i < leads.Length; i++)
        {
            var l = leads[i];
            var created = DateTime.UtcNow.AddDays(-60 + i * 4);
            var closed = l.Stage is "won" or "lost";
            var lead = new CrmLead
            {
                Name = l.Name, Company = l.Company, Title = l.Company == null ? null : "Owner", Kind = l.Kind, Email = l.Name.ToLower().Replace(' ', '.') + "@example.com",
                Phone = $"(555) 555-02{i:00}", City = new[] { "Houston", "Dallas", "Austin", "Corpus Christi" }[i % 4], MarketId = marketIds[i % marketIds.Count],
                Source = l.Source, PartnerId = l.Partner is { } p ? partners[p].Id : null, AnnualKwh = l.Kwh, EstValue = Math.Round(l.Kwh * 0.14m, 2), Sites = l.Kind == "Multi-site" ? 4 : 1,
                CurrentProvider = i % 2 == 0 ? "Reliant" : "TXU Energy", ContractEndsOn = today.AddMonths(2 + i % 6), Stage = l.Stage, StageChangedAt = created.AddDays(5),
                LostReason = l.Stage == "lost" ? "Price" : null, OwnerId = owner.Id, NextFollowUpOn = closed ? null : today.AddDays(i % 5 - 2),
                CreatedAt = created, ClosedAt = closed ? DateTime.UtcNow.AddDays(-i) : null,
            };
            db.CrmLeads.Add(lead);
            await db.SaveChangesAsync();
            db.CrmActivities.Add(new CrmActivity { LeadId = lead.Id, UserId = owner.Id, Type = "note", Subject = $"Lead added from {l.Source}", CreatedAt = created, DoneAt = created });
            if (l.Stage != "new")
                db.CrmActivities.Add(new CrmActivity { LeadId = lead.Id, UserId = owner.Id, Type = "call", Subject = "Intro call", Body = "Talked through their usage and current contract.", CreatedAt = created.AddDays(2), DoneAt = created.AddDays(2) });
            if (!closed)
                db.CrmActivities.Add(new CrmActivity { LeadId = lead.Id, UserId = owner.Id, Type = "task", Subject = l.Stage == "proposal" ? "Check if they got the proposal" : "Follow up call", DueOn = lead.NextFollowUpOn, CreatedAt = created.AddDays(3) });
        }
        db.CrmActivities.Add(new CrmActivity { PartnerId = partners[0].Id, UserId = owner.Id, Type = "meeting", Subject = "Quarterly review", Body = "Went over the leads they sent this quarter.", CreatedAt = DateTime.UtcNow.AddDays(-12), DoneAt = DateTime.UtcNow.AddDays(-12) });
        await db.SaveChangesAsync();
    }

    private static async Task SeedShopAsync(AppDbContext db)
    {
        var position = 0;
        foreach (var p in Load("shop_products"))
            db.ShopProducts.Add(new ShopProduct
            {
                Sku = S(p, "sku"), Name = S(p, "name"), Category = S(p, "cat"), Price = D(p, "price"), Was = p["was"] is null ? null : D(p, "was"),
                Rating = D(p, "rating"), Reviews = (int)D(p, "reviews"), Stars = (int)D(p, "stars"), InStock = p["stock"]!.GetValue<bool>(),
                Badge = p["badge"]?.ToString(), Icon = S(p, "icon"), Blurb = S(p, "blurb"), Position = ++position,
            });
        await db.SaveChangesAsync();

        var products = db.ShopProducts.Local.ToList();
        var customers = db.Customers.Local.OrderBy(c => c.Id).Take(8).ToList();
        var statuses = new[] { "Placed", "Shipped", "Delivered", "Delivered", "Canceled", "Placed", "Shipped", "Delivered" };
        for (var i = 0; i < customers.Count; i++)
        {
            var c = customers[i];
            var product = products[(i * 3) % products.Count];
            var qty = 1 + i % 2;
            var subtotal = product.Price * qty;
            var tax = Math.Round(subtotal * 0.0825m, 2);
            db.ShopOrders.Add(new ShopOrder
            {
                Number = $"SO-{100001 + i}", CustomerId = c.Id,
                Items = JsonSerializer.Serialize(new[] { new { sku = product.Sku, name = product.Name, qty, price = product.Price, line = subtotal, monthly = false, stars = product.Stars * qty } }),
                Subtotal = subtotal, Tax = tax, Total = subtotal + tax, Stars = product.Stars * qty, PayWith = i % 3 == 0 ? "bill" : "card",
                ShipTo = $"{c.Name}|{c.Address}|{c.City}|TX|{c.Zip}", PaymentStatus = i % 3 == 0 ? "On next bill" : "Paid", Status = statuses[i],
                PlacedBy = i % 2 == 0 ? "Casey Service" : null, CreatedAt = DateTime.UtcNow.AddDays(-i * 4),
            });
        }
        await db.SaveChangesAsync();
    }
}
