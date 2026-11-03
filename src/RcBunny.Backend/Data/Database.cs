using Microsoft.EntityFrameworkCore;

namespace RcBunny.Data;

public static class Database
{
    /// <summary>SQLite in Development, SQL Server otherwise (Database:Provider and ConnectionStrings:Default).</summary>
    public static void AddAppDatabase(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Default");
        services.AddDbContext<AppDbContext>(o =>
        {
            if (config["Database:Provider"] == "Sqlite") o.UseSqlite(connection);
            else o.UseSqlServer(connection);
        });
    }
}
