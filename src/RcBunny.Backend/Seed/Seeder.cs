using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Seed;

/// <summary>Creates the database on first start and fills it with ebunny's sample data (Seed/data/*.json).</summary>
public static partial class Seeder
{
    public static async Task RunAsync(AppDbContext db, string storageRoot)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Roles.AnyAsync()) return;
        db.TrackHistory = false; // the logs start empty

        var password = Passwords.Hash("password");

        // Staff and what each role can do: ebunny's roles and staff (Seed/data/roles.json, users.json). ebunny's Astro is
        // our "plans" permission; its apps we don't have yet (Rodeo, Deputy) are left out.
        string Perms(JsonNode? list) => string.Join(",", (list?.AsArray() ?? []).Select(p => p!.ToString() == "astro" ? "plans" : p!.ToString())
            .Where(Permissions.All.ContainsKey).Distinct());
        var roles = Load("roles").Select(r => new Role { Name = S(r, "name"), Permissions = Perms(r["perms"]) }).ToDictionary(r => r.Name);
        roles["Administrator"].Permissions = string.Join(",", Permissions.All.Keys); // every right, delete included
        db.Roles.AddRange(roles.Values);
        var staff = Load("users").ToList();
        var users = staff.Select(u => new User
        {
            Name = S(u, "name"), Email = S(u, "email"), PasswordHash = password, Role = roles[S(u, "role")], Active = u["active"]!.GetValue<bool>(),
            EmployeeId = u["employee_id"]?.ToString(), Title = u["title"]?.ToString(), Department = u["department"]?.ToString(),
            PhoneExt = u["ext"]?.ToString(), Location = u["location"]?.ToString(), HiredOn = u["hired"] is { } h ? DateOnly.Parse(h.ToString()) : null,
            ExtraPermissions = Perms(u["extra"]), DeniedPermissions = Perms(u["denied"]), AccessNote = u["note"]?.ToString(),
            LastLoginAt = u["last"] is { } l ? DateTime.Parse(l.ToString()).ToUniversalTime() : null,
        }).ToDictionary(u => u.Email);
        db.Users.AddRange(users.Values);
        await db.SaveChangesAsync();
        foreach (var u in staff) // managers once everyone exists
            if (u["manager"]?.ToString() is { } m && users.TryGetValue(m, out var manager)) users[S(u, "email")].ManagerId = manager.Id;
        var service = roles["Customer Service"];

        // Markets with their delivery charges
        var fees = Load("tdsp_fees").ToDictionary(f => S(f, "market"));
        var markets = Load("markets").Select(m => new Market
        {
            Code = S(m, "name"), Name = S(m, "desc"),
            DeliveryCentsPerKwh = fees.TryGetValue(S(m, "name"), out var f) ? D(f, "perKwh") : 0,
            DeliveryPerMonth = fees.TryGetValue(S(m, "name"), out var f2) ? D(f2, "perBill") : 0,
            DeliveryEffectiveOn = fees.TryGetValue(S(m, "name"), out var f3) ? DateOnly.Parse(S(f3, "kwhDate")) : null,
            Short = S(m, "short"), Region = S(m, "region"), Phone = S(m, "phone"),
        }).ToDictionary(m => m.Code);
        db.Markets.AddRange(markets.Values);
        await db.SaveChangesAsync();

        foreach (var z in Load("zip_ranges"))
            db.ZipRanges.Add(new ZipRange { From = z[0]!.GetValue<int>(), To = z[1]!.GetValue<int>(), MarketId = markets[z[2]!.ToString()].Id });

        // Plans and rates
        var plans = Load("plans").Select(p => new Plan
        {
            Code = S(p, "internal"), Name = S(p, "name"), Type = S(p, "type") == "Biz" ? "Business" : "Residential",
            TermMonths = p["term"]!.GetValue<int>(), MonthlyFee = D(p, "mrc"), GreenPercent = p["green"]!.GetValue<int>(), Active = p["active"]!.GetValue<bool>(),
            Rolloff = S(p, "rolloff"), Etf = S(p, "etf"), Slug = S(p, "slug"),
            Tags = string.Join(",", p["tags"]!.AsArray().Select(t => t!.ToString())), Perks = string.Join("\n", p["perks"]!.AsArray().Select(t => t!.ToString())),
        }).ToDictionary(p => p.Code);
        db.Plans.AddRange(plans.Values);
        await db.SaveChangesAsync();
        foreach (var r in Load("rates"))
            if (plans.TryGetValue(S(r, "plan"), out var plan) && markets.TryGetValue(S(r, "market"), out var market))
                db.Rates.Add(new Rate { PlanId = plan.Id, MarketId = market.Id, EnergyCents = D(r, "energy"), EffectiveOn = DateOnly.Parse(S(r, "effective")) });

        // What the website's plan pages and Build Your Own Plan read: plan groups, term discounts and ETFs, add-ons
        foreach (var g in Load("plan_groups"))
            db.PlanGroups.Add(new PlanGroup { Name = S(g, "name"), Slug = S(g, "slug"), PlanCodes = string.Join(",", g["plans"]!.AsArray().Select(c => c!.ToString())) });
        var regions = Load("regions").Select(r => r!.ToString()).ToList();
        foreach (var row in Load("term_mods"))
        {
            var term = row["term"]!.GetValue<int>();
            foreach (var region in regions) db.TermDiscounts.Add(new TermDiscount { Term = term, Region = region, Discount = D(row, region) });
            db.TermEtfs.Add(new TermEtf { Term = term, Amount = D(row, "etf") });
        }
        var position = 0;
        foreach (var b in Load("byop_products"))
            db.ByopProducts.Add(new ByopProduct
            {
                Key = S(b, "key"), Name = S(b, "name"), Model = S(b, "model"), Type = S(b, "type"), RateAdj = D(b, "adj"), Monthly = D(b, "monthly"),
                Step = S(b, "step"), Active = b["active"]!.GetValue<bool>(), Description = b["description"]?.ToString(), Position = position++,
            });

        // Customers with everything on their accounts, then CRM and the Shop (Seed/CorralSeed.cs)
        var today = DateOnly.FromDateTime(DateTime.Today);
        await db.SaveChangesAsync();
        await SeedCustomersAsync(db, markets, plans, password);
        await SeedPortalLoginsAsync(db, password);
        var adminUser = await db.Users.FirstAsync(u => u.Email == "admin@example.com");
        await SeedCrmAsync(db, adminUser, markets);
        await SeedShopAsync(db);

        // Ranger: the ticket team (in-office and a 3rd-party company), tickets, and website messages (Seed/RangerSeed.cs)
        await SeedRangerAsync(db, adminUser, await db.Users.FirstAsync(u => u.Email == "csr@example.com"), service, password, storageRoot);

        // Lando: the website's pages, content blocks, visits and cookie choices (Seed/LandoSeed.cs)
        await SeedLandoAsync(db, adminUser);

        // Sheriff: the website's maintenance switch
        db.HeartbeatControls.Add(new HeartbeatControl { Key = "website", Name = "Website" });

        // Caboose: MSIDs, promo codes, deposits, credit balances, and some requests waiting for approval
        db.Msids.AddRange(
            new Msid { Code = "1001", Name = "Organic", Channel = "Web" },
            new Msid { Code = "130001", Name = "Epicenter mailer", Channel = "Direct mail" },
            new Msid { Code = "2050", Name = "Lone Star Brokers", Channel = "Broker" },
            new Msid { Code = "3100", Name = "Spring search ads", Channel = "Paid search" });
        db.PromoCodes.AddRange(
            new PromoCode { Code = "HOWDY50", Description = "$50 bill credit for new customers", Credit = 50, StartsOn = today.AddMonths(-3), EndsOn = today.AddMonths(3) },
            new PromoCode { Code = "REFER25", Description = "$25 for referring a friend", Credit = 25 },
            new PromoCode { Code = "SUMMER24", Description = "Last summer's offer", Credit = 30, EndsOn = today.AddMonths(-4), Active = false });
        var money = await db.Customers.OrderBy(c => c.Id).Skip(10).Take(12).ToListAsync();
        var msidCodes = new[] { "1001", "130001", "2050", "3100" };
        for (var i = 0; i < money.Count; i++)
        {
            var c = money[i];
            c.Msid = msidCodes[i % 4];
            if (i % 3 == 0) c.PromoCode = "HOWDY50";
            if (i < 3) c.DepositDue = 100 + 50 * i;
            if (i is >= 3 and < 6) c.DepositHeld = 150;
            if (i is 6 or 7) c.Balance = -42.17m * (i - 5);                 // credit balances, ready to refund
            if (i is 8 or 9) { c.Balance = 180 + i * 20; c.DueDate = today.AddDays(-25 * (i - 7)); } // past due
        }
        db.LedgerEntries.AddRange(
            new LedgerEntry { CustomerId = money[0].Id, Kind = "credit", Amount = 25, Description = "Goodwill credit for the outage", RequestedBy = "Casey Service" },
            new LedgerEntry { CustomerId = money[1].Id, Kind = "debit", Amount = 15, Description = "Returned payment fee", RequestedBy = "Casey Service" });
        db.Refunds.Add(new Refund { CustomerId = money[6].Id, Kind = "balance", Amount = 42.17m, Reason = "Moved out, closing the account", RequestedBy = "Casey Service" });

        // Bounty: offers, earning rules, star history, a couple of redemptions to send, and the monthly drawing
        var offers = new[]
        {
            new RewardOffer { Name = "$10 bill credit", Description = "Takes $10 off your next bill.", Stars = 1000, Effect = "credit", Value = "10", Position = 1 },
            new RewardOffer { Name = "$25 e-gift card", Description = "Emailed to you within a week.", Stars = 2500, Effect = "gift", Value = "$25 e-gift card", Position = 2 },
            new RewardOffer { Name = "Monthly drawing entry", Description = "One entry in this month's drawing for a $500 bill credit.", Stars = 100, Effect = "drawing", Position = 3 },
            new RewardOffer { Name = "LED bulb 6-pack", Description = "Shipped to your service address.", Stars = 800, Effect = "product", Value = "LED-6", Position = 4 },
        };
        db.RewardOffers.AddRange(offers);
        db.RewardRules.AddRange(
            new RewardRule { Event = "bill_paid", Name = "Pay a bill", Stars = 100 },
            new RewardRule { Name = "Refer a friend", Stars = 1000 },
            new RewardRule { Name = "Account anniversary", Stars = 500 },
            new RewardRule { Name = "Go paperless", Stars = 250 });
        await db.SaveChangesAsync();
        var members = await db.Customers.Where(c => c.Stars > 0).OrderByDescending(c => c.Stars).Take(10).ToListAsync();
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        for (var i = 0; i < members.Count; i++)
        {
            var c = members[i];
            db.StarEntries.Add(new StarEntry { CustomerId = c.Id, Stars = c.Stars, Reason = "Stars carried over from ebunny", By = "System", CreatedAt = c.CreatedAt });
            if (i < 6) db.DrawingEntries.Add(new DrawingEntry { CustomerId = c.Id, Month = month });
        }
        db.StarEntries.Add(new StarEntry { CustomerId = members[0].Id, Stars = -2500, Reason = "Redeemed: $25 e-gift card", Offer = offers[1], Fulfillment = "pending", By = "My Account" });
        db.StarEntries.Add(new StarEntry { CustomerId = members[1].Id, Stars = -800, Reason = "Redeemed: LED bulb 6-pack", Offer = offers[3], Fulfillment = "pending", By = "My Account" });
        members[0].Stars -= 2500; members[1].Stars -= 800;
        db.DrawingWinners.Add(new DrawingWinner { Month = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM"), CustomerId = members[2].Id, Prize = "$500 bill credit", DrawnBy = "Admin Demo", DrawnAt = DateTime.UtcNow.AddDays(-20) });

        await db.SaveChangesAsync();

        // Walker: report runs (run last, so the sample reports see everything), files, ETL feed runs and saved reports (Seed/WalkerSeed.cs)
        await SeedWalkerAsync(db, await db.Users.FirstAsync(u => u.Email == "admin@example.com"), storageRoot);
    }

    /// <summary>
    /// My Account logins for sample customers in different situations, for the Test Sign-Ins (password "password"),
    /// the same as ebunny's PortalLoginSeeder.
    /// </summary>
    private static async Task SeedPortalLoginsAsync(AppDbContext db, string password)
    {
        var customers = await db.Customers.Where(c => c.Email != null).OrderBy(c => c.Id).ToListAsync();
        var picks = new (string Username, Func<Customer, bool> Where)[]
        {
            ("test.business", c => c.Type == "Small Business"),
            ("test.pastdue", c => c.Balance > 50 && c.Status == "Good - On Flow"),
            ("test.credit", c => c.Balance < 0),
            ("test.deposit", c => c.Status == "Pending - Deposit Due"),
            ("test.moved", c => c.Status is "Dropped - Churned" or "Moved Out" or "Disconnected"),
        };
        foreach (var (username, where) in picks)
            if (customers.FirstOrDefault(c => c.PasswordHash == null && where(c)) is { } c)
                (c.Username, c.PasswordHash) = (username, password);
        await db.SaveChangesAsync();
    }

    private static IEnumerable<JsonNode> Load(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", "data", name + ".json")))!.AsArray().Select(n => n!);

    private static string S(JsonNode? n, string key) => n?[key]?.ToString() ?? "";

    private static decimal D(JsonNode? n, string key) =>
        n?[key] is JsonValue v && v.TryGetValue<decimal>(out var d) ? d : decimal.TryParse(n?[key]?.ToString(), out var p) ? p : 0;
}
