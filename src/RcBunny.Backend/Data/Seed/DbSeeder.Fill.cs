using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace RcBunny.Data.Seed;

public partial class DbSeeder
{
    private static readonly HashSet<string> NeverFill = ["audit_entries", "customer_password_resets", "signups", "scheduled_payments", "table_snapshots", "report_columns"];

    /// <summary>
    /// Like ebunny's FillTablesSeeder: every admin table that would otherwise be empty gets a few believable rows,
    /// so each screen has something to show. Values come from the column's type and name; foreign keys point at real rows.
    /// </summary>
    private async Task FillEmptyTables()
    {
        foreach (var entity in db.Model.GetEntityTypes().OrderBy(e => e.GetTableName()))
        {
            var table = entity.GetTableName()!;
            if (NeverFill.Contains(table)) continue;
            var set = (IQueryable<object>)db.GetType().GetMethod("Set", Type.EmptyTypes)!.MakeGenericMethod(entity.ClrType).Invoke(db, null)!;
            if (await set.AnyAsync()) continue;

            var fkValues = new Dictionary<IProperty, List<object>>();
            var skip = false;
            foreach (var fk in entity.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType;
                var pset = (IQueryable<object>)db.GetType().GetMethod("Set", Type.EmptyTypes)!.MakeGenericMethod(principal.ClrType).Invoke(db, null)!;
                var key = fk.PrincipalKey.Properties[0];
                var ids = (await pset.Take(20).ToListAsync()).Select(o => key.PropertyInfo!.GetValue(o)!).ToList();
                if (ids.Count == 0 && !fk.IsRequired) continue;
                if (ids.Count == 0) { skip = true; break; }
                fkValues[fk.Properties[0]] = ids;
            }
            if (skip) continue;

            for (var i = 0; i < 6; i++)
            {
                var row = Activator.CreateInstance(entity.ClrType)!;
                foreach (var p in entity.GetProperties())
                {
                    if (p.PropertyInfo is null || (p.IsPrimaryKey() && p.ValueGenerated != ValueGenerated.Never && !p.IsForeignKey())) continue;
                    if (fkValues.TryGetValue(p, out var ids)) { p.PropertyInfo.SetValue(row, ids[(i + rng.Next(ids.Count)) % ids.Count]); continue; }
                    if (p.IsForeignKey()) continue;
                    var v = SampleValue(p, i);
                    if (v != null) p.PropertyInfo.SetValue(row, v);
                }
                db.Add(row);
            }
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException e) { log.LogWarning("Couldn't fill {Table}: {Message}", table, e.InnerException?.Message ?? e.Message); }
            db.ChangeTracker.Clear();
        }
    }

    private object? SampleValue(IProperty p, int i)
    {
        var t = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
        var name = p.GetColumnName();
        if (t == typeof(string))
        {
            var max = p.GetMaxLength() ?? 255;
            var s = name switch
            {
                var n when n.Contains("email") => $"sample{i}@example.com",
                var n when n.Contains("phone") => $"(555) 555-01{i:00}",
                "status" => new[] { "active", "pending", "done" }[i % 3],
                "key" or "code" or "token" or "number" or "sku" => $"{name.ToUpper()}-{i + 1}-{rng.Next(1000, 9999)}",
                var n when n.EndsWith("html") || n == "body" => "<p>Sample content.</p>",
                _ => $"Sample {name.Replace('_', ' ')} {i + 1}",
            };
            return s.Length > max ? s[..max] : s;
        }
        if (t == typeof(int) || t == typeof(long)) return Convert.ChangeType(rng.Next(1, 100), t);
        if (t == typeof(decimal)) return (decimal)rng.Next(100, 10000) / 100m;
        if (t == typeof(bool)) return i % 2 == 0;
        if (t == typeof(DateTime)) return Ago(30);
        if (t == typeof(DateOnly)) return today.AddDays(-rng.Next(0, 30));
        if (t == typeof(TimeOnly)) return new TimeOnly(rng.Next(8, 18), 0);
        if (t == typeof(JsonNode)) return new JsonObject();
        return null;
    }
}
