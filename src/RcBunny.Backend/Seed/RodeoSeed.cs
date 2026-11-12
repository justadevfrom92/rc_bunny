using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;
using RcBunny.Services;

namespace RcBunny.Seed;

public static partial class Seeder
{
    /// <summary>
    /// Rodeo (ebunny's MarketingRewardsSeeder, CampaignSeeder, SurveySeeder, SurveyEmailSeeder, PdfTemplateSeeder and the
    /// link clicks of FillTablesSeeder): email templates, categories, headers and footers, the suppression list, campaigns
    /// with results, surveys with responses, survey emails, PDF templates and the PDFs made from them, and email tracking.
    /// </summary>
    private static async Task SeedRodeoAsync(AppDbContext db)
    {
        var random = new Random(31);
        var now = DateTime.UtcNow;
        const string Site = "http://localhost:5100";
        var admin = await db.Users.FirstAsync(u => u.Email == "admin@example.com");
        int? Uid(string email) => db.Users.Where(u => u.Email == email).Select(u => (int?)u.Id).FirstOrDefault();

        // ---- Email templates, categories, headers and footers ----
        foreach (var t in Load("email_templates"))
            db.EmailTemplates.Add(new EmailTemplate { Name = S(t, "name"), Subject = S(t, "subject"), Body = S(t, "body") });
        await db.SaveChangesAsync();
        var templates = await db.EmailTemplates.ToDictionaryAsync(t => t.Name);
        var categories = new Dictionary<string, EmailCategory>();
        foreach (var (i, (name, desc, color, names)) in new (string, string, string, string[])[]
        {
            ("Account", "Welcome letters, confirmations and sign-in emails", "#00AEEF", ["Welcome Letter", "Welcome Letter - Renewed", "Renewal - Confirmation", "Freedom Flex - Confirmation", "MyAccount - Password Reset"]),
            ("Billing", "Bills, payments and collections notices", "#2e7d32", ["Bill Ready", "Payment Made", "Billing - Resi - Payment Made", "Disconnect Notice"]),
            ("Marketing", "Offers and campaigns; only to opted-in customers", "#f39c12", ["Renewal Offer", "Winback Offer"]),
            ("Rewards", "Peak Perks and Bounty emails", "#8e44ad", ["MyAcct - Peak Perks - Welcome to Peak Perks"]),
            ("Surveys", "Survey invitations", "#c0392b", ["Customer Survey", "Survey - 90 Day Check-In", "Survey - Farewell"]),
            ("Service", "One-off messages from Customer Service", "#607d8b", ["Custom Message"]),
        }.Index())
        {
            var cat = new EmailCategory { Name = name, Description = desc, Color = color, Position = i + 1 };
            db.EmailCategories.Add(cat);
            await db.SaveChangesAsync();
            categories[name] = cat;
            foreach (var n in names) if (templates.TryGetValue(n, out var t)) t.EmailCategoryId = cat.Id;
        }
        const string Wrap = "font-family:Arial,sans-serif;max-width:600px;margin:0 auto";
        var b = Rodeo.Brand;
        var parts = new Dictionary<string, EmailPart>();
        foreach (var (kind, name, isDefault, html) in new (string, string, bool, string)[]
        {
            ("header", "Standard Header", true, $"<div style=\"{Wrap};background:#102247;padding:16px 24px\"><a href=\"/\" style=\"color:#fff;font-weight:700;font-size:20px;text-decoration:none\">{b}</a></div>"),
            ("header", "Rewards Header", false, $"<div style=\"{Wrap};background:#00AEEF;padding:16px 24px;color:#fff\"><a href=\"/myaccount/rewards\" style=\"color:#fff;font-weight:700;font-size:20px;text-decoration:none\">Rangler Rewards</a><div style=\"font-size:13px\">from {b}</div></div>"),
            ("header", "Billing Header", false, $"<div style=\"{Wrap};border-bottom:3px solid #102247;padding:14px 24px\"><a href=\"/\" style=\"color:#102247;font-weight:700;font-size:18px;text-decoration:none\">{b}</a> <span style=\"float:right;color:#555;font-size:13px\">Account {{{{account}}}}</span></div>"),
            ("header", "None", false, ""),
            ("footer", "Standard Footer", true, $"<div style=\"{Wrap};padding:20px 24px;text-align:center\"><a href=\"/myaccount\" style=\"display:inline-block;background:#00AEEF;color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;font-weight:700\">Go to My Account</a></div>"
                + $"<div style=\"{Wrap};border-top:1px solid #ddd;padding:14px 24px;font-size:12px;color:#666;text-align:center\"><a href=\"/myaccount/pay\">Pay My Bill</a> · <a href=\"/plans\">See Plans</a> · <a href=\"/unsubscribe\">Unsubscribe</a> · <a href=\"/privacy-policy\">Privacy</a><br>{b} · 1000 Sample Ave, Houston, TX 77002 · PUCT Cert. No. XXXXX</div>"),
            ("footer", "Billing Footer", false, $"<div style=\"{Wrap};padding:20px 24px;text-align:center\"><a href=\"/myaccount/pay\" style=\"display:inline-block;background:#c8102e;color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;font-weight:700\">Pay Now</a><div style=\"font-size:13px;color:#555;margin-top:10px\">Never miss a due date: <a href=\"/myaccount/methods\">turn on AutoPay</a>.</div></div>"
                + $"<div style=\"{Wrap};border-top:1px solid #ddd;padding:14px 24px;font-size:12px;color:#666;text-align:center\">Questions about your bill? Call 1-800-555-0100. · <a href=\"/privacy-policy\">Privacy</a></div>"),
            ("footer", "Marketing Footer", false, $"<div style=\"{Wrap};padding:20px 24px;text-align:center\"><a href=\"/myaccount/rewards\" style=\"display:inline-block;background:#00AEEF;color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;font-weight:700\">See My Rewards</a></div>"
                + $"<div style=\"{Wrap};border-top:1px solid #ddd;padding:14px 24px;font-size:12px;color:#666;text-align:center\"><a href=\"https://facebook.com\">Facebook</a> · <a href=\"https://instagram.com\">Instagram</a> · <a href=\"/unsubscribe\">Unsubscribe from offers</a><br>You get this because you opted in to offers from {b}.</div>"),
            ("footer", "Legal Only", false, $"<div style=\"{Wrap};border-top:1px solid #ddd;padding:14px 24px;font-size:12px;color:#666;text-align:center\">{b} · PUCT Cert. No. XXXXX · <a href=\"/privacy-policy\">Privacy</a></div>"),
        })
            db.EmailParts.Add(parts[name] = new EmailPart { Kind = kind, Name = name, IsDefault = isDefault, Html = html });
        await db.SaveChangesAsync();
        foreach (var t in templates.Values)
        {
            if (t.EmailCategoryId == categories["Billing"].Id) (t.EmailHeaderId, t.EmailFooterId) = (parts["Billing Header"].Id, parts["Billing Footer"].Id);
            if (t.EmailCategoryId == categories["Marketing"].Id) t.EmailFooterId = parts["Marketing Footer"].Id;
            if (t.Name.Contains("Star") || t.Name.Contains("Reward")) (t.EmailHeaderId, t.EmailFooterId) = (parts["Rewards Header"].Id, parts["Marketing Footer"].Id);
        }
        await db.SaveChangesAsync();

        // ---- Campaigns: the Fall Renewal Reminder (with two bounces on the suppression list), a draft, and ones with results ----
        var renewalTpl = templates.GetValueOrDefault("Renewal Offer");
        var fall = new Campaign
        {
            Name = "Fall Renewal Reminder", Channel = "Email", Goal = "renewal", EmailTemplateId = renewalTpl?.Id, Audience = """{"statuses":["Good - On Flow"]}""",
            Description = "Contracts ending this fall: renew before month-to-month rates kick in.", Msid = "130001", CapDays = 7,
            Status = "sent", SentAt = now.AddDays(-6), CreatedById = admin.Id, CreatedAt = now.AddDays(-8),
        };
        db.Campaigns.Add(fall);
        await db.SaveChangesAsync();
        var fallTo = await Campaigns.AudienceQuery(db, fall).OrderBy(c => c.Id).Take(14).ToListAsync();
        foreach (var (i, c) in fallTo.Index())
        {
            // Two bounce, and their addresses go on the suppression list (Rodeo → Emails)
            var bounced = (i == 4 || i == 10) && c.Email != null;
            db.ContactLogs.Add(new ContactLog
            {
                CustomerId = c.Id, CampaignId = fall.Id, Channel = "Email", Template = renewalTpl?.Name ?? "", Status = "sent", Who = "System",
                SentAt = now.AddDays(-6), DroppedAt = bounced ? now.AddDays(-6).AddMinutes(2) : null,
                OpenedAt = !bounced && i % 2 == 1 ? now.AddDays(-5) : null, ClickedAt = !bounced && i % 4 == 1 ? now.AddDays(-5) : null, CreatedAt = now.AddDays(-6),
            });
            if (bounced) db.EmailSuppressions.Add(new EmailSuppression { Email = c.Email!.ToLowerInvariant(), Reason = "bounce", CustomerId = c.Id, Note = "Mailbox does not exist (550)", CreatedAt = now.AddDays(-6) });
            if (i < 3) Term(c, fall.SentAt!.Value.AddDays(random.Next(1, 7)));
        }
        fall.Recipients = fallTo.Count;
        await db.SaveChangesAsync();
        var taken = await db.EmailSuppressions.Select(s => s.Email).ToListAsync();
        var others = await db.Customers.Where(c => c.Email != null).OrderByDescending(c => c.Id).ToListAsync();
        foreach (var (reason, note, days) in new[] { ("unsubscribe", "Clicked unsubscribe in a Renewal Offer email", 9), ("complaint", "Marked a Winback Offer email as spam", 14), ("manual", "Asked on the phone to stop all emails", 3) })
        {
            var c = others.Where(x => !taken.Contains(x.Email!.ToLowerInvariant())).Skip(days).FirstOrDefault();
            if (c == null) continue;
            taken.Add(c.Email!.ToLowerInvariant());
            db.EmailSuppressions.Add(new EmailSuppression { Email = c.Email!.ToLowerInvariant(), Reason = reason, CustomerId = c.Id, Note = note, UserId = reason == "manual" ? Uid("csr@example.com") : null, CreatedAt = now.AddDays(-days) });
        }
        db.EmailSuppressions.Add(new EmailSuppression { Email = "old-address@example.net", Reason = "bounce", Note = "Address from a closed account", CreatedAt = now.AddDays(-40) });
        db.Campaigns.Add(new Campaign
        {
            Name = "Peak Perks Enrollment", Channel = "Email", Goal = "product", Product = "Peak Perks", EmailTemplateId = templates.Values.FirstOrDefault(t => t.Name.Contains("Peak Perks"))?.Id,
            Description = "Summer is over, but enrolling now gets customers ready for next season.", Audience = """{"statuses":["Good - On Flow"],"without_product":"Peak Perks"}""", CreatedById = admin.Id,
        });

        // Sent: AutoPay push by text, with sign-ups
        var autopay = new Campaign
        {
            Name = "AutoPay Push", Channel = "SMS", Goal = "product", Product = "AutoPay", CapDays = 7, CreatedById = admin.Id,
            Message = "Hi {{first_name}}, never miss a due date: turn on AutoPay in My Account and earn 50 bonus stars. Reply STOP to opt out.",
            Audience = """{"autopay":"no"}""", Status = "sent", SentAt = now.Date.AddDays(-12).AddHours(15), CreatedAt = now.AddDays(-14),
        };
        // Sent: win-back email
        var winback = new Campaign
        {
            Name = "Come Back to Energy Texas", Channel = "Email", Goal = "winback", EmailTemplateId = (templates.GetValueOrDefault("Winback Offer") ?? renewalTpl)?.Id, CapDays = 14,
            Description = "Customers who left in the last year: $50 credit to come back.", PromoCode = await db.PromoCodes.Select(p => p.Code).FirstOrDefaultAsync(), CreatedById = admin.Id,
            Audience = """{"statuses":["Dropped - Churned","Moved Out"]}""", Status = "sent", SentAt = now.Date.AddDays(-20).AddHours(14.5), CreatedAt = now.AddDays(-22),
        };
        // Scheduled: a rewards text for this weekend
        var saturday = now.Date.AddDays(((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7 is 0 ? 7 : ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7).AddHours(14);
        db.Campaigns.AddRange(autopay, winback, new Campaign
        {
            Name = "Double Stars Weekend", Channel = "SMS", Goal = "rewards", CapDays = 7, CreatedById = admin.Id,
            Message = "Hi {{first_name}}, this weekend only: redeem stars for gift cards at 2x value in My Account. Reply STOP to opt out.",
            Audience = """{"statuses":["Good - On Flow"]}""", Status = "scheduled", ScheduledAt = saturday,
        });
        await db.SaveChangesAsync();
        async Task Deliver(Campaign c, int n, Action<Customer, int> convert)
        {
            var tpl = c.EmailTemplateId is { } id ? await db.EmailTemplates.FindAsync(id) : null;
            foreach (var (i, cust) in (await Campaigns.AudienceQuery(db, c).OrderBy(x => x.Id).Take(n).ToListAsync()).Index())
            {
                DateTime? opened = c.Channel == "Email" && random.Next(1, 101) <= 46 ? c.SentAt!.Value.AddHours(random.Next(1, 61)) : null;
                db.ContactLogs.Add(new ContactLog
                {
                    CustomerId = cust.Id, CampaignId = c.Id, Channel = c.Channel, Template = c.Channel == "Email" ? tpl?.Name ?? "" : $"Campaign: {c.Name}",
                    Body = c.Channel == "SMS" ? Campaigns.TextFor(c, cust) : null, Phone = c.Channel == "SMS" ? cust.Phone : null, Status = "sent", Who = "System",
                    SentAt = c.SentAt, OpenedAt = opened, ClickedAt = opened != null && random.Next(1, 101) <= 35 ? opened.Value.AddMinutes(random.Next(1, 31)) : null, CreatedAt = c.SentAt!.Value,
                });
                convert(cust, i);
            }
            await db.SaveChangesAsync();
            c.Recipients = await db.ContactLogs.CountAsync(l => l.CampaignId == c.Id);
        }
        await Deliver(autopay, 18, (c, i) => { if (i % 4 == 0) db.CustomerProducts.Add(new CustomerProduct { CustomerId = c.Id, Product = "AutoPay", Author = "My Account", CreatedAt = autopay.SentAt!.Value.AddDays(random.Next(0, 6)) }); });
        await Deliver(winback, 10, (c, i) => { if (i == 1) Term(c, winback.SentAt!.Value.AddDays(3)); });
        await db.SaveChangesAsync();
        void Term(Customer c, DateTime at) => db.PlanTerms.Add(new PlanTerm
        {
            CustomerId = c.Id, PlanId = c.PlanId, RateClass = c.Type == "Residential" ? "Residential" : "Commercial", EnergyCharge = 0.1125m, Rate2000 = 14.9m,
            OrderedAt = at, ContractStart = DateOnly.FromDateTime(at.AddDays(14)), Status = "not started",
        });

        // A test send of the welcome email to the marketing team
        if (await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == "marketing@example.com") is { } marketing && templates.Values.MinBy(t => t.Id) is { } welcome)
            db.EmailTestSends.Add(new EmailTestSend
            {
                EmailTemplateId = welcome.Id, UserId = marketing.Id, Mode = "team", Target = $"{marketing.Role?.Name} team", Status = "logged", CreatedAt = now.AddDays(-1),
                Recipients = JsonSerializer.Serialize(new[] { new { email = marketing.Email, name = marketing.Name, status = "logged" } }),
            });

        // ---- Surveys ----
        await SeedSurveysAsync(db, now);
        // Past 90-day check-in and farewell survey emails: everyone who answered was sent one, plus some who didn't
        foreach (var (template, slug) in new[] { ("Survey - 90 Day Check-In", "90-day-check-in"), ("Survey - Farewell", "farewell") })
        {
            var survey = await db.Surveys.FirstAsync(s => s.Slug == slug);
            var answered = await db.SurveyResponses.Where(r => r.SurveyId == survey.Id && r.CustomerId != null).Select(r => new { r.CustomerId, r.CreatedAt }).ToListAsync();
            var ids = answered.Select(a => a.CustomerId).ToList();
            var more = (await db.Customers.Where(c => c.Email != null && !ids.Contains(c.Id)).Select(c => c.Id).ToListAsync()).OrderBy(_ => random.Next()).Take(14);
            foreach (var (id, at) in answered.Select(a => (a.CustomerId!.Value, a.CreatedAt.AddDays(-random.Next(1, 4)))).Concat(more.Select(id => (id, now.AddDays(-random.Next(1, 41))))))
                db.ContactLogs.Add(new ContactLog
                {
                    CustomerId = id, Channel = "Email", Template = template, Body = "Survey invitation (sample)", Status = "sent", Who = "System",
                    SentAt = at, OpenedAt = random.Next(3) > 0 ? at.AddHours(random.Next(1, 31)) : null, CreatedAt = at,
                });
        }
        await db.SaveChangesAsync();

        // ---- Email tracking on the other emails: most are opened, some clicked ----
        foreach (var log in await db.ContactLogs.Where(l => l.Channel == "Email" && l.CampaignId == null && l.OpenedAt == null && l.Status == "sent").ToListAsync())
        {
            var at = log.SentAt ?? log.CreatedAt;
            if (random.Next(3) > 0) log.OpenedAt = at.AddHours(random.Next(1, 41));
            if (log.OpenedAt != null && random.Next(2) == 0) log.ClickedAt = log.OpenedAt.Value.AddMinutes(random.Next(1, 30));
        }
        await db.SaveChangesAsync();

        // ---- Link clicks for the heat maps: the big button gets the most, then the body's own links, then the logo ----
        var all = await db.EmailTemplates.ToListAsync();
        var byName = all.ToDictionary(t => t.Name);
        var partsById = await db.EmailParts.ToDictionaryAsync(p => p.Id);
        var defaults = (header: partsById.Values.FirstOrDefault(p => p.Kind == "header" && p.IsDefault), footer: partsById.Values.FirstOrDefault(p => p.Kind == "footer" && p.IsDefault));
        var campaignTemplates = await db.Campaigns.ToDictionaryAsync(c => c.Id, c => c.EmailTemplateId);
        string[] devices = ["Phone", "Phone", "Phone", "Desktop", "Desktop", "Tablet"];
        // (a bounced email gets no clicks; a clicked email was opened first)
        foreach (var log in await db.ContactLogs.Where(l => (l.Channel == "Email" || l.Channel == "SMS") && l.DroppedAt == null && (l.OpenedAt != null || l.ClickedAt != null || l.CampaignId != null)).ToListAsync())
        {
            var t = log.CampaignId is { } cid ? (campaignTemplates[cid] is { } tid ? all.FirstOrDefault(x => x.Id == tid) : null) : byName.GetValueOrDefault(log.Template);
            List<(string Key, string Label, string Url)> links = log.Channel == "SMS" ? [("body-1", "Link in the text", "")]
                : t == null ? []
                : [.. Rodeo.Links("header", (t.EmailHeaderId is { } h ? partsById[h] : defaults.header)?.Html ?? ""), .. Rodeo.Links("body", t.Body), .. Rodeo.Links("footer", (t.EmailFooterId is { } f ? partsById[f] : defaults.footer)?.Html ?? "")];
            if (links.Count == 0) continue;
            var weights = links.Select(l =>
            {
                var label = l.Label.ToLowerInvariant();
                return label.Contains("my account") || label.Contains("pay now") || label.Contains("see my rewards") ? 10
                    : l.Key.StartsWith("body-") ? 7 : l.Key.StartsWith("header-") ? 3 : label.Contains("pay") ? 4 : label.Contains("unsubscribe") ? 1 : 2;
            }).ToList();
            var n = log.ClickedAt != null ? random.Next(1, 4) : log.CampaignId != null ? random.Next(0, 4) : random.Next(3) == 0 ? 1 : 0;
            for (var k = 0; k < n; k++)
            {
                var pick = random.Next(1, weights.Sum() + 1);
                var j = 0;
                while ((pick -= weights[j]) > 0) j++;
                db.LinkClicks.Add(new LinkClick
                {
                    ContactLogId = log.Id, CampaignId = log.CampaignId, EmailTemplateId = t?.Id, CustomerId = log.CustomerId, Link = links[j].Key, Url = links[j].Url,
                    Device = devices[random.Next(devices.Length)], ClickedAt = (log.ClickedAt ?? log.OpenedAt ?? log.CreatedAt).AddMinutes(random.Next(0, 91)),
                });
            }
            if (n > 0 && log.ClickedAt == null) log.ClickedAt = (log.OpenedAt ?? log.CreatedAt).AddMinutes(random.Next(1, 61));
            if (log.ClickedAt != null && log.Channel == "Email") log.OpenedAt ??= log.ClickedAt.Value.AddMinutes(-random.Next(1, 10));
        }
        await db.SaveChangesAsync();

        // ---- PDFs: headers and footers, templates, and PDFs made from them for sample accounts ----
        var pdfHeader = new PdfPart { Kind = "header", Name = "Standard (navy)", Title = "{{brand}}", Color = "navy", IsDefault = true };
        var legal = new PdfPart { Kind = "header", Name = "Legal notice (white)", Title = "{{brand}}", Subtitle = "Important notice about your electric service", Color = "white" };
        var promo = new PdfPart { Kind = "header", Name = "Rewards (cyan)", Title = "Rangler Rewards", Subtitle = "From {{brand}}", Color = "cyan" };
        var pdfFooter = new PdfPart { Kind = "footer", Name = "Standard", Body = "{{brand}} · Questions? Call {{phone}} or visit My Account. Sample document for demonstration.", IsDefault = true };
        var legalFooter = new PdfPart { Kind = "footer", Name = "Legal (PUCT)", Body = "{{brand}} · PUCT Certificate #10000 (sample) · Complaints: Public Utility Commission of Texas, 1-888-782-8477 (sample) · Account {{account}}" };
        db.PdfParts.AddRange(pdfHeader, legal, promo, pdfFooter, legalFooter);
        await db.SaveChangesAsync();
        var sample = (await db.Customers.Select(c => c.Id).ToListAsync()).OrderBy(_ => random.Next()).Take(30).ToList();
        int?[] staff = [admin.Id, Uid("marketing@example.com"), Uid("csr@example.com")];
        foreach (var (i, p) in Load("pdf_templates").Index())
        {
            var category = S(p, "category");
            var t = new PdfTemplate
            {
                Name = S(p, "name"), Category = category, Title = S(p, "title"), Body = S(p, "body"),
                PdfHeaderId = category is "Legal" or "Collections" ? legal.Id : category == "Rewards" ? promo.Id : null,
                PdfFooterId = category is "Legal" or "Collections" ? legalFooter.Id : null,
                ShowAddress = category != "Legal", Status = i == 7 ? "Draft" : "Active", CreatedAt = now.AddDays(-(60 - i * 3)), UpdatedAt = now.AddDays(-(20 - i)),
            };
            db.PdfTemplates.Add(t);
            await db.SaveChangesAsync();
            var doc = await PdfTemplates.DocumentAsync(db, t);
            foreach (var cid in i == 7 ? [] : sample.OrderBy(_ => random.Next()).Take(random.Next(2, 7)))
            {
                var account = await PdfTemplates.AccountAsync(db, cid);
                var pdf = PdfTemplates.Build(doc, account);
                db.PdfGenerations.Add(new PdfGeneration
                {
                    PdfTemplateId = t.Id, CustomerId = cid, UserId = staff[random.Next(staff.Length)], FileName = PdfTemplates.FileName(t, account!.Customer), Pages = pdf.PageCount,
                    Size = pdf.Output(t.Title).Length, Title = doc.Title, Body = doc.Body, Footer = doc.Footer, ShowAddress = doc.ShowAddress,
                    Header = doc.Header == null ? null : JsonSerializer.Serialize(doc.Header), CreatedAt = now.AddMinutes(-random.Next(30, 60 * 24 * 30)),
                });
            }
            // A couple of test sends (to fictional addresses)
            if (i < 3) db.PdfTestSends.Add(new PdfTestSend { PdfTemplateId = t.Id, UserId = admin.Id, CustomerId = sample[0], Recipients = "marketing@example.com", Status = "sent", CreatedAt = now.AddDays(-(3 + i)) });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Rodeo → Surveys (ebunny's SurveySeeder): categories, answer sets, the question bank, five surveys and fictional responses.</summary>
    private static async Task SeedSurveysAsync(AppDbContext db, DateTime now)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var cats = new Dictionary<string, SurveyCategory>();
        foreach (var (i, (name, desc, color)) in new[]
        {
            ("Overall Experience", "How customers feel about us overall", "#102247"), ("Customer Service", "Phone, chat and email support", "#00AEEF"),
            ("Billing & Payments", "Bills, payments, AutoPay and due dates", "#EF8A00"), ("Website & My Account", "Signing up online and managing the account", "#5dd3ff"),
            ("Plans & Pricing", "Rates, terms and plan choices", "#137a3a"), ("Getting Started", "Enrollment, switching and the first bill", "#C80611"),
            ("About You", "How customers found us and what they want", "#96999d"),
        }.Index())
            db.SurveyCategories.Add(cats[name] = new SurveyCategory { Name = name, Description = desc, Color = color, Position = i });
        var sets = new Dictionary<string, SurveyAnswerSet>();
        foreach (var (name, group, type, options) in new (string, string, string, string[])[]
        {
            ("Satisfaction (5-point)", "Satisfaction", "scale", ["Very dissatisfied", "Dissatisfied", "Neutral", "Satisfied", "Very satisfied"]),
            ("Agreement (5-point)", "Agreement", "scale", ["Strongly disagree", "Disagree", "Neither agree nor disagree", "Agree", "Strongly agree"]),
            ("Ease (5-point)", "Ease", "scale", ["Very difficult", "Difficult", "Neither", "Easy", "Very easy"]),
            ("Likelihood (5-point)", "Likelihood", "scale", ["Very unlikely", "Unlikely", "Not sure", "Likely", "Very likely"]),
            ("How often", "Frequency", "single", ["Never", "Rarely", "Sometimes", "Often", "Every month"]),
            ("Contact channels", "Channels", "multi", ["Phone", "Email", "Text message", "Website chat", "My Account messages"]),
            ("How you heard about us", "Channels", "single", ["Search engine", "Friend or family", "Ad", "Power to Choose", "Other"]),
            ("What matters most", "Other", "multi", ["Low rate", "Fixed price", "Renewable energy", "Rewards", "Great service", "No deposit"]),
            ("Reasons for leaving", "Other", "single", ["Moving out of the area", "Found a lower rate", "Unhappy with customer service", "Billing problems", "Contract ended", "Other"]),
        })
            db.SurveyAnswerSets.Add(sets[name] = new SurveyAnswerSet { Name = name, Group = group, Type = type, Options = JsonSerializer.Serialize(options) });
        await db.SaveChangesAsync();
        var bank = new Dictionary<string, SurveyBankQuestion>();
        foreach (var (cat, question, type, set, help) in new (string, string, string, string?, string?)[]
        {
            ("Overall Experience", "How likely are you to recommend us to a friend or colleague?", "rating", null, "0 = not at all likely, 10 = extremely likely"),
            ("Overall Experience", "Overall, how satisfied are you with your electricity service?", "scale", "Satisfaction (5-point)", null),
            ("Overall Experience", "How likely are you to renew when your contract ends?", "scale", "Likelihood (5-point)", null),
            ("Customer Service", "How satisfied were you with your last call to customer service?", "scale", "Satisfaction (5-point)", null),
            ("Customer Service", "Was your question answered on the first contact?", "yes_no", null, null),
            ("Customer Service", "How would you like us to contact you?", "multi", "Contact channels", "Pick all that apply"),
            ("Billing & Payments", "My bill is easy to understand.", "scale", "Agreement (5-point)", null),
            ("Billing & Payments", "How easy is it to pay your bill?", "scale", "Ease (5-point)", null),
            ("Billing & Payments", "Do you use AutoPay?", "yes_no", null, null),
            ("Website & My Account", "How easy was it to sign up on our website?", "scale", "Ease (5-point)", null),
            ("Website & My Account", "How often do you sign in to My Account?", "single", "How often", null),
            ("Plans & Pricing", "What matters most when you choose a plan?", "multi", "What matters most", "Pick all that apply"),
            ("Plans & Pricing", "My rate is fair for what I get.", "scale", "Agreement (5-point)", null),
            ("Getting Started", "How easy was it to switch to us?", "scale", "Ease (5-point)", null),
            ("Getting Started", "Did your service start on the date you expected?", "yes_no", null, null),
            ("About You", "How did you hear about us?", "single", "How you heard about us", null),
            ("About You", "Anything we could do better?", "text", null, "Optional"),
            ("Overall Experience", "What is the main reason you left?", "single", "Reasons for leaving", null),
            ("Overall Experience", "How likely are you to come back to us someday?", "scale", "Likelihood (5-point)", null),
            ("Getting Started", "How has your first three months with us been?", "scale", "Satisfaction (5-point)", null),
        })
            db.SurveyBankQuestions.Add(bank[question] = new SurveyBankQuestion { SurveyCategoryId = cats[cat].Id, Question = question, Type = type, SurveyAnswerSetId = set == null ? null : sets[set].Id, Help = help });
        await db.SaveChangesAsync();

        async Task<Survey> Make(Survey s, (string Text, bool Required)[] questions)
        {
            foreach (var (i, (text, required)) in questions.Index())
            {
                var bq = bank[text];
                s.Questions.Add(new SurveyQuestion
                {
                    SurveyCategoryId = bq.SurveyCategoryId, SurveyBankQuestionId = bq.Id, Question = bq.Question, Type = bq.Type, SurveyAnswerSetId = bq.SurveyAnswerSetId,
                    Options = bq.SurveyAnswerSetId is { } sid ? sets.Values.First(x => x.Id == sid).Options : null, Required = required, Help = bq.Help, Position = i,
                });
            }
            db.Surveys.Add(s);
            await db.SaveChangesAsync();
            return s;
        }
        var howAreWe = await Make(new Survey { Title = "How are we doing?", Slug = "how-are-we-doing", Intro = "Two minutes to help us serve you better.", ThankYou = "Thanks for telling us how we’re doing. We read every answer.", OpensOn = today.AddMonths(-3) },
        [
            ("How likely are you to recommend us to a friend or colleague?", true), ("Overall, how satisfied are you with your electricity service?", true),
            ("How satisfied were you with your last call to customer service?", false), ("Was your question answered on the first contact?", false),
            ("My bill is easy to understand.", false), ("How easy is it to pay your bill?", false), ("What matters most when you choose a plan?", false),
            ("How did you hear about us?", false), ("Anything we could do better?", false),
        ]);
        var onboarding = await Make(new Survey { Title = "New Customer Welcome", Slug = "new-customer-welcome", Intro = "You’re a few weeks in. How did getting started go?", ThankYou = "Thanks, and welcome aboard." },
        [
            ("How easy was it to sign up on our website?", true), ("How easy was it to switch to us?", true), ("Did your service start on the date you expected?", true),
            ("How would you like us to contact you?", false), ("Anything we could do better?", false),
        ]);
        await Make(new Survey { Title = "Billing Check-In", Slug = "billing-check-in", Intro = "Help us make bills clearer.", OpensOn = today.AddDays(10), ClosesOn = today.AddDays(40) },
            [("My bill is easy to understand.", true), ("Do you use AutoPay?", false), ("My rate is fair for what I get.", false)]);
        // Sent automatically by the survey emails (Rodeo → Survey Emails)
        var ninety = await Make(new Survey { Title = "90-Day Check-In", Slug = "90-day-check-in", Intro = "You’ve been with us about three months. How’s it going?", ThankYou = "Thanks! We read every answer." },
        [
            ("How has your first three months with us been?", true), ("How likely are you to recommend us to a friend or colleague?", true), ("My bill is easy to understand.", false),
            ("How likely are you to renew when your contract ends?", false), ("Anything we could do better?", false),
        ]);
        var farewell = await Make(new Survey { Title = "Sorry to See You Go", Slug = "farewell", Intro = "Thank you for being a customer. Tell us why you left; it helps us do better.", ThankYou = "Thank you, and good luck. We’d love to have you back someday." },
        [
            ("What is the main reason you left?", true), ("Overall, how satisfied are you with your electricity service?", false),
            ("How likely are you to come back to us someday?", false), ("Anything we could do better?", false),
        ]);

        // Fictional responses with a realistic spread
        string[] comments = ["Faster answers on the phone.", "Text me when my bill is ready.", "Happy so far, the app is easy.", "The renewal letter was confusing.",
            "Love the rewards.", "Rates went up at renewal, not thrilled.", "Chat support was great.", "Wish AutoPay was easier to set up."];
        var customers = await db.Customers.OrderBy(c => c.Id).Take(44).Select(c => c.Id).ToListAsync();
        string Pick(List<string> list, int i, int skew = 0) => list[Math.Min(list.Count - 1, Math.Max(0, (i * 7 + skew) % list.Count))];
        void Respond(Survey s, int? customer, Dictionary<string, object?> answers, string source, DateTime at) => db.SurveyResponses.Add(new SurveyResponse
        {
            SurveyId = s.Id, CustomerId = customer, Answers = JsonSerializer.Serialize(answers.Where(a => a.Value != null).ToDictionary()), Source = source, CreatedAt = at,
        });
        int[] nps = [10, 9, 9, 8, 10, 7, 6, 9, 10, 4, 8, 9, 10, 5, 9, 7, 10];
        int[] scale = [4, 3, 3, 4, 2, 3, 4, 1, 3, 4];
        foreach (var (i, c) in customers.Take(34).Index())
        {
            var answers = new Dictionary<string, object?>();
            foreach (var q in howAreWe.Questions)
            {
                object? a = q.Type switch
                {
                    "rating" => nps[i % 17].ToString(),
                    "scale" => q.Choices()[scale[(i + q.Position) % 10]],
                    "yes_no" => i % 4 != 0 ? "Yes" : "No",
                    "multi" => new[] { Pick(q.Choices(), i), Pick(q.Choices(), i, 3) }.Distinct().ToList(),
                    "single" => Pick(q.Choices(), i),
                    _ => i % 3 != 0 ? null : comments[i / 3 % comments.Length],
                };
                if (a != null && !(!q.Required && i % 9 == 4)) answers[q.Id.ToString()] = a; // a few skip optional questions
            }
            Respond(howAreWe, i % 11 == 10 ? null : c, answers, new[] { "email", "email", "website", "my-account" }[i % 4], now.AddDays(-(2 + i * 2)).AddHours(-i));
        }
        int[] onScale = [4, 3, 4, 2, 3];
        foreach (var (i, c) in customers.Skip(32).Index())
            Respond(onboarding, c, onboarding.Questions.ToDictionary(q => q.Id.ToString(), q => (object?)(q.Type switch
            {
                "scale" => q.Choices()[onScale[(i + q.Position) % 5]],
                "yes_no" => i == 2 ? "No" : "Yes",
                "multi" => new List<string> { Pick(q.Choices(), i), Pick(q.Choices(), i, 2) },
                _ => i == 2 ? "Start date slipped a week; nobody told me." : null,
            })), "email", now.AddDays(-(1 + i * 3)));
        int[] r9 = [9, 10, 8, 7, 9, 10, 6, 9, 8], s9 = [4, 3, 4, 2, 3, 4, 1, 3, 4], p9 = [0, 1, 0, 2, 4, 0, 1, 3, 4];
        string?[] t9 = [null, "Great so far.", null, "The first bill was confusing.", null, null, "Rate went up at renewal.", null, null];
        foreach (var (k, s) in new[] { ninety, farewell }.Index())
            foreach (var (i, c) in customers.Skip(10 * k).Take(9).Index())
                Respond(s, c, s.Questions.ToDictionary(q => q.Id.ToString(), q => (object?)(q.Type switch
                {
                    "rating" => r9[i].ToString(),
                    "scale" => q.Choices()[s9[i]],
                    "single" => q.Choices()[p9[i] % q.Choices().Count],
                    _ => t9[i],
                })), "email", now.AddDays(-(2 + i * 3)));
        await db.SaveChangesAsync();
    }
}
