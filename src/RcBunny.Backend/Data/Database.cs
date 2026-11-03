using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RcBunny.Data;

/// <summary>
/// Picks the database from configuration, the same way in both projects:
///   "Database": { "Provider": "SqlServer" | "Sqlite" }, "ConnectionStrings": { "Default": "..." }
/// SQL Server is the real target; SQLite is the zero-setup local fallback.
/// </summary>
public static class Database
{
    public static string Provider(IConfiguration config) => config["Database:Provider"] ?? "SqlServer";

    public static bool IsSqlServer(IConfiguration config) => Provider(config).Equals("SqlServer", StringComparison.OrdinalIgnoreCase);

    public static IServiceCollection AddRcBunnyDatabase(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not set.");
        var sqlServer = IsSqlServer(config);

        // Pooled contexts: cheaper per request, which matters on busy admin screens.
        services.AddDbContextPool<AppDbContext>(o =>
        {
            if (sqlServer)
                o.UseSqlServer(cs, s => s.EnableRetryOnFailure(3));
            else
                o.UseSqlite(cs);
            o.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });
        return services;
    }
}
