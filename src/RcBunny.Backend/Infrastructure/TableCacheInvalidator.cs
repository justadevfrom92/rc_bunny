using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;

namespace RcBunny.Backend.Infrastructure;

/// <summary>
/// Every save, from any screen or job, removes the cached reads of the tables it touched (tag "t:&lt;table&gt;").
/// So a cached list never outlives a change to its rows, without each endpoint remembering what to clear.
/// </summary>
public sealed class TableCacheInvalidator(IServiceProvider services) : SaveChangesInterceptor
{
    private const string Key = "rcb:changed-tables";

    public static string Tag(string table) => "t:" + table;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (e.Context is { } ctx)
        {
            var tables = ctx.ChangeTracker.Entries()
                .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(x => x.Metadata.GetTableName()).OfType<string>().Distinct().ToList();
            Pending.Value = tables;
        }
        return base.SavingChangesAsync(e, result, ct);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int result, CancellationToken ct = default)
    {
        if (Pending.Value is { Count: > 0 } tables)
        {
            Pending.Value = null;
            var cache = services.GetRequiredService<HybridCache>();
            await cache.RemoveByTagAsync(tables.Select(Tag), ct);
            // The hand-written screens' own tags, for the tables they read.
            if (tables.Contains("customers") || tables.Contains("payments") || tables.Contains("notes") || tables.Contains("bills"))
                await cache.RemoveByTagAsync("customers", ct);
            if (tables.Contains("users") || tables.Contains("roles"))
                await cache.RemoveByTagAsync(["staff", "access"], ct);
        }
        return await base.SavedChangesAsync(e, result, ct);
    }

    private static readonly AsyncLocal<List<string>?> Pending = new();

    public static string Name => Key;
}
