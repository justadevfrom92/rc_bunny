using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data.Entities;

namespace RcBunny.Data.Seed;

/// <summary>
/// Creates/updates the schema and loads the fictional sample data: a port of ebunny's database/seeders
/// (JSON data in Data/Seed/data is copied from ebunny). Runs on Backend start-up; does nothing when data exists.
/// Split by area across the DbSeeder.*.cs files; Fill.cs finally gives every remaining empty table a few rows.
/// </summary>
public partial class DbSeeder(AppDbContext db, IConfiguration config, ILogger<DbSeeder> log)
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "data");
    private readonly Random rng = new(1219);
    private string hash = "";
    private DateTime now = DateTime.UtcNow;
    private DateOnly today = DateOnly.FromDateTime(DateTime.Today);

    public async Task InitializeAsync()
    {
        if (Database.IsSqlServer(config))
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (await db.Roles.AnyAsync()) return;
        if (!config.GetValue("Seed:SampleData", true)) return;

        log.LogInformation("Empty database: loading sample data");
        hash = Passwords.Hash(config["Seed:DemoPassword"] ?? "password");
        db.ChangeTracker.AutoDetectChangesEnabled = true;

        await SeedCatalog();
        await SeedAccess();
        await SeedCms();
        await SeedCustomers();
        await SeedMarketing();       // email templates, rewards, channels, promos (before account detail uses them)
        await SeedAccounts();        // every section of the Corral account page
        await SeedSurveys();
        await SeedCampaigns();
        await SeedSystem();
        await SeedWalker();
        await SeedSiteTraffic();
        await SeedContactMessages();
        await SeedDeputy();
        await SeedCrm();
        await SeedRanger();
        await SeedHeartbeat();
        await SeedPdfs();
        await SeedRateImports();
        await SeedShop();
        await SeedHistory();
        await SeedNotifications();
        await FillEmptyTables();
        log.LogInformation("Sample data loaded");
    }

    // ---- helpers ------------------------------------------------------------------------------------------
    private static JsonArray Load(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, name + ".json")))!.AsArray();
    private static JsonObject LoadObject(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, name + ".json")))!.AsObject();
    private T Pick<T>(IReadOnlyList<T> list) => list[rng.Next(list.Count)];
    private DateTime Ago(int maxDays, int minDays = 0) => now.AddDays(-rng.Next(minDays, Math.Max(minDays + 1, maxDays))).AddMinutes(-rng.Next(0, 1440));
    private Task<int> UserId(string email) => db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
    private static List<string> Strs(JsonNode? n) => n?.AsArray().Select(x => (string)x!).ToList() ?? [];
    private async Task Save() { await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }

    private async Task SeedCatalog()
    {
        var markets = Load("markets").Select(m => new Market
        {
            Name = (string)m!["name"]!, Short = (string)m["short"]!, Description = (string)m["desc"]!, Region = (string)m["region"]!, Phone = (string?)m["phone"],
        }).ToList();
        db.Markets.AddRange(markets);
        await db.SaveChangesAsync();
        var marketByName = markets.ToDictionary(m => m.Name);

        db.ZipRanges.AddRange(Load("zip_ranges").Select(z => new ZipRange { ZipFrom = (int)z![0]!, ZipTo = (int)z[1]!, MarketId = marketByName[(string)z[2]!].Id }));
        db.TdspFees.AddRange(Load("tdsp_fees").Select(f => new TdspFee
        {
            MarketId = marketByName[(string)f!["market"]!].Id, PerKwh = (decimal)f["perKwh"]!, PerBill = (decimal)f["perBill"]!, EffectiveOn = DateOnly.Parse((string)f["kwhDate"]!),
        }));

        var plans = Load("plans").Select(p => new Plan
        {
            Type = (string)p!["type"]!, Term = (int)p["term"]!, Name = (string)p["name"]!, Internal = (string)p["internal"]!, Slug = (string)p["slug"]!,
            Rolloff = (string)p["rolloff"]!, Etf = (string)p["etf"]!, Mrc = (decimal)p["mrc"]!, Green = (int)p["green"]!, Active = (bool)p["active"]!,
            Tags = Strs(p["tags"]), Perks = Strs(p["perks"]),
        }).ToList();
        db.Plans.AddRange(plans);
        await db.SaveChangesAsync();
        var planByCode = plans.ToDictionary(p => p.Internal);

        foreach (var r in Load("rates"))
            if (planByCode.TryGetValue((string)r!["plan"]!, out var plan))
                db.Rates.Add(new Rate { PlanId = plan.Id, MarketId = marketByName[(string)r["market"]!].Id, Energy = (decimal)r["energy"]!, EffectiveOn = DateOnly.Parse((string)r["effective"]!) });

        foreach (var g in Load("plan_groups"))
        {
            var group = new PlanGroup { Name = (string)g!["name"]!, Slug = (string)g["slug"]! };
            var pos = 0;
            foreach (var code in Strs(g["plans"]))
                if (planByCode.TryGetValue(code, out var plan)) group.Plans.Add(new PlanGroupPlan { PlanId = plan.Id, Position = pos++ });
            db.PlanGroups.Add(group);
        }

        foreach (var row in Load("term_mods"))
        {
            var term = (int)row!["term"]!;
            foreach (var (key, value) in row.AsObject())
                if (key is not ("term" or "etf"))
                    db.TermDiscounts.Add(new TermDiscount { Term = term, Region = key, Discount = (decimal)value! });
            db.TermEtfs.Add(new TermEtf { Term = term, Amount = (int)row["etf"]! });
        }

        var pos2 = 0;
        db.ByopProducts.AddRange(Load("byop_products").Select(p => new ByopProduct
        {
            Key = (string)p!["key"]!, Name = (string)p["name"]!, Model = (string)p["model"]!, Type = (string)p["type"]!, Step = (string)p["step"]!,
            RateAdj = (decimal)p["adj"]!, Monthly = (decimal)p["monthly"]!, Active = (bool)p["active"]!, Description = (string?)p["description"], Position = pos2++,
        }));
        await Save();
    }

    private async Task SeedAccess()
    {
        var roles = Load("roles").Select(r => new Role { Name = (string)r!["name"]!, Perms = Strs(r["perms"]) }).ToList();
        db.Roles.AddRange(roles);
        await db.SaveChangesAsync();
        var role = roles.ToDictionary(r => r.Name, r => r.Id);

        var rows = Load("users");
        var users = new Dictionary<string, User>();
        foreach (var u in rows)
        {
            var user = new User
            {
                Name = (string)u!["name"]!, Email = (string)u["email"]!, RoleId = role[(string)u["role"]!], Active = (bool)u["active"]!,
                EmployeeId = (string?)u["employee_id"], Title = (string?)u["title"], Department = (string?)u["department"], PhoneExt = (string?)u["ext"],
                Location = (string?)u["location"], HiredOn = u["hired"] is { } h ? DateOnly.Parse((string)h!) : null,
                ExtraPerms = Strs(u["extra"]), DeniedPerms = Strs(u["denied"]), AccessNote = (string?)u["note"], Password = hash,
                LastLoginAt = u["last"] is { } l ? DateTime.Parse((string)l!) : null,
            };
            users[user.Email] = user;
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
        foreach (var u in rows)
            if ((string?)u!["manager"] is { } mgr && users.TryGetValue(mgr, out var m))
                users[(string)u["email"]!].ManagerId = m.Id;

        // More staff (ebunny MoreUsersSeeder): in-office reps and managers, and the 3rd-party company Brightline
        var lead = users["lead@example.com"].Id;
        var ends = DateOnly.FromDateTime(DateTime.Today.AddMonths(8));
        (string Name, string Email, string Title, string Role, string? Company, string[] Denied, string? Note)[] more =
        [
            ("Morgan Placeholder", "morgan.placeholder@staff.example.com", "Customer Service Rep", "Customer Service", null, [], null),
            ("Jordan Sampleton", "jordan.sampleton@staff.example.com", "Customer Service Rep", "Customer Service", null, [], null),
            ("Riley Testwood", "riley.testwood@staff.example.com", "Customer Service Rep", "Customer Service", null, [], null),
            ("Quinn Mockridge", "quinn.mockridge@staff.example.com", "Billing Specialist", "Customer Service", null, [], null),
            ("Taylor Demoson", "taylor.demoson@staff.example.com", "Customer Service Supervisor", "Customer Service Lead", null, [], null),
            ("Alex Ortega", "alex.o@brightline.example.net", "Contract Agent", "Customer Service", "Brightline Contact Solutions", ["bounty"], null),
            ("Blair Andrews", "blair.a@brightline.example.net", "Contract Agent", "Customer Service", "Brightline Contact Solutions", ["bounty"], null),
            ("Cameron Wells", "cameron.w@brightline.example.net", "Contract Agent", "Customer Service", "Brightline Contact Solutions", ["bounty"], null),
            ("Devon Shaw", "devon.s@brightline.example.net", "Contract Team Lead", "Customer Service Lead", "Brightline Contact Solutions", ["deputy", "rodeo"], null),
            ("Sam Contractrep", "3p.csr@example.com", "Contract Agent — Tickets", "Customer Service", "Brightline Contact Solutions", ["bounty"], "3rd-party rep: works their own Ranger tickets"),
            ("Reese Shiftlead", "3p.manager@example.com", "Contract Team Manager", "Customer Service Lead", "Brightline Contact Solutions", ["deputy"], "3rd-party manager: assigns, takes over and joins in on tickets"),
            ("Jamie Accountlead", "3p.admin@example.com", "Brightline Account Admin", "Customer Service Lead", "Brightline Contact Solutions", ["deputy"], "3rd-party company admin: runs Brightline's people in Ranger"),
        ];
        var n = 1100;
        foreach (var x in more)
            db.Users.Add(new User
            {
                Name = x.Name, Email = x.Email, Title = x.Title, RoleId = role[x.Role], Company = x.Company, DeniedPerms = [.. x.Denied], AccessNote = x.Note,
                AccessEndsOn = x.Company is null ? null : ends, ManagerId = lead, Password = hash, EmployeeId = x.Company is null ? $"E-{n++}" : null,
                Department = x.Company is null ? "Customer Service" : null, Location = x.Company is null ? "Houston Call Center" : $"Off-site ({x.Company})",
                HiredOn = today.AddDays(-rng.Next(200, 2000)), LastLoginAt = Ago(10),
            });
        db.Users.Add(new User
        {
            Name = "Kai Formerton", Email = "kai.f@brightline.example.net", Title = "Contract Agent", RoleId = role["Customer Service"],
            Company = "Brightline Contact Solutions", AccessEndsOn = today.AddMonths(-2), Active = false, Location = "Off-site (Brightline Contact Solutions)", Password = hash,
        });
        await Save();
    }

    private async Task SeedCustomers()
    {
        var markets = await db.Markets.ToDictionaryAsync(m => m.Name, m => m.Id);
        var plans = await db.Plans.ToDictionaryAsync(p => p.Internal, p => p.Id);
        var activity = Load("customer_activity").ToDictionary(a => (string)a!["account"]!, a => a!);
        var phoneTypes = new[] { "Mobile", "Mobile", "Home", "Work" };
        var customers = new List<Customer>();

        foreach (var c in Load("customers"))
        {
            var name = (string)c!["name"]!;
            var parts = name.Split(' ', 2);
            var created = DateTime.Parse((string)c["created"]!);
            var acct = (string)c["account"]!;
            var cust = new Customer
            {
                Account = acct, Ticket = (string)c["ticket"]!, Name = name, FirstName = parts[0], LastName = parts.ElementAtOrDefault(1),
                Type = (string)c["type"]!, Phone = (string?)c["phone"], Email = (string?)c["email"], Address = (string)c["address"]!, City = (string)c["city"]!,
                Zip = (string)c["zip"]!, MarketId = markets.GetValueOrDefault((string)c["market"]!), Esiid = (string?)c["esiid"],
                PlanId = plans.TryGetValue((string)c["planCode"]!, out var pid) ? pid : null, Status = (string)c["status"]!,
                Exception = string.IsNullOrEmpty((string?)c["exception"]) ? null : (string)c["exception"]!, Source = (string)c["source"]!,
                Balance = (decimal)c["balance"]!, Autopay = (bool)c["autopay"]!, Paperless = (bool)c["paperless"]!, Stars = (int)c["stars"]!,
                CreatedAt = created, UpdatedAt = created, StartDate = DateOnly.FromDateTime(created.AddDays(7)), ActualStart = DateOnly.FromDateTime(created.AddDays(8)),
                RequestedStart = DateOnly.FromDateTime(created.AddDays(7)), DueDate = today.AddDays(rng.Next(3, 25)), PhoneType = Pick(phoneTypes),
                TecScore = rng.Next(540, 820), SsnLast4 = rng.Next(1000, 9999).ToString(), MeterNumber = "M" + rng.Next(10000000, 99999999),
                LoadProfile = "RESLOWR_COAST", LoadZone = "LZ_HOUSTON", MeterType = "AMS", RateClass = "Residential", BillingStreet = (string)c["address"]!,
                BillingCity = (string)c["city"]!, BillingState = "TX", BillingZip = (string)c["zip"]!, Ip = $"203.0.113.{rng.Next(1, 254)}",
                ReferralCode = "REF" + acct[^5..], PeakPerks = rng.Next(4) == 0,
            };
            if (activity.TryGetValue(acct, out var a))
            {
                foreach (var b in a["bills"]!.AsArray())
                {
                    var d = DateOnly.Parse((string)b!["date"]!);
                    cust.Bills.Add(new Bill
                    {
                        Reference = (string)b["id"]! + "-" + acct[^4..], BilledOn = d, DueOn = d.AddDays(16), PeriodStart = d.AddMonths(-1), PeriodEnd = d.AddDays(-1),
                        Kwh = (int)b["kwh"]!, Amount = (decimal)b["amount"]!, Invoice = "INV-" + acct[^6..] + d.ToString("yyMM"),
                    });
                }
                foreach (var p in a["payments"]!.AsArray())
                    cust.Payments.Add(new Payment
                    {
                        Reference = (string)p!["id"]! + "-" + acct[^4..], PaidOn = DateOnly.Parse((string)p["date"]!), Amount = (decimal)p["amount"]!,
                        Method = (string)p["method"]!, Source = (string)p["source"]!, Status = (string)p["status"]!, Confirmation = "CONF" + rng.Next(100000, 999999),
                    });
                foreach (var nn in a["notes"]!.AsArray())
                {
                    var at = DateTime.Parse((string)nn!["at"]!);
                    cust.Notes.Add(new Note { Author = (string)nn["by"]!, Body = (string)nn["text"]!, CreatedAt = at, UpdatedAt = at });
                }
            }
            else
                cust.Notes.Add(new Note { Author = "System", Body = $"Enrollment submitted via {cust.Source}.", CreatedAt = created, UpdatedAt = created });
            customers.Add(cust);
        }

        // My Account sign-ins (ebunny PortalLoginSeeder): the demo customer plus a few more test logins
        var demo = customers.First(c => c.Account == "1219000000");
        demo.Username = "demo.customer";
        demo.Password = hash;
        demo.AuthorizedUsers = new JsonArray(new JsonObject { ["name"] = "Sam Sample", ["relationship"] = "Spouse", ["phone"] = "(555) 555-0190" });
        foreach (var (c, i) in customers.Where(c => c.Status == "Good - On Flow" && c != demo).Take(3).Select((c, i) => (c, i)))
        {
            c.Username = $"test.customer{i + 2}";
            c.Password = hash;
        }

        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();

        foreach (var c in customers.Where(c => c.Status == "Pending - Deposit Due")) c.DepositDue = 150;
        // Exception queues (Corral → Exceptions): a work item per account with an exception, spread over the queues
        var queues = AppConfig.Keys("admin.queues").ToList();
        foreach (var (c, i) in customers.Where(c => c.Exception != null).Select((c, i) => (c, i)))
            db.WorkItems.Add(new WorkItem { Queue = queues[i % queues.Count], CustomerId = c.Id, Summary = c.Exception, CreatedAt = Ago(20) });
        for (var i = 0; i < 24; i++)
            db.WorkItems.Add(new WorkItem
            {
                Queue = queues[(i * 5) % queues.Count], CustomerId = Pick(customers).Id, Summary = "Sample exception",
                CreatedAt = Ago(40), ResolvedAt = i % 3 == 0 ? Ago(5) : null,
            });

        var csr = await UserId("csr@example.com");
        foreach (var c in customers.Take(3)) db.Bookmarks.Add(new Bookmark { UserId = csr, CustomerId = c.Id });
        db.Bookmarks.Add(new Bookmark { UserId = await UserId("admin@example.com"), CustomerId = demo.Id });
        await Save();
    }
}
