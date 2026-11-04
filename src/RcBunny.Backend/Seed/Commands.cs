using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Seed;

/// <summary>
/// Database commands, like Laravel's artisan. Run them from src/RcBunny.Backend:
///   dotnet run -- migrate          create the database and fill it with the sample data (if it's empty)
///   dotnet run -- migrate:fresh    drop the database, create it again and fill it (like php artisan migrate:fresh --seed)
///   dotnet run -- db:show          list the plans and rates, how many rows each table has, and the sample sign-ins
/// Both migrate commands finish by showing the same list as db:show.
/// </summary>
public static class Commands
{
    public static readonly string[] Names = ["migrate", "migrate:fresh", "db:show"];

    public static async Task<int> RunAsync(string command, AppDbContext db, string storageRoot)
    {
        switch (command)
        {
            case "migrate:fresh":
                Console.WriteLine("Dropping the database…");
                await db.Database.EnsureDeletedAsync();
                var ranger = Path.Combine(storageRoot, "ranger");
                if (Directory.Exists(ranger)) Directory.Delete(ranger, recursive: true); // the seeded ticket attachments
                goto case "migrate";
            case "migrate":
                Console.WriteLine("Creating the tables and adding the sample data…");
                await Seeder.RunAsync(db, storageRoot);
                Console.WriteLine("Done.");
                Console.WriteLine();
                await ShowAsync(db);
                return 0;
            case "db:show":
                if (!await db.Database.CanConnectAsync()) { Console.WriteLine("There's no database yet. Run: dotnet run -- migrate"); return 1; }
                await ShowAsync(db);
                return 0;
            default:
                Console.WriteLine($"Unknown command {command}. Commands: {string.Join(", ", Names)}");
                return 1;
        }
    }

    private static async Task ShowAsync(AppDbContext db)
    {
        var provider = db.Database.ProviderName?.Split('.').Last();
        Console.WriteLine($"Database: {provider} · {db.Database.GetDbConnection().DataSource}");
        Console.WriteLine();

        // Plans and today's rate in each market (cents per kWh, energy charge only)
        var markets = await db.Markets.OrderBy(m => m.Id).ToListAsync();
        var plans = await db.Plans.OrderBy(p => p.Type).ThenBy(p => p.TermMonths).ThenBy(p => p.Name).ToListAsync();
        var rates = await db.Rates.ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        decimal? RateFor(int plan, int market) => Pricing.CurrentRate(rates.Where(r => r.PlanId == plan && r.MarketId == market), today)?.EnergyCents;

        Console.WriteLine($"PLANS ({plans.Count}) · energy charge in ¢/kWh by market");
        var header = $"  {"Code",-10} {"Name",-34} {"Type",-11} {"Term",-5} {"Active",-6}" + string.Concat(markets.Select(m => $" {m.Short,7}"));
        Console.WriteLine(header);
        Console.WriteLine("  " + new string('-', header.Length - 2));
        foreach (var p in plans)
            Console.WriteLine($"  {p.Code,-10} {Cut(p.Name, 34),-34} {p.Type,-11} {(p.TermMonths <= 1 ? "M2M" : p.TermMonths + "mo"),-5} {(p.Active ? "yes" : "no"),-6}"
                + string.Concat(markets.Select(m => $" {(RateFor(p.Id, m.Id) is { } r ? r.ToString("0.00") : "—"),7}")));
        Console.WriteLine();

        // Every table and how many rows it has
        Console.WriteLine("TABLES");
        var counts = new List<(string Name, int Rows)>();
        foreach (var prop in typeof(AppDbContext).GetProperties().Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)))
        {
            var count = typeof(Commands).GetMethod(nameof(Count), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(prop.PropertyType.GetGenericArguments()[0]);
            counts.Add((prop.Name, await (Task<int>)count.Invoke(null, [prop.GetValue(db)])!));
        }
        foreach (var row in counts.Chunk(3))
            Console.WriteLine("  " + string.Concat(row.Select(t => $"{t.Name,-22}{t.Rows,6}    ")));
        Console.WriteLine();

        // Who can sign in (every sample password is "password")
        Console.WriteLine("ADMIN SIGN-INS · http://localhost:5200 · password: password");
        foreach (var u in await db.Users.Include(u => u.Role).Where(u => u.Active).OrderBy(u => u.Id).ToListAsync())
            Console.WriteLine($"  {u.Email,-28} {u.Name,-20} {u.Role!.Name}{(u.Company != null ? " · " + u.Company : "")}");
        Console.WriteLine();
        Console.WriteLine("MY ACCOUNT SIGN-INS · http://localhost:5100/myaccount/login · password: password");
        foreach (var c in await db.Customers.Where(c => c.Username != null && c.PasswordHash != null).OrderBy(c => c.Id).Take(12).ToListAsync())
            Console.WriteLine($"  {c.Username,-28} {c.Name,-22} account {c.Account} · {c.Status}");
        Console.WriteLine();
        Console.WriteLine("Start the admin (dotnet run in src/RcBunny.Backend), then the website (dotnet run in src/RcBunny.Frontend).");
    }

    private static Task<int> Count<T>(DbSet<T> set) where T : class => set.CountAsync();

    private static string Cut(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
}
