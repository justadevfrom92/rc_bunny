using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data.Entities;

namespace RcBunny.Data.Seed;

/// <summary>
/// Creates/updates the schema and loads the fictional sample data from Data/Seed/data/*.json
/// (copied from ebunny's database/seeders/data). Runs on Backend start-up; does nothing when data exists.
/// </summary>
public class DbSeeder(AppDbContext db, IConfiguration config, ILogger<DbSeeder> log)
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "data");

    public async Task InitializeAsync()
    {
        if (Database.IsSqlServer(config))
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (await db.Roles.AnyAsync()) return;
        if (!config.GetValue("Seed:SampleData", true)) return;

        log.LogInformation("Empty database: loading sample data");
        var password = config["Seed:DemoPassword"] ?? "password";
        var hash = Passwords.Hash(password);

        await SeedCatalog();
        await SeedAccess(hash);
        await SeedCustomers(hash);
        log.LogInformation("Sample data loaded");
    }

    private static JsonArray Load(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, name + ".json")))!.AsArray();

    private async Task SeedCatalog()
    {
        var markets = Load("markets").Select(m => new Market
        {
            Name = (string)m!["name"]!, Short = (string)m["short"]!, Description = (string)m["desc"]!,
            Region = (string)m["region"]!, Phone = (string?)m["phone"],
        }).ToList();
        db.Markets.AddRange(markets);
        await db.SaveChangesAsync();
        var marketByName = markets.ToDictionary(m => m.Name);

        db.ZipRanges.AddRange(Load("zip_ranges").Select(z => new ZipRange
        {
            ZipFrom = (int)z![0]!, ZipTo = (int)z[1]!, MarketId = marketByName[(string)z[2]!].Id,
        }));
        db.TdspFees.AddRange(Load("tdsp_fees").Select(f => new TdspFee
        {
            MarketId = marketByName[(string)f!["market"]!].Id, PerKwh = (decimal)f["perKwh"]!, PerBill = (decimal)f["perBill"]!,
            EffectiveOn = DateOnly.Parse((string)f["kwhDate"]!),
        }));

        var plans = Load("plans").Select(p => new Plan
        {
            Type = (string)p!["type"]!, Term = (int)p["term"]!, Name = (string)p["name"]!, Internal = (string)p["internal"]!,
            Slug = (string)p["slug"]!, Rolloff = (string)p["rolloff"]!, Etf = (string)p["etf"]!, Mrc = (decimal)p["mrc"]!,
            Green = (int)p["green"]!, Active = (bool)p["active"]!,
            Tags = p["tags"]!.AsArray().Select(t => (string)t!).ToList(),
            Perks = p["perks"]!.AsArray().Select(t => (string)t!).ToList(),
        }).ToList();
        db.Plans.AddRange(plans);
        await db.SaveChangesAsync();
        var planByCode = plans.ToDictionary(p => p.Internal);

        foreach (var r in Load("rates"))
        {
            if (!planByCode.TryGetValue((string)r!["plan"]!, out var plan)) continue;
            db.Rates.Add(new Rate
            {
                PlanId = plan.Id, MarketId = marketByName[(string)r["market"]!].Id, Energy = (decimal)r["energy"]!,
                EffectiveOn = DateOnly.Parse((string)r["effective"]!),
            });
        }

        foreach (var g in Load("plan_groups"))
        {
            var group = new PlanGroup { Name = (string)g!["name"]!, Slug = (string)g["slug"]! };
            var pos = 0;
            foreach (var code in g["plans"]!.AsArray().Select(c => (string)c!))
                if (planByCode.TryGetValue(code, out var plan))
                    group.Plans.Add(new PlanGroupPlan { PlanId = plan.Id, Position = pos++ });
            db.PlanGroups.Add(group);
        }

        foreach (var row in Load("term_mods"))
        {
            var term = (int)row!["term"]!;
            foreach (var (key, value) in row.AsObject())
            {
                if (key is "term" or "etf") continue;
                db.TermDiscounts.Add(new TermDiscount { Term = term, Region = key, Discount = (decimal)value! });
            }
            db.TermEtfs.Add(new TermEtf { Term = term, Amount = (int)row["etf"]! });
        }

        var pos2 = 0;
        db.ByopProducts.AddRange(Load("byop_products").Select(p => new ByopProduct
        {
            Key = (string)p!["key"]!, Name = (string)p["name"]!, Model = (string)p["model"]!, Type = (string)p["type"]!,
            Step = (string)p["step"]!, RateAdj = (decimal)p["adj"]!, Monthly = (decimal)p["monthly"]!, Active = (bool)p["active"]!,
            Description = (string?)p["description"], Position = pos2++,
        }));
        await db.SaveChangesAsync();
    }

    private async Task SeedAccess(string hash)
    {
        var roles = Load("roles").Select(r => new Role
        {
            Name = (string)r!["name"]!, Perms = r["perms"]!.AsArray().Select(p => (string)p!).ToList(),
        }).ToList();
        db.Roles.AddRange(roles);
        await db.SaveChangesAsync();
        var role = roles.ToDictionary(r => r.Name);

        var rows = Load("users");
        var users = new Dictionary<string, User>();
        foreach (var u in rows)
        {
            var user = new User
            {
                Name = (string)u!["name"]!, Email = (string)u["email"]!, RoleId = role[(string)u["role"]!].Id,
                Active = (bool)u["active"]!, EmployeeId = (string?)u["employee_id"], Title = (string?)u["title"],
                Department = (string?)u["department"], PhoneExt = (string?)u["ext"], Location = (string?)u["location"],
                HiredOn = u["hired"] is { } h ? DateOnly.Parse((string)h!) : null,
                ExtraPerms = u["extra"]!.AsArray().Select(p => (string)p!).ToList(),
                DeniedPerms = u["denied"]!.AsArray().Select(p => (string)p!).ToList(),
                AccessNote = (string?)u["note"], Password = hash,
                LastLoginAt = u["last"] is { } l ? DateTime.Parse((string)l!) : null,
            };
            users[user.Email] = user;
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
        foreach (var u in rows)
            if ((string?)u!["manager"] is { } mgr && users.TryGetValue(mgr, out var m))
                users[(string)u["email"]!].ManagerId = m.Id;

        // 3rd-party company people (Brightline): a rep, their manager, the company's admin, and one whose contract ended.
        var ends = DateOnly.FromDateTime(DateTime.Today.AddMonths(8));
        var lead = users["lead@example.com"].Id;
        (string Name, string Email, string Title, string Role, string[] Denied, string Note)[] thirdParty =
        [
            ("Sam Contractrep", "3p.csr@example.com", "Contract Agent — Tickets", "Customer Service", ["bounty"], "3rd-party rep: works their own tickets"),
            ("Reese Shiftlead", "3p.manager@example.com", "Contract Team Manager", "Customer Service Lead", ["deputy"], "3rd-party manager: assigns, takes over and joins in on tickets"),
            ("Jamie Accountlead", "3p.admin@example.com", "Brightline Account Admin", "Customer Service Lead", ["deputy"], "3rd-party company admin: runs Brightline's people"),
        ];
        foreach (var t in thirdParty)
            db.Users.Add(new User
            {
                Name = t.Name, Email = t.Email, Title = t.Title, RoleId = role[t.Role].Id, DeniedPerms = [.. t.Denied], AccessNote = t.Note,
                Company = "Brightline Contact Solutions", AccessEndsOn = ends, ManagerId = lead,
                Location = "Off-site (Brightline Contact Solutions)", Password = hash,
            });
        db.Users.Add(new User
        {
            Name = "Kai Formerton", Email = "kai.f@brightline.example.net", Title = "Contract Agent", RoleId = role["Customer Service"].Id,
            Company = "Brightline Contact Solutions", AccessEndsOn = DateOnly.FromDateTime(DateTime.Today.AddMonths(-2)), Active = false,
            Location = "Off-site (Brightline Contact Solutions)", Password = hash,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedCustomers(string hash)
    {
        var markets = await db.Markets.AsTracking().ToDictionaryAsync(m => m.Name);
        var plans = await db.Plans.ToDictionaryAsync(p => p.Internal);
        var rng = new Random(1219);
        var methods = new[] { "Visa", "Mastercard", "Bank Account", "Amex", "Check" };
        var customers = new List<Customer>();

        foreach (var c in Load("customers"))
        {
            var name = (string)c!["name"]!;
            var parts = name.Split(' ', 2);
            var created = DateTime.Parse((string)c["created"]!);
            var cust = new Customer
            {
                Account = (string)c["account"]!, Ticket = (string)c["ticket"]!, Name = name, FirstName = parts[0],
                LastName = parts.ElementAtOrDefault(1), Type = (string)c["type"]!, Phone = (string?)c["phone"], Email = (string?)c["email"],
                Address = (string)c["address"]!, City = (string)c["city"]!, Zip = (string)c["zip"]!,
                MarketId = markets.GetValueOrDefault((string)c["market"]!)?.Id, Esiid = (string?)c["esiid"],
                PlanId = plans.GetValueOrDefault((string)c["planCode"]!)?.Id, Status = (string)c["status"]!,
                Exception = string.IsNullOrEmpty((string?)c["exception"]) ? null : (string)c["exception"]!,
                Source = (string)c["source"]!, Balance = (decimal)c["balance"]!, Autopay = (bool)c["autopay"]!,
                Paperless = (bool)c["paperless"]!, Stars = (int)c["stars"]!, CreatedAt = created, UpdatedAt = created,
                StartDate = DateOnly.FromDateTime(created.AddDays(7)), DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(12)),
            };

            // A year of bills and payments for accounts that are (or were) flowing
            if (cust.Status is "Good - On Flow" or "Dropped - Churned")
            {
                var months = Math.Min(12, Math.Max(2, (int)((DateTime.Today - created).TotalDays / 30)));
                for (var i = months; i >= 1; i--)
                {
                    var billed = DateOnly.FromDateTime(DateTime.Today.AddMonths(-i));
                    var kwh = 600 + rng.Next(1400);
                    var amount = Math.Round(kwh * 0.142m + 9.95m, 2);
                    var refNo = $"{cust.Account[^6..]}{billed:yyMM}";
                    cust.Bills.Add(new Bill { Reference = "B" + refNo, BilledOn = billed, DueOn = billed.AddDays(16), Kwh = kwh, Amount = amount, Invoice = "INV-" + refNo });
                    if (i > 1 || rng.Next(3) > 0)
                        cust.Payments.Add(new Payment
                        {
                            Reference = "P" + refNo, PaidOn = billed.AddDays(rng.Next(3, 15)), Amount = amount,
                            Method = methods[rng.Next(methods.Length)], Source = cust.Autopay ? "AutoPay" : "My Account",
                            Status = rng.Next(20) == 0 ? "Failed" : "Success", Confirmation = "CONF" + rng.Next(100000, 999999),
                        });
                }
            }
            cust.Notes.Add(new Note { Author = "System", Body = $"Order received from {cust.Source}.", Disposition = "New Enrollment", Category = "Order", CreatedAt = created });
            if (cust.Exception != null)
                cust.Notes.Add(new Note { Author = "System", Body = $"Exception raised: {cust.Exception}.", Priority = "High", CreatedAt = created.AddDays(1) });
            customers.Add(cust);
        }

        // My Account demo sign-in (website): demo.customer / the demo password, on account 1219000000
        var demo = customers.First(c => c.Account == "1219000000");
        demo.Username = "demo.customer";
        demo.Password = hash;

        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();

        // A few bookmarks so Corral's Bookmarks panel isn't empty
        var csr = await db.Users.FirstAsync(u => u.Email == "csr@example.com");
        foreach (var c in customers.Take(3))
            db.Bookmarks.Add(new Bookmark { UserId = csr.Id, CustomerId = c.Id });
        await db.SaveChangesAsync();
    }
}
