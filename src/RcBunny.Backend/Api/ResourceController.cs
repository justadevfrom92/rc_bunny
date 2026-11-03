using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RcBunny.Backend.Access;
using RcBunny.Backend.Infrastructure;
using RcBunny.Backend.Resources;
using RcBunny.Data;

namespace RcBunny.Backend.Api;

/// <summary>
/// The generic admin screens: /api/r/{resource} list, record, create, edit, delete, actions, plus reference lookups.
/// Access: the resource's app (and its write right / the action's right); checked on every call.
/// Reads are cached per query with the tags of every table they show, and cleared automatically on save.
/// </summary>
[Route("api/r")]
public class ResourceController(AppDbContext db, HybridCache cache, IServiceProvider services) : ApiControllerBase
{
    private static readonly HybridCacheEntryOptions ReadCache = new() { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromMinutes(1) };

    private ResourceDef? Def(string key, out IActionResult? error)
    {
        error = null;
        var def = ResourceRegistry.Get(key);
        if (def is null) { error = NotFound(new { code = "not_found", message = "No such screen." }); return null; }
        if (!Can(def.App)) { error = Forbidden(def.App); return null; }
        return def;
    }

    private string[] Tags(ResourceDef def)
    {
        var et = ResourceEngine.EntityType(db, def.Entity);
        return [TableCacheInvalidator.Tag(et.GetTableName()!), .. et.GetForeignKeys().Select(f => TableCacheInvalidator.Tag(f.PrincipalEntityType.GetTableName()!)).Distinct()];
    }

    /// <summary>Every screen this person can open, by app (for menus and links).</summary>
    [HttpGet]
    public IActionResult Index() => Ok(ResourceRegistry.All.Where(d => Can(d.App)).Select(d => new { d.Key, d.App, d.Title }));

    [HttpGet("{key}/meta")]
    public IActionResult Meta(string key)
    {
        var def = Def(key, out var err);
        if (def is null) return err!;
        Response.Headers.CacheControl = "private, max-age=300";
        var fields = ResourceEngine.Fields(db, def);
        var canWrite = def.WritePerm is null || Can(def.WritePerm);
        return Ok(new
        {
            def.Key, def.App, def.Title, name = def.Name, def.Description,
            list = (def.List.Length > 0 ? def.List.Select(l => fields.First(f => f.Prop == l).Name) : fields.Where(f => f.Type is not ("text" or "json")).Take(6).Select(f => f.Name)).ToArray(),
            search = def.Search.Length > 0, filters = def.Filters.Select(f => fields.First(x => x.Prop == f).Name), sort = def.Sort,
            fields, create = def.Create && canWrite, edit = def.Edit && canWrite, delete = def.Delete && canWrite && Can("delete"),
            actions = def.Actions.Where(a => a.Perm is null || Can(a.Perm)).Select(a => new { a.Key, a.Label, a.Confirm, a.Inputs, a.Tone }),
            children = def.Children.Where(c => ResourceRegistry.Get(c.Resource) is { } r && Can(r.App))
                .Select(c => new { c.Title, c.Resource, foreignKey = char.ToLowerInvariant(c.ForeignKey[0]) + c.ForeignKey[1..] }),
        });
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> List(string key, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int limit = 25, [FromQuery] string? sort = null, CancellationToken ct = default)
    {
        var def = Def(key, out var err);
        if (def is null) return err!;
        var filters = Request.Query.Where(kv => kv.Key.StartsWith("f.")).ToDictionary(kv => kv.Key[2..], kv => kv.Value.ToString());
        var query = new ListQuery(q, page, limit, sort, filters);
        var cacheKey = $"r:{key}:{q}|{page}|{limit}|{sort}|{string.Join(',', filters.OrderBy(f => f.Key).Select(f => f.Key + "=" + f.Value))}";
        try
        {
            return Ok(await cache.GetOrCreateAsync(cacheKey, async t => await ResourceEngine.ListAsync(db, def, query, t), ReadCache, Tags(def), ct));
        }
        catch (ActionError e) { return Problem422(e.Message); }
    }

    [HttpGet("{key}/{id}")]
    public async Task<IActionResult> Show(string key, string id, CancellationToken ct)
    {
        var def = Def(key, out var err);
        if (def is null) return err!;
        var row = await cache.GetOrCreateAsync($"r:{key}:#{id}", async t => await ResourceEngine.GetAsync(db, def, id, t), ReadCache, Tags(def), ct);
        if (row is null) return NotFound();
        var obj = await ResourceEngine.FindObjectAsync(db, def, id, false, ct);
        var actions = def.Actions.Where(a => (a.Perm is null || Can(a.Perm)) && (a.When is null || a.When(obj!))).Select(a => a.Key);
        return Ok(new { row, actions });
    }

    [HttpPost("{key}")]
    public async Task<IActionResult> Create(string key, JsonObject input, CancellationToken ct)
    {
        var def = Def(key, out var err);
        if (def is null) return err!;
        if (!def.Create) return Problem422("New records can't be added here.");
        if (def.WritePerm is { } wp && !Can(wp)) return Forbidden(wp);
        var row = Activator.CreateInstance(def.Entity)!;
        try
        {
            ResourceEngine.Apply(db, def, row, input);
            if (def.BeforeSave is { } before) await before(new Resources.ActionContext(db, row, input, Me, services));
        }
        catch (ActionError e) { return Problem422(e.Message); }
        db.Add(row);
        Audit(db, "created", $"{def.Name}: {ResourceEngine.Label(row)}");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) { return Problem422(SaveError(e)); }
        return Ok(ResourceEngine.ToDict(db, def, row));
    }

    [HttpPut("{key}/{id}")]
    public async Task<IActionResult> Update(string key, string id, JsonObject input, CancellationToken ct)
    {
        var def = Def(key, out var err);
        if (def is null) return err!;
        if (!def.Edit) return Problem422("These records can't be edited here.");
        if (def.WritePerm is { } wp && !Can(wp)) return Forbidden(wp);
        var row = await ResourceEngine.FindObjectAsync(db, def, id, true, ct);
        if (row is null) return NotFound();
        List<string> changed;
        try
        {
            changed = ResourceEngine.Apply(db, def, row, input);
            if (def.BeforeSave is { } before) await before(new Resources.ActionContext(db, row, input, Me, services));
        }
        catch (ActionError e) { return Problem422(e.Message); }
        if (changed.Count > 0) Audit(db, "updated", $"{def.Name}: {ResourceEngine.Label(row)}", string.Join(", ", changed));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) { return Problem422(SaveError(e)); }
        return Ok(ResourceEngine.ToDict(db, def, row));
    }

    [HttpDelete("{key}/{id}")]
    public async Task<IActionResult> Delete(string key, string id, CancellationToken ct)
    {
        var def = Def(key, out var err);
        if (def is null) return err!;
        if (!def.Delete) return Problem422("These records can't be deleted.");
        if (!Can("delete")) return Forbidden("delete");
        if (def.WritePerm is { } wp && !Can(wp)) return Forbidden(wp);
        var row = await ResourceEngine.FindObjectAsync(db, def, id, true, ct);
        if (row is null) return NotFound();
        db.Remove(row);
        Audit(db, "deleted", $"{def.Name}: {ResourceEngine.Label(row)}");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Problem422("Other records still use this one, so it can't be deleted."); }
        return NoContent();
    }

    [HttpPost("{key}/{id}/actions/{action}")]
    public async Task<IActionResult> Act(string key, string id, string action, [FromBody] JsonObject? input, CancellationToken ct)
    {
        var def = Def(key, out var err);
        if (def is null) return err!;
        var a = def.Actions.FirstOrDefault(x => x.Key == action);
        if (a is null) return NotFound();
        if (a.Perm is { } p && !Can(p)) return Forbidden(p);
        var row = await ResourceEngine.FindObjectAsync(db, def, id, true, ct);
        if (row is null) return NotFound();
        if (a.When is { } when && !when(row)) return Problem422($"{a.Label} isn't available for this record now.");
        string message;
        try { message = await a.Run(new Resources.ActionContext(db, row, input ?? [], Me, services)); }
        catch (ActionError e) { return Problem422(e.Message); }
        Audit(db, action.Replace('-', '_'), $"{def.Name}: {ResourceEngine.Label(row)}", message);
        await db.SaveChangesAsync(ct);
        return Ok(new { message });
    }

    /// <summary>Choices for a reference field (by table). Labels only; needs access to some screen that uses that table.</summary>
    [HttpGet("options/{table}")]
    public async Task<IActionResult> Options(string table, [FromQuery] string? q, CancellationToken ct)
    {
        var et = db.Model.GetEntityTypes().FirstOrDefault(e => e.GetTableName() == table);
        if (et is null) return NotFound();
        var allowed = ResourceRegistry.All.Any(d => Can(d.App) && (d.Entity == et.ClrType || ResourceEngine.EntityType(db, d.Entity).GetForeignKeys().Any(f => f.PrincipalEntityType == et)));
        if (!allowed) return Forbidden("any app that uses this list");
        return Ok(await cache.GetOrCreateAsync($"opts:{table}:{q}", async t => await ResourceEngine.Options(db, et.ClrType, q, t), ReadCache, [TableCacheInvalidator.Tag(table)], ct));
    }

    private static string SaveError(DbUpdateException e)
    {
        var m = e.InnerException?.Message ?? e.Message;
        return m.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) || m.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            ? "Another record already uses that value." : m.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ? "One of the linked records doesn't exist." : "Couldn't save that.";
    }
}
