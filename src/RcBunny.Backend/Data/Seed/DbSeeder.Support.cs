using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data.Entities;

namespace RcBunny.Data.Seed;

public partial class DbSeeder
{
    /// <summary>Corral → CRM: brokers/partners, leads at every stage, activities and follow-ups (CrmSeeder).</summary>
    private async Task SeedCrm()
    {
        var owners = (await db.Users.Include(u => u.Role).Where(u => u.Active && u.Company == null).ToListAsync()).Where(u => u.Role?.Perms.Contains("corral") == true).Select(u => u.Id).ToList();
        var markets = await db.Markets.Select(m => m.Id).ToListAsync();
        var partners = new List<CrmPartner>();
        foreach (var (name, kind, rate, basis) in new[] { ("Lone Star Energy Brokers", "broker", 0.004m, "per_kwh"), ("Gulf Coast Commercial Brokers", "broker", 0.003m, "per_kwh"), ("HomeMove Realty Partners", "partner", 50m, "per_signup"), ("Hill Country Apartments Group", "partner", 25m, "per_signup"), ("Panhandle Ag Co-op", "partner", 2m, "percent") })
        {
            var p = new CrmPartner { Name = name, Kind = kind, ContactName = Pick(new[] { "Pat Doe", "Lee Sample", "Robin Test" }), Email = $"contact@{Slug(name)[..Math.Min(14, Slug(name).Length)]}.example.com",
                Phone = $"(555) 555-02{rng.Next(10, 99)}", City = Pick(new[] { "Houston", "Dallas", "Austin", "San Antonio" }), CommissionRate = rate, CommissionBasis = basis, Status = "active",
                OwnerId = Pick(owners), AgreementEndsOn = today.AddMonths(rng.Next(2, 20)), Notes = "Signed agreement on file." };
            partners.Add(p);
            db.CrmPartners.Add(p);
        }
        await db.SaveChangesAsync();
        var stages = Strs(AppConfig.At("corral.crm.stages")) is { Count: > 0 } s ? s : ["new", "contacted", "qualified", "quote", "negotiation", "won", "lost"];
        var towns = new[] { "Katy", "Sugar Land", "Plano", "Round Rock", "Frisco", "Pearland" };
        var biz = new[] { "Dental", "Bakery", "Auto Repair", "Self Storage", "Church", "Fitness", "Dry Cleaners" };
        var first = new[] { "Avery", "Blake", "Carmen", "Diego", "Elena", "Frank", "Gina", "Hector", "Ivy", "Jon" };
        for (var i = 0; i < 48; i++)
        {
            var kind = i % 3 == 0 ? "Residential" : "Commercial";
            var name = $"{first[i % first.Length]} {new[] { "Sample", "Placeholder", "Testman", "Demo" }[i % 4]}";
            var company = kind == "Residential" ? null : $"{towns[i % towns.Length]} {biz[i % biz.Length]}";
            var stage = stages[i % stages.Count];
            var lead = new CrmLead
            {
                Name = name, Company = company, Title = company == null ? null : "Owner", Kind = kind, Email = $"{name.ToLower().Replace(' ', '.')}@example.com", Phone = $"(555) 555-03{i:00}",
                City = towns[i % towns.Length], MarketId = Pick(markets), Source = Pick(new[] { "Website", "Referral", "Broker", "Event", "Cold call" }), PartnerId = i % 4 == 0 ? Pick(partners).Id : null,
                Stage = stage, AnnualKwh = kind == "Residential" ? rng.Next(8000, 25000) : rng.Next(40000, 900000), EstValue = rng.Next(800, 60000), Sites = kind == "Residential" ? 1 : rng.Next(1, 8),
                CurrentProvider = Pick(new[] { "TXU", "Reliant", "Gexa", "Direct Energy", "Unknown" }), ContractEndsOn = today.AddDays(rng.Next(10, 300)), OwnerId = Pick(owners),
                NextFollowUpOn = stage is "won" or "lost" ? null : today.AddDays(rng.Next(-5, 15)), LostReason = stage == "lost" ? "Price" : null, StageChangedAt = Ago(30),
                ClosedAt = stage is "won" or "lost" ? Ago(10) : null, CreatedAt = Ago(90, 20), Notes = company == null ? null : "Wants a fixed rate through the summer.",
            };
            db.CrmLeads.Add(lead);
            foreach (var k in Enumerable.Range(0, 1 + i % 3))
                db.CrmActivities.Add(new CrmActivity { Lead = lead, UserId = lead.OwnerId, Type = new[] { "call", "email", "meeting", "task" }[(i + k) % 4], Subject = new[] { "Intro call", "Sent quote", "Site walk", "Follow up on quote" }[(i + k) % 4],
                    Body = "Sample activity.", DueOn = k == 0 && lead.NextFollowUpOn != null ? lead.NextFollowUpOn : null, DoneAt = k == 0 && lead.NextFollowUpOn != null ? null : Ago(20), CreatedAt = Ago(40) });
        }
        foreach (var p in partners)
            db.CrmActivities.Add(new CrmActivity { PartnerId = p.Id, UserId = p.OwnerId, Type = "meeting", Subject = "Quarterly review", Body = "Went over sign-ups and commissions.", DoneAt = Ago(30), CreatedAt = Ago(30) });
        await Save();
    }

    /// <summary>Ranger: the ticket team (in-office and Brightline) and 140 tickets in every state with replies, notes and ratings (RangerSeeder).</summary>
    private async Task SeedRanger()
    {
        var users = await db.Users.ToDictionaryAsync(u => u.Email);
        var lead = users["lead@example.com"].Id;
        (string Email, string Team, string Level)[] team =
        [
            ("csr@example.com", "office", "agent"), ("morgan.placeholder@staff.example.com", "office", "agent"), ("jordan.sampleton@staff.example.com", "office", "agent"),
            ("riley.testwood@staff.example.com", "office", "agent"), ("quinn.mockridge@staff.example.com", "office", "agent"), ("lead@example.com", "office", "manager"),
            ("taylor.demoson@staff.example.com", "office", "manager"), ("alex.o@brightline.example.net", "third_party", "agent"), ("blair.a@brightline.example.net", "third_party", "agent"),
            ("cameron.w@brightline.example.net", "third_party", "agent"), ("devon.s@brightline.example.net", "third_party", "manager"), ("kai.f@brightline.example.net", "third_party", "agent"),
            ("3p.csr@example.com", "third_party", "agent"), ("3p.manager@example.com", "third_party", "manager"), ("3p.admin@example.com", "third_party", "company_admin"),
        ];
        foreach (var (t, i) in team.Select((t, i) => (t, i)))
            if (users.TryGetValue(t.Email, out var u))
                db.RangerAgents.Add(new RangerAgent { UserId = u.Id, Team = t.Team, Level = t.Level, Active = u.Active, AddedBy = lead, CreatedAt = now.AddDays(-(120 - i * 6)) });
        await db.SaveChangesAsync();

        var workers = team.Where(t => t.Level == "agent" && users[t.Email].Active).Select(t => (users[t.Email], t.Team)).ToList();
        var managers = team.Where(t => t.Level == "manager").Select(t => (users[t.Email], t.Team)).ToList();
        var scripts = new (string Topic, string Subject, string Ask, string Answer, string? FollowUp, string? Final)[]
        {
            ("billing", "Why is my bill higher this month?", "My bill went from about $120 to $210 and nothing changed at home. Can someone check it?", "I looked at your usage: it went from 1,050 kWh to 1,830 kWh during the heat wave, and the meter reads were actual reads. Your rate didn't change. I can set up a payment arrangement if that helps.", "Can you split it into two payments?", "Done: half is due on your normal date and half with your next bill."),
            ("billing", "Payment not showing", "I paid on Monday from my bank but My Account still shows the balance.", "Found it: your payment posted Tuesday morning and your balance is $0.00 now. My Account updates overnight.", null, null),
            ("billing", "Change my due date", "Can I move my due date to the 20th? I get paid mid-month.", "I moved your due date to the 20th starting with your next bill.", null, null),
            ("service", "Moving next month", "I'm moving to a new apartment across town on the 15th. How do I move my service?", "I set up service at your new address starting the 15th and stopping at the old one on the 16th, so there's no gap. Your plan and rate move with you.", "Perfect, thanks!", null),
            ("service", "Start date for new service", "I signed up last week. When does my service start?", "Your switch is scheduled with the utility for the 3rd. You don't need to do anything; your lights stay on during the switch.", null, null),
            ("plan", "Renewal options", "My contract ends soon. What are my options to renew?", "You can renew for 12 or 24 months. The 24-month plan is 0.4¢ less per kWh. I can switch you today with no fee; just reply with which you'd like.", "24 months please.", "You're renewed on the 24-month plan; the confirmation email is on its way."),
            ("plan", "Early termination fee question", "If I cancel early, how much is the fee?", "Your plan's cancellation fee is $150. It's waived if you're moving outside our service area; send a copy of your new lease and I'll waive it.", null, null),
            ("rewards", "Stars missing", "I paid on time but didn't get my stars this month.", "Your on-time stars post two days after the payment clears. I see 25 stars posted this morning, so you're all caught up.", null, null),
            ("rewards", "Gift card not received", "I redeemed stars for a gift card a week ago and never got the email.", "The gift card email went to an old address. I re-sent it to the email on your account just now.", "Got it, thanks.", null),
            ("account", "Can't sign in", "My Account keeps saying my password is wrong.", "I sent a password reset link to your email. It's good for 60 minutes.", null, null),
            ("account", "Add my spouse to the account", "Can my husband call in about the account too?", "I added him as an authorized user. He'll need the account number when he calls.", null, null),
            ("outage", "Power out since this morning", "My power has been out since 7am. Is it you or the utility?", "Outages are handled by your utility. I checked their outage map and crews are working in your area; the estimate is 2pm.", null, null),
            ("other", "Paperless billing", "Please stop mailing me paper bills.", "Done: you're on paperless billing and you'll get 5 bonus stars on your next bill.", null, null),
        };
        var good = new[] { "Fast and friendly, thank you!", "Super helpful.", "Fixed it the same day.", "Easy, thanks!", null, null, null };
        var bad = new[] { "Took too long to get an answer.", "Still don't understand why my bill went up.", "Had to ask twice.", "The answer didn't fix my problem." };
        var customers = await db.Customers.Where(c => c.Email != null).OrderBy(c => c.Password == null).ThenBy(c => c.Id).ToListAsync();
        for (var n = 0; n < 140; n++)
        {
            var sc = scripts[n % scripts.Length];
            var c = new[] { 1, 9, 18, 30, 45, 61 }.Contains(n) ? customers[0] : customers[1 + n * 7 % (customers.Count - 1)];
            var state = n < 8 ? "new" : n < 17 ? "open" : n < 24 ? "waiting" : n % 11 == 0 ? "closed" : "solved";
            var opened = n < 24 ? now.AddMinutes(-rng.Next(20, 60 * (n < 8 ? 10 : 70))) : now.AddMinutes(-rng.Next(360, 60 * 24 * 90));
            var pool = workers.Where(w => w.Team == (n % 8 < 3 ? "third_party" : "office")).ToList();
            var owner = state == "new" ? null : pool[(n + n / 8) % pool.Count].Item1;
            var channel = n < 8 ? (n % 3 == 1 ? "Email" : "My Account") : new[] { 0, 2, 4, 6 }.Contains(n % 9) ? "Phone" : new[] { 1, 5 }.Contains(n % 9) ? "Email" : "My Account";
            var via = channel switch { "Email" => "email", "Phone" => "phone", _ => "portal" };
            var t = new SupportTicket
            {
                CustomerId = c.Id, Name = c.Name, Email = c.Email!, Subject = sc.Subject, Topic = sc.Topic, Channel = channel, Status = state,
                Priority = sc.Topic == "outage" ? "high" : n % 17 == 0 ? "urgent" : n % 6 == 0 ? "low" : "normal", AssignedTo = owner?.Id, CreatedAt = opened, UpdatedAt = opened, LastActivityAt = opened,
            };
            db.SupportTickets.Add(t);
            var at = opened;
            void Msg(string author, string body, User? u = null, int minutes = 0, string? direction = null, int? duration = null)
            {
                at = at.AddMinutes(minutes);
                t.Messages.Add(new SupportTicketMessage { Author = author, Body = body, UserId = u?.Id, Via = author == "system" ? "portal" : via, Direction = direction, Duration = duration, Contact = via == "email" ? c.Email : via == "phone" ? c.Phone : null, CreatedAt = at });
            }
            void NoteFor(string body, User u, int minutes, string priority = "Low", string action = "Internal Note")
            {
                at = at.AddMinutes(minutes);
                db.Notes.Add(new Note { CustomerId = c.Id, UserId = u.Id, Author = u.Name, Category = "Ticket", Action = action, Priority = priority, Body = body, SupportTicket = t, CreatedAt = at, UpdatedAt = at });
            }
            if (channel == "Phone" && owner != null)
            {
                Msg("agent", $"Called about: {sc.Ask}\n\nWhat I did: {sc.Answer}", owner, 0, "in", rng.Next(90, 1100));
                t.FirstResponseAt = at;
                if (n % 3 != 2) NoteFor(new[] { "Verified identity with the last 4 of the account and the service address.", "Checked the meter reads with the utility while they were on the line.", "Customer prefers a call back after 5pm." }[n % 3], owner, rng.Next(2, 20));
            }
            else Msg("customer", sc.Ask);
            if (owner != null && channel != "Phone")
            {
                Msg("system", $"{owner.Name} picked this up.", owner, rng.Next(5, 240));
                if (n % 2 == 1) NoteFor("Looked at the last 3 bills and payments before answering.", owner, rng.Next(2, 20));
                Msg("agent", $"Hi {c.FirstName}, {sc.Answer}", owner, rng.Next(3, 30));
                t.FirstResponseAt = at;
                if (n % 9 == 2)
                {
                    var mg = managers.First().Item1;
                    t.Watchers.Add(new SupportTicketWatcher { UserId = mg.Id, JoinedAt = at.AddMinutes(10) });
                    Msg("system", $"{mg.Name} joined in.", mg, 10);
                    NoteFor("Looks right to me. If they push back, offer the deferred payment plan.", mg, 5, "Medium");
                }
                if (sc.FollowUp != null && state != "waiting")
                {
                    Msg("customer", sc.FollowUp, null, rng.Next(30, 600));
                    if (sc.Final != null) Msg("agent", sc.Final, owner, rng.Next(10, 180));
                }
            }
            if (owner != null && new[] { 9, 13, 19, 22 }.Contains(n))
            {
                var why = new[] { "Customer is upset and wants to talk to a manager.", "Not sure I can approve this; need a manager to OK it.", null, "Second time they wrote in about this." }[n % 4];
                if (why != null) NoteFor(why, owner, 4, "Medium", "Manager Request");
                t.ManagerRequestedAt = at; t.ManagerRequestedBy = owner.Id; t.ManagerRequestNote = why;
            }
            t.LastActivityAt = at;
            if (state is "solved" or "closed" && owner != null)
            {
                t.SolvedAt = at.AddMinutes(5);
                Msg("system", $"{owner.Name} set the status to Solved.", owner, 5);
                if (n % 4 != 3)
                {
                    var ok = rng.Next(1, 101) <= (pool[0].Team == "third_party" ? 82 : 93);
                    t.Rating = ok ? "good" : "bad";
                    t.RatingComment = ok ? good[n % good.Length] : bad[n % bad.Length];
                    var rated = at.AddHours(rng.Next(1, 30));
                    t.RatedAt = rated > now ? now : rated;
                }
            }
            if (n % 3 == 1 && channel != "Phone")
                t.Attachments.Add(new SupportTicketAttachment { FromCustomer = true, Name = sc.Topic is "service" or "plan" ? "lease.pdf" : "screenshot.png", Path = $"ranger/{n}/file", Mime = sc.Topic is "service" or "plan" ? "application/pdf" : "image/png", Size = rng.Next(20_000, 400_000), CreatedAt = opened });
        }
        await Save();
    }

    /// <summary>Sheriff → Heartbeat: pulses, checks over the last day, controls and actions (HeartbeatSeeder).</summary>
    private async Task SeedHeartbeat()
    {
        db.HeartbeatPulses.Add(new HeartbeatPulse { Key = "scheduler", BeatAt = now.AddSeconds(-40), Meta = new JsonObject { ["host"] = "web-1" } });
        db.HeartbeatPulses.Add(new HeartbeatPulse { Key = "queue", BeatAt = now.AddSeconds(-12), Meta = new JsonObject { ["pending"] = 0 } });
        var checks = new[] { "database", "scheduler", "queue", "storage", "mail", "website", "my_account", "admin", "api_keys" };
        for (var i = 0; i < 48; i++)
        {
            var at = now.AddMinutes(-30 * i);
            var res = new JsonObject();
            foreach (var c in checks)
                res[c] = new JsonObject { ["status"] = c == "mail" && i % 7 == 3 ? "warn" : c == "queue" && i == 20 ? "fail" : "ok", ["ms"] = rng.Next(1, 120), ["message"] = c == "mail" && i % 7 == 3 ? "Mail is set to log (sample)" : "OK" };
            db.Heartbeats.Add(new Heartbeat { CheckedAt = at, Status = i == 20 ? "fail" : i % 7 == 3 ? "warn" : "ok", Results = res, DurationMs = rng.Next(80, 900) });
        }
        var admin = await UserId("admin@example.com");
        db.HeartbeatControls.Add(new HeartbeatControl { Key = "website", State = "up", UserId = admin, ChangedAt = Ago(10) });
        db.HeartbeatControls.Add(new HeartbeatControl { Key = "queue", State = "up", Reason = "Restarted after the stuck job", UserId = admin, ChangedAt = Ago(3) });
        foreach (var (k, a, ok) in new[] { ("queue", "restart", true), ("cache", "clear", true), ("website", "maintenance_on", true), ("website", "maintenance_off", true), ("mail", "send_test", false) })
            db.HeartbeatActions.Add(new HeartbeatAction { CheckKey = k, Action = a, UserId = admin, Ok = ok, Output = ok ? "Done (sample)" : "Mail is set to log, nothing sent (sample)", DurationMs = rng.Next(40, 3000), CreatedAt = Ago(12) });
        await Save();
    }

    /// <summary>Rodeo → PDFs: headers/footers, letter templates, PDFs made from them (PdfTemplateSeeder).</summary>
    private async Task SeedPdfs()
    {
        var hdr = new PdfPart { Kind = "header", Name = "Letterhead", Title = AppConfig.At("brand.name")?.ToString(), Subtitle = "1000 Sample Ave, Houston, TX 77002", Color = "#102247", IsDefault = true };
        var hdr2 = new PdfPart { Kind = "header", Name = "Collections", Title = "Account Notice", Subtitle = "Please read promptly", Color = "#C80611" };
        var ftr = new PdfPart { Kind = "footer", Name = "Legal Footer", Color = "#96999d", Body = "PUCT Cert. No. XXXXX · Questions? Call 1-800-555-0100.", IsDefault = true };
        db.PdfParts.AddRange(hdr, hdr2, ftr);
        var templates = new (string Name, string Cat, string Title, string Body, PdfPart H)[]
        {
            ("Welcome Letter", "Account", "Welcome to the family!", "Dear {{first_name}},\n\nThank you for choosing us. Your account number is {{account}} and your plan is {{plan}}. Your service starts on {{start_date}}.\n\nWelcome aboard!", hdr),
            ("Disconnect Notice", "Collections", "Disconnect Notice", "Dear {{name}},\n\nYour balance of {{balance}} is past due. To avoid disconnection, please pay by {{due_date}}.", hdr2),
            ("Deposit Letter", "Account", "Deposit Required", "Dear {{first_name}},\n\nBased on your credit check, a deposit of {{deposit_due}} is required before we can start service.", hdr),
            ("Renewal Notice", "Account", "Your Contract Is Ending", "Dear {{first_name}},\n\nYour plan ends soon. Renew now to keep a fixed rate.", hdr),
            ("Payment Arrangement", "Collections", "Payment Arrangement Confirmation", "Dear {{name}},\n\nThis confirms your payment arrangement for {{balance}}.", hdr2),
        };
        var pdfs = templates.Select(t => new PdfTemplate { Name = t.Name, Category = t.Cat, Title = t.Title, Body = t.Body, Footer = "Thank you for being a customer.", ShowAddress = true, Status = "active", PdfHeader = t.H, PdfFooter = ftr }).ToList();
        db.PdfTemplates.AddRange(pdfs);
        await db.SaveChangesAsync();
        var customers = await db.Customers.Select(c => new { c.Id, c.Name, c.Account }).Take(20).ToListAsync();
        var users = await db.Users.Where(u => u.Active).Select(u => u.Id).Take(5).ToListAsync();
        for (var i = 0; i < 18; i++)
        {
            var t = pdfs[i % pdfs.Count];
            var c = customers[i % customers.Count];
            db.PdfGenerations.Add(new PdfGeneration { PdfTemplateId = t.Id, CustomerId = c.Id, UserId = users[i % users.Count], FileName = $"{Slug(t.Name)}-{c.Account}.pdf", Pages = 1 + i % 2, Size = rng.Next(18_000, 90_000),
                Title = t.Title, Body = t.Body.Replace("{{first_name}}", c.Name.Split(' ')[0]).Replace("{{name}}", c.Name).Replace("{{account}}", c.Account), Footer = t.Footer, ShowAddress = true,
                Header = new JsonObject { ["title"] = hdr.Title, ["color"] = hdr.Color }, CreatedAt = Ago(30) });
        }
        db.PdfTestSends.Add(new PdfTestSend { PdfTemplateId = pdfs[0].Id, UserId = users[0], CustomerId = customers[0].Id, Recipients = "marketing@example.com", Status = "logged", CreatedAt = Ago(3) });
        await Save();
    }

    /// <summary>Astro → Update Rates: sample uploads that went live, wait to be confirmed, or failed (RateImportSeeder).</summary>
    private async Task SeedRateImports()
    {
        var pricing = await UserId("pricing@example.com");
        db.RateImports.Add(new RateImport { UserId = pricing, FileName = "rates-2026-09-15.csv", Path = "rate-imports/1.csv", Status = "applied", Rows = 150, Unchanged = 112, EffectiveOn = today.AddDays(-40), AppliedBy = pricing, AppliedAt = Ago(41, 40), FinishedAt = Ago(41, 40),
            Changes = new JsonArray(new JsonObject { ["plan"] = "CT12", ["market"] = "TX-E-ONCOR", ["from"] = 11.4, ["to"] = 11.2 }), CreatedAt = Ago(42, 41) });
        db.RateImports.Add(new RateImport { UserId = pricing, FileName = "rates-next-month.csv", Path = "rate-imports/2.csv", Status = "ready", Rows = 150, Unchanged = 140, EffectiveOn = today.AddDays(10), FinishedAt = Ago(1),
            Changes = new JsonArray(new JsonObject { ["plan"] = "BTT24", ["market"] = "TX-E-CENTERPOINT", ["from"] = 10.9, ["to"] = 10.6 }, new JsonObject { ["plan"] = "36IF", ["market"] = "TX-E-ONCOR", ["from"] = 10.1, ["to"] = 9.9 }), CreatedAt = Ago(1) });
        db.RateImports.Add(new RateImport { UserId = pricing, FileName = "rates-bad.xlsx", Path = "rate-imports/3.xlsx", Status = "failed", Rows = 0, Unchanged = 0, Error = "Column 'Energy' is missing.", Problems = new JsonArray("Row 1: no Energy column"), FinishedAt = Ago(5), CreatedAt = Ago(5) });
        await Save();
    }

    /// <summary>Shop: products from config/shop.php and a few orders (website and phone).</summary>
    private async Task SeedShop()
    {
        var products = new List<ShopProduct>();
        foreach (var (p, i) in (AppConfig.At("shop.products") as JsonArray ?? []).Select((p, i) => (p!.AsArray(), i)))
            products.Add(new ShopProduct { Sku = (string)p[0]!, Name = (string)p[1]!, Cat = (string)p[2]!, Price = (decimal)p[3]!, Was = p[4] is null ? null : (decimal)p[4]!, Rating = (decimal)p[5]!, Reviews = (int)p[6]!, Stars = (int)p[7]!, Stock = (bool)p[8]!, Badge = (string?)p[9], Icon = (string?)p[10] ?? "plug", Blurb = (string?)p[11], Position = i });
        db.ShopProducts.AddRange(products);
        await db.SaveChangesAsync();
        var customers = await db.Customers.Where(c => c.Status == "Good - On Flow").Select(c => new { c.Id, c.Name, c.Address, c.City, c.Zip }).Take(8).ToListAsync();
        var csr = await UserId("csr@example.com");
        for (var i = 0; i < 10 && products.Count > 0; i++)
        {
            var c = customers[i % customers.Count];
            var items = new JsonArray();
            decimal sub = 0;
            foreach (var p in products.Skip(i % products.Count).Take(1 + i % 2))
            {
                items.Add(new JsonObject { ["sku"] = p.Sku, ["name"] = p.Name, ["qty"] = 1, ["price"] = p.Price });
                sub += p.Price;
            }
            var tax = Math.Round(sub * 0.0825m, 2);
            db.ShopOrders.Add(new ShopOrder { Number = $"SO-{1000 + i}", CustomerId = c.Id, Items = items, Subtotal = sub, Tax = tax, Shipping = sub > 99 ? 0 : 7.95m, Total = sub + tax + (sub > 99 ? 0 : 7.95m),
                Stars = (int)sub, PayWith = i % 3 == 0 ? "bill" : "card", ShipTo = new JsonObject { ["name"] = c.Name, ["street"] = c.Address, ["city"] = c.City, ["state"] = "TX", ["zip"] = c.Zip },
                Status = new[] { "placed", "shipped", "delivered", "cancelled" }[i % 4], PaymentStatus = i % 4 == 3 ? "refunded" : "paid", PlacedBy = i % 5 == 0 ? csr : null, CreatedAt = Ago(40) });
        }
        await Save();
    }

    /// <summary>History: what changed on accounts and in the admin (HistorySeeder, PageHistorySeeder).</summary>
    private async Task SeedHistory()
    {
        var users = await db.Users.Where(u => u.Active).Select(u => u.Id).Take(8).ToListAsync();
        var payments = await db.Payments.Select(p => new { p.Id, p.CustomerId, p.Amount, p.Reference }).Take(80).ToListAsync();
        foreach (var p in payments)
            db.HistoryItems.Add(new HistoryItem { CustomerId = p.CustomerId, UserId = null, Model = "ItemPayment_model", Group = "Payments", RecordId = p.Id, Action = "created", Summary = $"Payment {p.Reference} ${p.Amount:N2}", Data = new JsonObject { ["amount"] = p.Amount }, CreatedAt = Ago(120) });
        var customers = await db.Customers.Select(c => new { c.Id, c.Status }).Take(30).ToListAsync();
        foreach (var c in customers)
            db.HistoryItems.Add(new HistoryItem { CustomerId = c.Id, UserId = Pick(users), Model = "TicketCustomer_model", Group = "Account Attributes", RecordId = c.Id, Action = "updated", Summary = "Status changed",
                Changes = new JsonObject { ["status"] = new JsonArray("Submitted", c.Status) }, CreatedAt = Ago(60), Ip = "10.0.0.5" });
        var pages = await db.Pages.Select(p => new { p.Id, p.Title }).Take(12).ToListAsync();
        foreach (var p in pages)
            db.HistoryItems.Add(new HistoryItem { UserId = Pick(users), Model = "Page_model", Group = "Admin Changes", RecordId = p.Id, Action = "updated", Summary = $"Page {p.Title} edited", Changes = new JsonObject { ["title"] = new JsonArray(p.Title + " (old)", p.Title) }, CreatedAt = Ago(45) });
        foreach (var u in users)
            db.HistoryItems.Add(new HistoryItem { UserId = u, Model = "User_model", Group = "Sign-ins", RecordId = u, Action = "login", Summary = "Signed in", CreatedAt = Ago(3), Ip = "10.0.0." + u });
        await Save();
    }

    /// <summary>The bell in the admin: tickets, reports, emails and campaigns pointing at real records (AdminNotificationSeeder).</summary>
    private async Task SeedNotifications()
    {
        var users = await db.Users.Where(u => u.Active).Select(u => u.Id).ToListAsync();
        var tickets = await db.SupportTickets.OrderByDescending(t => t.Id).Select(t => new { t.Id, t.Subject }).Take(5).ToListAsync();
        var runs = await db.ReportRuns.Where(r => r.Status == "completed").Select(r => new { r.Id, r.UserId, r.Title }).Take(10).ToListAsync();
        foreach (var u in users)
        {
            foreach (var (t, i) in tickets.Select((t, i) => (t, i)).Take(3))
                db.AdminNotifications.Add(new AdminNotification { UserId = u, Kind = "ticket", Title = $"New ticket: {t.Subject}", Body = "A customer opened a ticket in My Account.", Url = $"/ranger/tickets/{t.Id}", ReadAt = i == 0 ? null : Ago(2), CreatedAt = Ago(4) });
            db.AdminNotifications.Add(new AdminNotification { UserId = u, Kind = "email", Title = "New email from Pat Sample", Body = "general: Do you serve my area in Katy?", Url = "/ranger/messages", CreatedAt = Ago(2) });
        }
        foreach (var r in runs)
            db.AdminNotifications.Add(new AdminNotification { UserId = r.UserId, Kind = "report", Title = $"Report ready: {r.Title}", Url = $"/walker/runs/{r.Id}", CreatedAt = Ago(6) });
        await Save();
    }
}
