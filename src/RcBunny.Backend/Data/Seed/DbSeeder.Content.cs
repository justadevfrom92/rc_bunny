using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data.Entities;

namespace RcBunny.Data.Seed;

public partial class DbSeeder
{
    private static string Slug(string s) => Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    /// <summary>Lando: site, page templates, content blocks (by category), pages and modules (CmsSeeder, ContentModulesSeeder).</summary>
    private async Task SeedCms()
    {
        var brand = AppConfig.At("brand.name")?.ToString() ?? "Energy Texas";
        var site = new Site { Name = brand, Domain = "localhost", Phone = AppConfig.At("brand.phone")?.ToString(), Status = "active" };
        db.Sites.Add(site);
        foreach (var t in Load("templates"))
            db.Templates.Add(new Template { Name = (string)t!["name"]!, Description = (string?)t["desc"] ?? "" });
        await db.SaveChangesAsync();
        var templates = await db.Templates.ToDictionaryAsync(t => t.Name, t => t.Id);

        var used = new HashSet<string>();
        string Unique(string slug) { var s = slug; for (var k = 2; !used.Add(s); k++) s = $"{slug}-{k}"; return s; }
        var cats = new Dictionary<string, BlockCategory>();
        BlockCategory Cat(string name)
        {
            if (!cats.TryGetValue(name, out var c)) { c = new BlockCategory { Name = name }; db.BlockCategories.Add(c); cats[name] = c; }
            return c;
        }
        foreach (var b in Load("blocks"))
        {
            var name = (string)b!["name"]!;
            var updated = DateTime.Parse((string)b["updated"]!);
            db.ContentBlocks.Add(new ContentBlock { Name = name, Slug = Unique(Slug(name)), Html = (string)b["html"]!, Category = Cat(name.Split(" - ")[0]), Site = site, CreatedAt = updated, UpdatedAt = updated });
        }
        var pageContent = Cat("Page Content");
        var files = AppConfig.At("cms.files") as JsonObject;
        foreach (var p in Load("pages"))
        {
            var path = (string)p!["path"]!;
            var title = (string)p["title"]!;
            ContentBlock? content = null;
            if (p["content"] is { } html)
            {
                content = new ContentBlock { Name = title + " - Primary", Slug = Unique("page-" + Slug(path) + "-primary"), Html = (string)html!, Category = pageContent, Site = site };
                db.ContentBlocks.Add(content);
            }
            var file = files?[path]?.ToString();
            db.Pages.Add(new Page
            {
                Site = site, Path = path, Title = title, TemplateId = templates.GetValueOrDefault((string?)p["template"] ?? "default"),
                Redirect = file != null || string.IsNullOrEmpty((string?)p["redirect"]) ? null : (string)p["redirect"]!, Status = (string)p["status"]!,
                File = file, ContentPrimary = content, MetaDescription = $"{title} — {brand}", HtmlTitle = $"{title} | {brand}",
            });
        }
        // Modules: blocks with {{variables}} filled in by each page
        var modules = Cat("Modules");
        (string Slug, string Name, string Html)[] mods =
        [
            ("location-intro", "Module - Location Intro", "<section class=\"loc-intro\"><h2>Electricity plans in {{city|your city}}</h2><p>We serve {{city|your city}} and the {{utility|local utility}} service area. {{note|Compare fixed-rate plans and start service in minutes.}}</p>[[zip_form]]</section>"),
            ("promo-band", "Module - Promo Band", "<div class=\"rewards-band\"><b>{{heading|Earn Rangler Rewards}}</b> {{text|Earn stars on every on-time payment.}} <a href=\"{{link|/refer-a-friend}}\">{{button|Learn more}}</a></div>"),
            ("job-post", "Module - Job Post", "<article class=\"job-post\"><p>{{summary}}</p><ul><li><b>Location:</b> {{location|Houston, TX}}</li><li><b>Type:</b> {{type|Full time}}</li><li><b>Team:</b> {{team}}</li></ul><p>Send your resume through the <a href=\"/contact-us\">contact page</a>.</p></article>"),
            ("rewards-promo-band", "Rewards Promo Band", "[[block|promo-band|heading=Rangler Rewards|text=Earn stars on every on-time payment and spend them on bill credits.|link=/myaccount/rewards|button=See your stars]]"),
        ];
        var added = db.ChangeTracker.Entries<ContentBlock>().Select(e => e.Entity).ToList();
        foreach (var m in mods)
            if (added.FirstOrDefault(b => b.Slug == m.Slug) is { } existing) existing.Html = m.Html;
            else db.ContentBlocks.Add(new ContentBlock { Slug = Unique(m.Slug), Name = m.Name, Html = m.Html, Category = modules, Site = site });
        await db.SaveChangesAsync();

        var uses = new Dictionary<string, string>
        {
            ["page-locations-houston-primary"] = "[[block|location-intro|city=Houston|utility=CenterPoint]]",
            ["page-locations-dallas-primary"] = "[[block|location-intro|city=Dallas|utility=Oncor]]",
            ["page-locations-fort-worth-primary"] = "[[block|location-intro|city=Fort Worth|utility=Oncor]]",
            ["page-locations-corpus-christi-primary"] = "[[block|location-intro|city=Corpus Christi|utility=AEP Texas Central|note=Coastal homes: ask about our storm-season plans.]]",
        };
        foreach (var b in await db.ContentBlocks.AsTracking().Where(b => uses.Keys.Contains(b.Slug!)).ToListAsync()) b.Html = uses[b.Slug!];
        var band = await db.ContentBlocks.FirstAsync(b => b.Slug == "rewards-promo-band");
        if (await db.Pages.FirstOrDefaultAsync(p => p.Path == "about-us") is { } about)
            db.PageComponents.Add(new PageComponent { PageId = about.Id, ContentBlockId = band.Id, Zone = "sidebar" });
        await Save();
    }

    /// <summary>Rodeo + Bounty + Caboose basics (MarketingRewardsSeeder).</summary>
    private async Task SeedMarketing()
    {
        foreach (var o in Load("reward_offers"))
            db.RewardOffers.Add(new RewardOffer
            {
                Name = (string)o!["name"]!, Description = (string)o["description"]!, Stars = (int)o["stars"]!, Effect = (string)o["effect"]!,
                Value = o["value"]?.ToString(), Position = (int)o["position"]!,
            });
        foreach (var (key, label, stars, perDollar) in new[] { ("on_time_payment", "Bill Payment", 0.05m, true), ("loyalty_monthly", "Monthly Loyalty", 10m, false),
                     ("autopay_monthly", "AutoPay Bonus", 5m, false), ("paperless_signup", "Paperless Signup", 10m, false), ("referral", "Referral", 50m, false) })
            db.RewardRules.Add(new RewardRule { Key = key, Label = label, Stars = stars, PerDollar = perDollar });

        var brand = AppConfig.At("brand.name")?.ToString() ?? "Energy Texas";
        const string wrap = "font-family:Arial,sans-serif;max-width:600px;margin:0 auto";
        (string Kind, string Name, bool Default, string Html)[] parts =
        [
            ("header", "Standard Header", true, $"<div style=\"{wrap};background:#102247;padding:16px 24px\"><a href=\"/\" style=\"color:#fff;font-weight:700;font-size:20px;text-decoration:none\">{brand}</a></div>"),
            ("header", "Rewards Header", false, $"<div style=\"{wrap};background:#00AEEF;padding:16px 24px;color:#fff\"><a href=\"/myaccount/rewards\" style=\"color:#fff;font-weight:700;font-size:20px;text-decoration:none\">Rangler Rewards</a><div style=\"font-size:13px\">from {brand}</div></div>"),
            ("header", "Billing Header", false, $"<div style=\"{wrap};border-bottom:3px solid #102247;padding:14px 24px\"><a href=\"/\" style=\"color:#102247;font-weight:700;font-size:18px;text-decoration:none\">{brand}</a> <span style=\"float:right;color:#555;font-size:13px\">Account {{{{account}}}}</span></div>"),
            ("header", "None", false, ""),
            ("footer", "Standard Footer", true, $"<div style=\"{wrap};padding:20px 24px;text-align:center\"><a href=\"/myaccount\" style=\"display:inline-block;background:#00AEEF;color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;font-weight:700\">Go to My Account</a></div><div style=\"{wrap};border-top:1px solid #ddd;padding:14px 24px;font-size:12px;color:#666;text-align:center\"><a href=\"/myaccount/pay\">Pay My Bill</a> · <a href=\"/plans\">See Plans</a> · <a href=\"/unsubscribe\">Unsubscribe</a><br>{brand} · 1000 Sample Ave, Houston, TX 77002 · PUCT Cert. No. XXXXX</div>"),
            ("footer", "Billing Footer", false, $"<div style=\"{wrap};padding:20px 24px;text-align:center\"><a href=\"/myaccount/pay\" style=\"display:inline-block;background:#c8102e;color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;font-weight:700\">Pay Now</a></div><div style=\"{wrap};border-top:1px solid #ddd;padding:14px 24px;font-size:12px;color:#666;text-align:center\">Questions about your bill? Call 1-800-555-0100.</div>"),
            ("footer", "Marketing Footer", false, $"<div style=\"{wrap};padding:20px 24px;text-align:center\"><a href=\"/myaccount/rewards\" style=\"display:inline-block;background:#00AEEF;color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;font-weight:700\">See My Rewards</a></div><div style=\"{wrap};border-top:1px solid #ddd;padding:14px 24px;font-size:12px;color:#666;text-align:center\"><a href=\"/unsubscribe\">Unsubscribe from offers</a><br>You get this because you opted in to offers from {brand}.</div>"),
            ("footer", "Legal Only", false, $"<div style=\"{wrap};border-top:1px solid #ddd;padding:14px 24px;font-size:12px;color:#666;text-align:center\">{brand} · PUCT Cert. No. XXXXX</div>"),
        ];
        var partIds = new Dictionary<string, EmailPart>();
        foreach (var p in parts) { var e = new EmailPart { Kind = p.Kind, Name = p.Name, IsDefault = p.Default, Html = p.Html }; db.EmailParts.Add(e); partIds[p.Name] = e; }

        (string Name, string Desc, string Color, string[] Templates)[] categories =
        [
            ("Account", "Welcome letters, confirmations and sign-in emails", "#00AEEF", ["Welcome Letter", "Welcome Letter - Renewed", "Renewal - Confirmation", "Freedom Flex - Confirmation", "MyAccount - Password Reset"]),
            ("Billing", "Bills, payments and collections notices", "#2e7d32", ["Bill Ready", "Payment Made", "Billing - Resi - Payment Made", "Disconnect Notice"]),
            ("Marketing", "Offers and campaigns; only to opted-in customers", "#f39c12", ["Renewal Offer", "Winback Offer"]),
            ("Rewards", "Peak Perks and Bounty emails", "#8e44ad", ["MyAcct - Peak Perks - Welcome to Peak Perks"]),
            ("Surveys", "Survey invitations", "#c0392b", ["Customer Survey", "Survey - 90 Day Check-In", "Survey - Farewell"]),
            ("Service", "One-off messages from Customer Service", "#607d8b", ["Custom Message"]),
        ];
        var catByTemplate = new Dictionary<string, EmailCategory>();
        var catByName = new Dictionary<string, EmailCategory>();
        foreach (var (c, i) in categories.Select((c, i) => (c, i)))
        {
            var cat = new EmailCategory { Name = c.Name, Description = c.Desc, Color = c.Color, Position = i + 1 };
            db.EmailCategories.Add(cat);
            catByName[c.Name] = cat;
            foreach (var t in c.Templates) catByTemplate[t] = cat;
        }
        var templates = Load("email_templates").Select(t => (string)t!["name"]!).ToList();
        foreach (var t in Load("email_templates"))
        {
            var name = (string)t!["name"]!;
            var cat = catByTemplate.GetValueOrDefault(name);
            var e = new EmailTemplate { Name = name, Subject = (string)t["subject"]!, Body = (string)t["body"]!, EmailCategory = cat };
            if (cat?.Name == "Billing") { e.EmailHeader = partIds["Billing Header"]; e.EmailFooter = partIds["Billing Footer"]; }
            if (cat?.Name == "Marketing") e.EmailFooter = partIds["Marketing Footer"];
            if (name.Contains("Star") || name.Contains("Reward")) { e.EmailHeader = partIds["Rewards Header"]; e.EmailFooter = partIds["Marketing Footer"]; }
            db.EmailTemplates.Add(e);
        }
        // Survey invitation templates use {{survey:<slug>}} (Rodeo → Survey Emails)
        foreach (var (name, slug) in new[] { ("Survey - 90 Day Check-In", "90-day-check-in"), ("Survey - Farewell", "farewell") })
            if (!templates.Contains(name))
                db.EmailTemplates.Add(new EmailTemplate { Name = name, Subject = name.Replace("Survey - ", "") + ": tell us how we're doing", EmailCategory = catByName["Surveys"],
                    Body = $"<p>Hi {{{{first_name}}}},</p><p>Two minutes to help us serve you better: {{{{survey:{slug}}}}}</p>" });

        foreach (var (msid, name, type) in new[] { ("1001", "Organic Website", "Organic"), ("2001", "Call Center", "Agent"), ("3001", "Google Ads Pay Per Click", "Paid"),
                     ("32001", "Partner Referral Network", "Partner"), ("41001", "Broker - Sample Brokerage", "Broker"), ("52001", "Community Event", "Partner") })
            db.MarketingChannels.Add(new MarketingChannel { Msid = msid, Name = name, Type = type });
        db.PromoCodes.Add(new PromoCode { Code = "FALL25", Description = "$25 bill credit for fall sign-ups", Credit = 25, StartsOn = today.AddDays(-20), EndsOn = today.AddDays(40) });
        db.PromoCodes.Add(new PromoCode { Code = "WELCOME", Description = "Website welcome offer", Credit = 10 });
        await Save();
    }

    /// <summary>Every section of the Corral account page (AccountSeeder, ItemSeeder).</summary>
    private async Task SeedAccounts()
    {
        var customers = await db.Customers.AsTracking().ToListAsync();
        var userIds = await db.Users.Where(u => u.Active && u.Company == null).Select(u => new { u.Id, u.Name }).ToListAsync();
        var offers = await db.RewardOffers.ToListAsync();
        var flags = Strs(AppConfig.At("corral.flags"));
        var queueNames = Strs(AppConfig.At("corral.queues"));
        var products = new[] { "Peak Perks", "Paperless Billing", "AutoPay", "Home Warranty", "ecobee Thermostat", "Freedom Flex", "Solar Buy Back" };
        var plans = await db.Plans.Where(p => p.Type == "Resi").Select(p => new { p.Id, p.Term }).ToListAsync();
        var templates = await db.EmailTemplates.Select(t => t.Name).ToListAsync();
        var apis = new[] { ("Experian", "Credit check"), ("Utilibill", "Send order"), ("Stripe", "Charge card"), ("ERCOT", "814_16 MoveIn"), ("Twilio", "Send SMS") };

        foreach (var (c, i) in customers.Select((c, i) => (c, i)))
        {
            var agent = userIds[i % userIds.Count];
            var start = c.CreatedAt;
            if (i % 7 == 0) db.CustomerFlags.Add(new CustomerFlag { CustomerId = c.Id, Flag = flags[i % flags.Count], UserId = agent.Id, Author = agent.Name, CreatedAt = start.AddDays(2) });
            foreach (var p in products.Where((_, k) => (i + k) % 4 == 0).Take(2))
                db.CustomerProducts.Add(new CustomerProduct { CustomerId = c.Id, Product = p, UserId = agent.Id, CreatedAt = start.AddDays(1), Data = new JsonObject { ["source"] = c.Source } });

            var card = new PaymentMethod { CustomerId = c.Id, Type = "Credit Card", Last4 = rng.Next(1000, 9999).ToString(), Expires = $"{rng.Next(1, 12):00}/{rng.Next(27, 31)}", Nickname = "Visa", Vendor = "Stripe", Autopay = c.Autopay, CreatedAt = start };
            db.PaymentMethods.Add(card);
            if (i % 3 == 0) db.PaymentMethods.Add(new PaymentMethod { CustomerId = c.Id, Type = "Bank Account", Last4 = rng.Next(1000, 9999).ToString(), Nickname = "Checking", Vendor = "Auth.net", CreatedAt = start.AddDays(10) });

            if (i % 5 == 1) db.LedgerEntries.Add(new LedgerEntry { CustomerId = c.Id, Kind = i % 2 == 0 ? "debit" : "credit", Amount = rng.Next(5, 80), Description = i % 2 == 0 ? "Late fee" : "Courtesy credit", Status = "pending", UserId = agent.Id, CreatedAt = Ago(15) });
            if (i % 6 == 2) db.LedgerEntries.Add(new LedgerEntry { CustomerId = c.Id, Kind = "credit", Amount = 25, Description = "Promo FALL25", Status = "applied", UserId = agent.Id, CreatedAt = Ago(60, 20), DecidedAt = Ago(19), DecidedBy = userIds[0].Id });

            var plan = c.PlanId is { } pl ? plans.FirstOrDefault(x => x.Id == pl) : null;
            var cs = DateOnly.FromDateTime(start.AddDays(8));
            db.PlanTerms.Add(new PlanTerm
            {
                CustomerId = c.Id, PlanId = c.PlanId, RateClass = "Residential", EnergyCharge = 0.1m + (decimal)rng.Next(0, 400) / 10000m, Rate2000 = 13 + (decimal)rng.Next(0, 40) / 10m,
                OrderedAt = start, ContractStart = cs, ContractEnd = cs.AddMonths(plan?.Term ?? 12), Status = c.Status == "Good - On Flow" ? "current" : c.Status.StartsWith("Dropped") ? "ended" : "not started",
            });
            db.ServiceAddresses.Add(new ServiceAddress { CustomerId = c.Id, Esiid = c.Esiid, Street = c.Address, City = c.City, Zip = c.Zip, OrderedAt = start, ServiceStart = cs });
            db.ErcotTransactions.Add(new ErcotTransaction { CustomerId = c.Id, Esiid = c.Esiid, TransDate = DateOnly.FromDateTime(start), Purpose = "request", TransType = "814_16", Label = c.EnrollmentType == "MoveIn" ? "MoveIn" : "Switch", Tracking = "T" + rng.Next(1000000, 9999999), Status = "linked", ScheduledOn = cs });
            if (c.Status != "Submitted")
                db.ErcotTransactions.Add(new ErcotTransaction { CustomerId = c.Id, Esiid = c.Esiid, TransDate = DateOnly.FromDateTime(start.AddDays(2)), Purpose = "response", TransType = c.Status.StartsWith("Rejected") ? "814_17" : "814_05", Label = c.Status.StartsWith("Rejected") ? "Reject" : "Accept", Tracking = "T" + rng.Next(1000000, 9999999), Status = "linked" });

            db.QueueLogs.Add(new QueueLog { CustomerId = c.Id, Queue = "QueueCartFulfilledFlowing", EnteredAt = start, ExitedAt = start.AddDays(3), Note = "Order received", UserId = null });
            if (c.Exception != null) db.QueueLogs.Add(new QueueLog { CustomerId = c.Id, Queue = queueNames[i % queueNames.Count], EnteredAt = start.AddDays(3), Note = c.Exception });

            foreach (var (kind, name) in new[] { ("efl", "Electricity Facts Label.pdf"), ("tos", "Terms of Service.pdf"), ("yrac", "Your Rights as a Customer.pdf"), ("welcome", "Welcome Packet.pdf") })
                db.CustomerFiles.Add(new CustomerFile { CustomerId = c.Id, Name = name, Kind = kind, GeneratedAt = start.AddDays(1), Size = rng.Next(40_000, 180_000), CreatedAt = start.AddDays(1) });

            foreach (var k in Enumerable.Range(0, 3))
            {
                var sent = Ago(90);
                db.ContactLogs.Add(new ContactLog
                {
                    CustomerId = c.Id, Channel = k == 2 ? "SMS" : "Email", Template = k == 2 ? "Bill Ready SMS" : Pick(templates), Status = "sent", SentAt = sent,
                    OpenedAt = k < 2 && rng.Next(2) == 0 ? sent.AddHours(3) : null, ClickedAt = k == 0 && rng.Next(4) == 0 ? sent.AddHours(4) : null,
                    Body = k == 2 ? $"Your {AppConfig.At("brand.name")} bill is ready. Reply STOP to opt out." : null, Direction = "out", Phone = k == 2 ? c.Phone : null, CreatedAt = sent,
                });
            }
            if (i % 4 == 0)
                db.ContactLogs.Add(new ContactLog { CustomerId = c.Id, Channel = "SMS", Template = "Inbound", Status = "received", Direction = "in", Phone = c.Phone, Body = Pick(new[] { "When is my bill due?", "Is there an outage near me?", "STOP", "Thanks!" }), SentAt = Ago(20), CreatedAt = Ago(20) });

            var net = 0m;
            foreach (var k in Enumerable.Range(0, 4))
            {
                var stars = k == 3 && c.Stars > 500 ? -500 : rng.Next(20, 400);
                net += stars;
                db.StarEntries.Add(new StarEntry
                {
                    CustomerId = c.Id, Reason = stars < 0 ? "Redeemed: $5 bill credit" : Pick(new[] { "On-time payment", "AutoPay bonus", "Paperless signup", "Monthly loyalty" }),
                    Stars = stars, CreatedAt = start.AddDays(20 * (k + 1)), RewardOfferId = stars < 0 ? offers.FirstOrDefault(o => o.Stars > 0)?.Id : null,
                    Fulfillment = stars < 0 ? (k % 2 == 0 ? "pending" : "fulfilled") : null,
                });
            }
            foreach (var (api, action) in apis.Where((_, k) => (i + k) % 2 == 0))
            {
                var at = start.AddMinutes(rng.Next(1, 600));
                db.ApiLogs.Add(new ApiLog { CustomerId = c.Id, Api = api, Action = action, Status = rng.Next(12) == 0 ? "500" : "200", ResponseMs = rng.Next(80, 1900), Method = "POST", Endpoint = $"https://api.{api.ToLower()}.example/v1/{action.Replace(' ', '-').ToLower()}", Request = "{\"account\":\"" + c.Account + "\"}", Response = "{\"ok\":true}", CreatedAt = at });
            }
            foreach (var k in Enumerable.Range(0, i % 3 + 1))
            {
                var callAgent = userIds[(i + k) % userIds.Count];
                db.Phonecalls.Add(new Phonecall
                {
                    CustomerId = c.Id, UserId = callAgent.Id, AgentId = "A" + (100 + callAgent.Id), Phone = c.Phone ?? "(555) 555-0100", Direction = k % 2 == 0 ? "inbound" : "outbound",
                    StartedAt = Ago(60), DurationSec = rng.Next(40, 1200), Disposition = Pick(new[] { "Resolved", "Follow-up Needed", "Incomplete", "Escalated" }),
                    Transcript = "Agent: Thanks for calling, how can I help?\nCustomer: I have a question about my bill.\nAgent: Let me pull that up for you.",
                });
            }
            if (i % 2 == 0)
                db.Notes.Add(new Note { CustomerId = c.Id, UserId = agent.Id, Author = agent.Name, Category = "Billing", Action = "Balance Inquiry", Disposition = "Balance Inquiry", Priority = "Low", Body = "Customer called about the balance; explained the last bill.", CreatedAt = Ago(30) });
        }
        await Save();
    }
}
