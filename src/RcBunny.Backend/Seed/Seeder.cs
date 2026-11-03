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
        var service = new Role { Name = "Customer Service", Permissions = "customers,messages" };
        var pricing = new Role { Name = "Pricing", Permissions = "plans" };
        var finance = new Role { Name = "Finance", Permissions = "customers,refunds" };
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
                Address = S(c, "address"), City = S(c, "city"), Zip = S(c, "zip"),
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
