using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Seed;

/// <summary>Creates the database on first start and fills it with ebunny's sample data (Seed/data/*.json).</summary>
public static class Seeder
{
    public static async Task RunAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Roles.AnyAsync()) return;

        var password = Passwords.Hash("password");

        // Staff and what each role can do
        var admin = new Role { Name = "Administrator", Permissions = string.Join(",", Permissions.All.Keys) };
        var service = new Role { Name = "Customer Service", Permissions = "corral,messages" };
        var pricing = new Role { Name = "Pricing", Permissions = "plans" };
        var finance = new Role { Name = "Finance", Permissions = "corral,refunds" };
        db.Roles.AddRange(admin, service, pricing, finance);
        db.Users.AddRange(
            new User { Name = "Admin Demo", Email = "admin@example.com", PasswordHash = password, Role = admin },
            new User { Name = "Casey Service", Email = "csr@example.com", PasswordHash = password, Role = service },
            new User { Name = "Pat Pricing", Email = "pricing@example.com", PasswordHash = password, Role = pricing },
            new User { Name = "Frankie Finance", Email = "finance@example.com", PasswordHash = password, Role = finance });

        // Markets with their delivery charges
        var fees = Load("tdsp_fees").ToDictionary(f => S(f, "market"));
        var markets = Load("markets").Select(m => new Market
        {
            Code = S(m, "name"), Name = S(m, "desc"),
            DeliveryCentsPerKwh = fees.TryGetValue(S(m, "name"), out var f) ? D(f, "perKwh") : 0,
            DeliveryPerMonth = fees.TryGetValue(S(m, "name"), out var f2) ? D(f2, "perBill") : 0,
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
        }).ToDictionary(p => p.Code);
        db.Plans.AddRange(plans.Values);
        await db.SaveChangesAsync();
        foreach (var r in Load("rates"))
            if (plans.TryGetValue(S(r, "plan"), out var plan) && markets.TryGetValue(S(r, "market"), out var market))
                db.Rates.Add(new Rate { PlanId = plan.Id, MarketId = market.Id, EnergyCents = D(r, "energy"), EffectiveOn = DateOnly.Parse(S(r, "effective")) });

        // Customers, each with six months of bills and payments
        var today = DateOnly.FromDateTime(DateTime.Today);
        var random = new Random(1219);
        var first = true;
        foreach (var c in Load("customers"))
        {
            var customer = new Customer
            {
                Account = S(c, "account"), Name = S(c, "name"), Email = S(c, "email"), Phone = S(c, "phone"),
                Address = S(c, "address"), City = S(c, "city"), Zip = S(c, "zip"), Type = S(c, "type"), Esiid = S(c, "esiid") is { Length: > 0 } e ? e : null,
                MarketId = markets.TryGetValue(S(c, "market"), out var m) ? m.Id : null,
                PlanId = plans.TryGetValue(S(c, "planCode"), out var p) ? p.Id : null,
                Status = S(c, "status"), Balance = D(c, "balance"), Autopay = c["autopay"]?.GetValue<bool>() ?? false,
                DueDate = today.AddDays(20), CreatedAt = DateTime.Parse(S(c, "created")),
            };
            if (first) { customer.Username = "demo.customer"; customer.PasswordHash = password; first = false; }
            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            for (var i = 6; i >= 1; i--)
            {
                var billed = today.AddMonths(-i + 1).AddDays(-10);
                var kwh = random.Next(700, 1900);
                var amount = Math.Round(kwh * 0.15m, 2);
                db.Bills.Add(new Bill { CustomerId = customer.Id, BilledOn = billed, DueOn = billed.AddDays(21), Kwh = kwh, Amount = i == 1 && customer.Balance > 0 ? customer.Balance : amount });
                if (i > 1) db.Payments.Add(new Payment { CustomerId = customer.Id, PaidOn = billed.AddDays(15), Amount = amount, Method = customer.Autopay ? "AutoPay" : "Card", Source = customer.Autopay ? "AutoPay" : "My Account" });
            }
            db.Notes.Add(new Note { CustomerId = customer.Id, Author = "System", Body = $"Account opened via {S(c, "source")}.", CreatedAt = customer.CreatedAt });

            // Corral: the ERCOT switch request, an exception if the account has one, and a text and a call
            if (customer.Esiid != null)
                db.ErcotTransactions.Add(new ErcotTransaction { CustomerId = customer.Id, Esiid = customer.Esiid, Type = "Switch", Status = customer.Status.StartsWith("Good") ? "Completed" : "Sent", SentAt = customer.CreatedAt });
            if (S(c, "exception") is { Length: > 0 } queue)
                db.WorkItems.Add(new WorkItem { CustomerId = customer.Id, Queue = queue, Summary = $"{queue}: {customer.Name}", CreatedAt = customer.CreatedAt });
            if (customer.Phone is { Length: > 0 } phone && random.Next(3) == 0)
            {
                db.TextMessages.Add(new TextMessage { CustomerId = customer.Id, Phone = phone, Body = $"Energy Texas: your bill of {customer.Balance:C} is ready.", Author = "System", CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 20)) });
                db.PhoneCalls.Add(new PhoneCall { CustomerId = customer.Id, Phone = phone, Direction = "in", Minutes = random.Next(2, 15), Outcome = "Resolved", Notes = "Asked about their bill.", Author = "Casey Service", CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 20)) });
            }
        }

        // CRM
        var partners = new[]
        {
            new Partner { Name = "Lone Star Brokers", Kind = "Broker", Contact = "Dana Ruiz", Email = "dana@lonestarbrokers.example", Phone = "(555) 555-0141", CommissionPercent = 3 },
            new Partner { Name = "Gulf Coast Energy Advisors", Kind = "Broker", Contact = "Lee Park", Email = "lee@gcea.example", Phone = "(555) 555-0142", CommissionPercent = 2.5m },
            new Partner { Name = "Hill Country Realty", Kind = "Partner", Contact = "Morgan Hale", Email = "morgan@hcrealty.example", Phone = "(555) 555-0143" },
        };
        db.Partners.AddRange(partners);
        var adminUser = db.Users.Local.First(u => u.Email == "admin@example.com");
        var stages = new[] { "new", "contacted", "qualified", "proposal", "won", "lost" };
        var kinds = new[] { "Residential", "Small Business", "Commercial", "Multi-site" };
        var sources = new[] { "Website", "Referral", "Broker", "Partner", "Event", "Outbound" };
        var names = new[] { ("Riley Grant", "Grant Dental"), ("Jesse Moreno", "Moreno Tacos"), ("Taylor Kim", null), ("Avery Brooks", "Brooks Storage"), ("Quinn Patel", "Patel Motels"), ("Drew Larson", null), ("Sky Nguyen", "Nguyen Nails"), ("Rowan Diaz", "Diaz Machine Shop"), ("Emerson Cole", null), ("Parker Shaw", "Shaw Offices") };
        for (var i = 0; i < names.Length; i++)
        {
            var stage = stages[i % stages.Length];
            var lead = new Lead
            {
                Name = names[i].Item1, Company = names[i].Item2, Email = names[i].Item1.ToLower().Replace(' ', '.') + "@example.com", Phone = $"(555) 555-02{i:00}",
                Kind = names[i].Item2 is null ? "Residential" : kinds[1 + i % 3], Source = sources[i % sources.Length], Stage = stage,
                MonthlyKwh = names[i].Item2 is null ? 1200 : 4000 + i * 1500, Partner = i % 3 == 0 ? partners[i % 2] : null, Owner = adminUser,
                LostReason = stage == "lost" ? "Price" : null, CreatedAt = DateTime.UtcNow.AddDays(-30 + i), ClosedAt = stage is "won" or "lost" ? DateTime.UtcNow.AddDays(-i) : null,
            };
            db.Leads.Add(lead);
            if (stage is not ("won" or "lost"))
                db.FollowUps.Add(new FollowUp { Lead = lead, DueOn = today.AddDays(i - 3), Task = stage == "proposal" ? "Check if they got the proposal" : "Call to follow up" });
        }

        // Products and product orders
        var products = new[]
        {
            new Product { Sku = "ECO-T1", Name = "ecobee Smart Thermostat", Category = "Thermostats", Price = 189.99m, Stock = 40, Description = "Wi-Fi thermostat that works with Peak Perks." },
            new Product { Sku = "LED-6", Name = "LED Bulb 6-Pack", Category = "Lighting", Price = 14.99m, Stock = 300 },
            new Product { Sku = "PLUG-2", Name = "Smart Plug 2-Pack", Category = "Smart Home", Price = 24.99m, Stock = 120 },
            new Product { Sku = "SURGE-1", Name = "Whole-Home Surge Protector", Category = "Protection", Price = 249.00m, Stock = 15 },
            new Product { Sku = "FAN-1", Name = "Ceiling Fan, Energy Star", Category = "Cooling", Price = 129.00m, Stock = 25 },
            new Product { Sku = "STRIP-1", Name = "Smart Power Strip", Category = "Smart Home", Price = 34.99m, Stock = 0, Active = false },
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        var someCustomers = await db.Customers.OrderBy(c => c.Id).Take(8).ToListAsync();
        var statuses = new[] { "New", "Shipped", "Delivered", "Delivered", "Cancelled" };
        for (var i = 0; i < someCustomers.Count; i++)
        {
            var product = products[i % 5];
            var qty = 1 + i % 2;
            db.ProductOrders.Add(new ProductOrder { Number = $"PO-{1001 + i}", CustomerId = someCustomers[i].Id, ProductId = product.Id, Quantity = qty, Total = product.Price * qty, Status = statuses[i % statuses.Length], CreatedAt = DateTime.UtcNow.AddDays(-i * 3) });
        }

        db.ContactMessages.AddRange(
            new ContactMessage { Name = "Jordan Lee", Email = "jordan@example.com", Topic = "Billing", Message = "Can I change my due date?" },
            new ContactMessage { Name = "Sam Ortiz", Email = "sam@example.com", Phone = "(555) 555-0199", Topic = "New service", Message = "Moving to Dallas next month, which plan should I pick?" });

        await db.SaveChangesAsync();
    }

    private static IEnumerable<JsonNode> Load(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Seed", "data", name + ".json")))!.AsArray().Select(n => n!);

    private static string S(JsonNode? n, string key) => n?[key]?.ToString() ?? "";

    private static decimal D(JsonNode? n, string key) =>
        n?[key] is JsonValue v && v.TryGetValue<decimal>(out var d) ? d : decimal.TryParse(n?[key]?.ToString(), out var p) ? p : 0;
}
