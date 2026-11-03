using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RcBunny.Data;

namespace RcBunny.Backend.Resources;

public sealed record FieldMeta(string Name, string Prop, string Label, string Type, bool Required, bool ReadOnly, int? MaxLength,
    IReadOnlyList<string>? Options, string? Ref);

public sealed record ListQuery(string? Q, int Page, int Limit, string? Sort, IReadOnlyDictionary<string, string> Filters);

public sealed record ListResult(IReadOnlyList<Dictionary<string, object?>> Rows, int Total, int Page, int Limit,
    IReadOnlyDictionary<string, Dictionary<string, string>> Labels);

/// <summary>
/// Reads and writes any table from its EF Core metadata: fields and their types, search, filters, sort, paging,
/// labels for foreign keys, and updates from JSON. Used by the generic admin screens.
/// </summary>
public static class ResourceEngine
{
    /// <summary>Never sent to the browser, whatever the screen.</summary>
    private static readonly HashSet<string> Secret = ["Password", "RememberToken", "SecurityStamp", "Token", "SsnLast4"];
    private static readonly string[] LabelProps = ["Name", "Title", "Subject", "Question", "Label", "Account", "Email", "Code", "Key", "Number", "Sku", "Reference", "Path", "Command", "Msid", "Value"];

    public static IEntityType EntityType(AppDbContext db, Type t) => db.Model.FindEntityType(t)!;

    public static IQueryable<T> Set<T>(AppDbContext db) where T : class => db.Set<T>();

    private static string Camel(string s) => char.ToLowerInvariant(s[0]) + s[1..];

    private static string Humanize(string prop)
    {
        var s = System.Text.RegularExpressions.Regex.Replace(prop, "(?<=[a-z0-9])([A-Z])", " $1");
        if (s.EndsWith(" Id")) s = s[..^3];
        return s.Replace("Esiid", "ESIID").Replace("Msid", "MSID").Replace("Kwh", "kWh").Replace("Url", "URL").Replace("Api", "API");
    }

    public static IReadOnlyList<FieldMeta> Fields(AppDbContext db, ResourceDef def)
    {
        var et = EntityType(db, def.Entity);
        var list = new List<FieldMeta>();
        foreach (var p in Ordered(et))
        {
            if (p.PropertyInfo is null || Secret.Contains(p.Name) || def.Hidden.Contains(p.Name)) continue;
            var t = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
            var fk = p.GetContainingForeignKeys().FirstOrDefault();
            var type = fk != null ? "ref"
                : t == typeof(bool) ? "bool" : t == typeof(int) || t == typeof(long) ? "int" : t == typeof(decimal) ? "decimal"
                : t == typeof(DateOnly) ? "date" : t == typeof(DateTime) ? "datetime" : t == typeof(TimeOnly) ? "time"
                : t == typeof(List<string>) ? "list" : t == typeof(JsonNode) ? "json"
                : (p.GetMaxLength() ?? int.MaxValue) > 255 ? "text" : "string";
            if (type == "string" && (p.Name.EndsWith("Html") || p.Name is "Html" or "Body" or "Message" or "Transcript" or "SystemPrompt")) type = "text";
            var ro = p.IsPrimaryKey() && p.ValueGenerated != ValueGenerated.Never || p.Name is "CreatedAt" or "UpdatedAt" || def.ReadOnly.Contains(p.Name) || def.Fixed.ContainsKey(p.Name);
            list.Add(new FieldMeta(Camel(p.Name), p.Name, Humanize(p.Name), type,
                !p.IsNullable && type is "string" or "text" or "ref" && !ro, ro, p.GetMaxLength(),
                def.Options.TryGetValue(p.Name, out var opts) ? opts : null, fk?.PrincipalEntityType.GetTableName()));
        }
        return list;
    }

    /// <summary>Properties in the table's column order (as declared), not EF's alphabetical order.</summary>
    private static IEnumerable<IProperty> Ordered(IEntityType et)
    {
        var order = et.ClrType.GetProperties().Select((p, i) => (p.Name, i)).ToDictionary(x => x.Name, x => x.i);
        return et.GetProperties().OrderBy(p => order.GetValueOrDefault(p.Name, int.MaxValue));
    }

    private static PropertyInfo Prop(Type t, string name) =>
        t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) ?? throw new ActionError($"Unknown field {name}.");

    // ---- reads ------------------------------------------------------------------------------------------

    public static Task<ListResult> ListAsync(AppDbContext db, ResourceDef def, ListQuery q, CancellationToken ct) =>
        (Task<ListResult>)typeof(ResourceEngine).GetMethod(nameof(ListCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(def.Entity).Invoke(null, [db, def, q, ct])!;

    private static async Task<ListResult> ListCore<T>(AppDbContext db, ResourceDef def, ListQuery q, CancellationToken ct) where T : class
    {
        IQueryable<T> query = db.Set<T>();
        var x = Expression.Parameter(typeof(T), "x");
        foreach (var (k, v) in def.Fixed) query = query.Where(Equal<T>(x, k, v.ToString()!));
        foreach (var (k, v) in q.Filters)
        {
            if (string.IsNullOrEmpty(v)) continue;
            var p = Prop(typeof(T), k);
            if (!def.Filters.Contains(p.Name, StringComparer.OrdinalIgnoreCase) && !p.Name.EndsWith("Id")) continue;   // filter only where allowed (and by parent id for related lists)
            query = query.Where(Equal<T>(x, p.Name, v));
        }
        if (!string.IsNullOrWhiteSpace(q.Q) && def.Search.Length > 0)
        {
            var like = "%" + q.Q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            Expression? any = null;
            var likeMethod = typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;
            foreach (var s in def.Search)
            {
                var p = Prop(typeof(T), s);
                Expression member = Expression.Property(x, p);
                if (p.PropertyType != typeof(string)) continue;
                var call = Expression.Call(likeMethod, Expression.Constant(EF.Functions), member, Expression.Constant(like), Expression.Constant("\\"));
                var test = Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null, typeof(string))), call);
                any = any == null ? test : Expression.OrElse(any, test);
            }
            if (any != null) query = query.Where(Expression.Lambda<Func<T, bool>>(any, x));
        }
        var total = await query.CountAsync(ct);
        var sort = string.IsNullOrEmpty(q.Sort) ? def.Sort : q.Sort;
        var desc = sort.StartsWith('-');
        var sp = Prop(typeof(T), sort.TrimStart('-'));
        var key = Expression.Lambda(Expression.Property(x, sp), x);
        var ordered = (IQueryable<T>)typeof(Queryable).GetMethods().First(m => m.Name == (desc ? "OrderByDescending" : "OrderBy") && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), sp.PropertyType).Invoke(null, [query, key])!;
        if (sp.Name != "Id" && typeof(T).GetProperty("Id") is { } idp)
        {
            var idKey = Expression.Lambda(Expression.Property(x, idp), x);
            ordered = (IQueryable<T>)typeof(Queryable).GetMethods().First(m => m.Name == (desc ? "ThenByDescending" : "ThenBy") && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), idp.PropertyType).Invoke(null, [ordered, idKey])!;
        }
        var limit = Math.Clamp(q.Limit, 1, 500);
        var page = Math.Max(1, q.Page);
        var rows = await ordered.Skip((page - 1) * limit).Take(limit).ToListAsync(ct);
        var dicts = rows.Select(r => ToDict(db, def, r)).ToList();
        return new ListResult(dicts, total, page, limit, await LabelsFor(db, def.Entity, rows, ct));
    }

    public static Task<Dictionary<string, object?>?> GetAsync(AppDbContext db, ResourceDef def, string id, CancellationToken ct) =>
        (Task<Dictionary<string, object?>?>)typeof(ResourceEngine).GetMethod(nameof(GetCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(def.Entity).Invoke(null, [db, def, id, ct])!;

    private static async Task<Dictionary<string, object?>?> GetCore<T>(AppDbContext db, ResourceDef def, string id, CancellationToken ct) where T : class
    {
        var row = await FindAsync<T>(db, def, id, tracking: false, ct);
        if (row is null) return null;
        var d = ToDict(db, def, row);
        d["_labels"] = await LabelsFor(db, def.Entity, [row], ct);
        return d;
    }

    public static async Task<T?> FindAsync<T>(AppDbContext db, ResourceDef def, string id, bool tracking, CancellationToken ct) where T : class
    {
        var et = EntityType(db, typeof(T));
        var keys = et.FindPrimaryKey()!.Properties;
        var parts = id.Split(',');
        if (parts.Length != keys.Count) return null;
        var x = Expression.Parameter(typeof(T), "x");
        IQueryable<T> q = tracking ? db.Set<T>().AsTracking() : db.Set<T>();
        foreach (var (k, v) in def.Fixed) q = q.Where(Equal<T>(x, k, v.ToString()!));
        try
        {
            for (var i = 0; i < keys.Count; i++) q = q.Where(Equal<T>(x, keys[i].Name, parts[i]));
        }
        catch (FormatException) { return null; }
        return await q.FirstOrDefaultAsync(ct);
    }

    public static Task<object?> FindObjectAsync(AppDbContext db, ResourceDef def, string id, bool tracking, CancellationToken ct) =>
        InvokeFind(db, def, id, tracking, ct);

    private static async Task<object?> InvokeFind(AppDbContext db, ResourceDef def, string id, bool tracking, CancellationToken ct)
    {
        var task = (Task)typeof(ResourceEngine).GetMethod(nameof(FindAsync))!.MakeGenericMethod(def.Entity).Invoke(null, [db, def, id, tracking, ct])!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }

    private static Expression<Func<T, bool>> Equal<T>(ParameterExpression x, string prop, string value)
    {
        var p = Prop(typeof(T), prop);
        var member = Expression.Property(x, p);
        var target = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        object? v = value == "" || value == "null" ? null : ConvertTo(target, value);
        var body = Expression.Equal(member, Expression.Constant(v, p.PropertyType));
        return Expression.Lambda<Func<T, bool>>(body, x);
    }

    private static object ConvertTo(Type t, string v) =>
        t == typeof(string) ? v
        : t == typeof(bool) ? v is "1" or "true" or "on" or "yes"
        : t == typeof(DateOnly) ? DateOnly.Parse(v, CultureInfo.InvariantCulture)
        : t == typeof(DateTime) ? DateTime.Parse(v, CultureInfo.InvariantCulture)
        : t == typeof(TimeOnly) ? TimeOnly.Parse(v, CultureInfo.InvariantCulture)
        : Convert.ChangeType(v, t, CultureInfo.InvariantCulture);

    public static Dictionary<string, object?> ToDict(AppDbContext db, ResourceDef? def, object row)
    {
        var d = new Dictionary<string, object?>();
        foreach (var p in Ordered(EntityType(db, row.GetType())))
        {
            if (p.PropertyInfo is null || Secret.Contains(p.Name) || def?.Hidden.Contains(p.Name) == true) continue;
            d[Camel(p.Name)] = p.PropertyInfo.GetValue(row);
        }
        d["_label"] = Label(row);
        d["_id"] = string.Join(",", EntityType(db, row.GetType()).FindPrimaryKey()!.Properties.Select(k => k.PropertyInfo!.GetValue(row)));
        return d;
    }

    public static string Label(object row)
    {
        var t = row.GetType();
        if (t.Name == "Customer") return $"{t.GetProperty("Account")!.GetValue(row)} · {t.GetProperty("Name")!.GetValue(row)}";
        foreach (var n in LabelProps)
            if (t.GetProperty(n)?.GetValue(row) is { } v && v.ToString() is { Length: > 0 } s)
                return s.Length > 80 ? s[..80] + "…" : s;
        return "#" + (t.GetProperty("Id")?.GetValue(row) ?? "?");
    }

    /// <summary>Foreign key value → label of the row it points at, for every reference on these rows.</summary>
    private static async Task<IReadOnlyDictionary<string, Dictionary<string, string>>> LabelsFor<T>(AppDbContext db, Type entity, IReadOnlyList<T> rows, CancellationToken ct) where T : class
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        if (rows.Count == 0) return result;
        foreach (var fk in EntityType(db, entity).GetForeignKeys())
        {
            var p = fk.Properties[0];
            if (p.PropertyInfo is null) continue;
            var ids = rows.Select(r => p.PropertyInfo.GetValue(r)).Where(v => v != null).Distinct().ToList();
            if (ids.Count == 0) continue;
            var labels = await LookupLabels(db, fk.PrincipalEntityType.ClrType, ids!, ct);
            result[Camel(p.Name)] = labels;
        }
        return result;
    }

    public static Task<Dictionary<string, string>> LookupLabels(AppDbContext db, Type principal, IReadOnlyList<object> ids, CancellationToken ct) =>
        (Task<Dictionary<string, string>>)typeof(ResourceEngine).GetMethod(nameof(LookupCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(principal).Invoke(null, [db, ids, ct])!;

    private static async Task<Dictionary<string, string>> LookupCore<T>(AppDbContext db, IReadOnlyList<object> ids, CancellationToken ct) where T : class
    {
        var pk = EntityType(db, typeof(T)).FindPrimaryKey()!.Properties[0];
        var x = Expression.Parameter(typeof(T), "x");
        var typed = ids.Select(i => Convert.ChangeType(i, pk.ClrType, CultureInfo.InvariantCulture)).ToList();
        var listType = typeof(List<>).MakeGenericType(pk.ClrType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var t in typed) list.Add(t);
        var call = Expression.Call(Expression.Constant(list), listType.GetMethod("Contains")!, Expression.Property(x, pk.PropertyInfo!));
        var rows = await db.Set<T>().Where(Expression.Lambda<Func<T, bool>>(call, x)).ToListAsync(ct);
        return rows.ToDictionary(r => pk.PropertyInfo!.GetValue(r)!.ToString()!, r => Label(r));
    }

    /// <summary>Choices for a reference field: up to 200 rows of the table, filtered by text.</summary>
    public static Task<List<KeyValuePair<string, string>>> Options(AppDbContext db, Type principal, string? q, CancellationToken ct) =>
        (Task<List<KeyValuePair<string, string>>>)typeof(ResourceEngine).GetMethod(nameof(OptionsCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(principal).Invoke(null, [db, q, ct])!;

    private static async Task<List<KeyValuePair<string, string>>> OptionsCore<T>(AppDbContext db, string? q, CancellationToken ct) where T : class
    {
        var pk = EntityType(db, typeof(T)).FindPrimaryKey()!.Properties[0];
        var rows = await db.Set<T>().Take(2000).ToListAsync(ct);
        return rows.Select(r => new KeyValuePair<string, string>(pk.PropertyInfo!.GetValue(r)!.ToString()!, Label(r)))
            .Where(kv => string.IsNullOrEmpty(q) || kv.Value.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Value).Take(200).ToList();
    }

    // ---- writes -----------------------------------------------------------------------------------------

    /// <summary>Copies the editable fields present in the JSON onto the row (unknown or read-only fields are ignored).</summary>
    public static List<string> Apply(AppDbContext db, ResourceDef def, object row, JsonObject input)
    {
        var changed = new List<string>();
        foreach (var f in Fields(db, def))
        {
            if (f.ReadOnly || !input.TryGetPropertyValue(f.Name, out var node)) continue;
            var p = row.GetType().GetProperty(f.Prop)!;
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            object? v;
            try
            {
                v = node is null ? null
                    : t == typeof(JsonNode) ? node.DeepClone()
                    : t == typeof(List<string>) ? node.AsArray().Select(n => n!.ToString()).ToList()
                    : node.GetValueKind() == JsonValueKind.String && t != typeof(string) && node.ToString() == "" ? null
                    : t == typeof(string) ? node.ToString()
                    : ConvertTo(t, node.ToString());
            }
            catch (Exception) { throw new ActionError($"{f.Label}: that isn't a valid value."); }
            if (v is null && !IsNullable(p) && t != typeof(string)) throw new ActionError($"{f.Label} is required.");
            if (v is string s && f.MaxLength is { } max && s.Length > max) throw new ActionError($"{f.Label} can be at most {max} characters.");
            if (v is string s2 && f.Options is { } opts && s2 != "" && !opts.Contains(s2)) throw new ActionError($"{f.Label}: pick one of the choices.");
            if (f.Required && (v is null || v is string { Length: 0 })) throw new ActionError($"{f.Label} is required.");
            var old = p.GetValue(row);
            if (Equals(old, v) || (old is JsonNode a && v is JsonNode b && JsonNode.DeepEquals(a, b))) continue;
            p.SetValue(row, v ?? (t == typeof(string) && !IsNullable(p) ? "" : null));
            changed.Add(f.Prop);
        }
        foreach (var (k, v) in def.Fixed) row.GetType().GetProperty(k)!.SetValue(row, v);
        return changed;
    }

    private static bool IsNullable(PropertyInfo p) =>
        Nullable.GetUnderlyingType(p.PropertyType) != null || new NullabilityInfoContext().Create(p).WriteState == NullabilityState.Nullable;
}
