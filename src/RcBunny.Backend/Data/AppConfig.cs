using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace RcBunny.Data;

/// <summary>
/// ebunny's config/*.php files, converted to JSON (tools/schema/phpconfig.py) and copied next to the app as
/// Data/Config/*.json: shop products, Walker reports, CMS, Corral lists, Deputy, My Account, item models, history…
/// Read once and kept in memory.
/// </summary>
public static class AppConfig
{
    private static readonly ConcurrentDictionary<string, JsonObject> Files = new();
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "Data", "Config");

    public static JsonObject Get(string file) => Files.GetOrAdd(file, f =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Dir, f + ".json")))!.AsObject());

    /// <summary>"walker.reports" → the node at that path (null when missing).</summary>
    public static JsonNode? At(string path)
    {
        var parts = path.Split('.');
        JsonNode? node = Get(parts[0]);
        foreach (var p in parts.Skip(1))
            node = node is JsonObject o ? o[p] : node is JsonArray a && int.TryParse(p, out var i) && i < a.Count ? a[i] : null;
        return node;
    }

    public static IEnumerable<string> Strings(string path) =>
        At(path) switch
        {
            JsonArray a => a.Select(x => x?.ToString() ?? ""),
            JsonObject o => o.Select(kv => kv.Value?.ToString() ?? ""),
            _ => [],
        };

    public static IEnumerable<string> Keys(string path) => At(path) is JsonObject o ? o.Select(kv => kv.Key) : [];
}
