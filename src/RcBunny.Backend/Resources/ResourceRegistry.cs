using System.Text.Json.Nodes;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Resources;

/// <summary>Every generic admin screen, by key. The definitions live in ResourceRegistry.*.cs, one file per app.</summary>
public static partial class ResourceRegistry
{
    private static readonly Lazy<Dictionary<string, ResourceDef>> Map = new(() =>
        Corral().Concat(Ranger()).Concat(Lando()).Concat(Astro()).Concat(Walker()).Concat(Caboose())
            .Concat(Bounty()).Concat(Rodeo()).Concat(Deputy()).Concat(Sheriff()).ToDictionary(d => d.Key));

    public static IEnumerable<ResourceDef> All => Map.Value.Values;

    public static ResourceDef? Get(string key) => Map.Value.GetValueOrDefault(key);

    // ---- small helpers for actions ---------------------------------------------------------------------
    private static T Row<T>(ActionContext c) => (T)c.Row;

    private static string In(ActionContext c, string name, bool required = true)
    {
        var v = c.Input[name]?.ToString()?.Trim() ?? "";
        if (required && v.Length == 0) throw new ActionError($"Fill in {name.Replace('_', ' ')}.");
        return v;
    }

    private static DateTime Now => DateTime.UtcNow;

    private static string[] Strs(string path) => AppConfig.Strings(path).ToArray();
    private static string[] Keys(string path) => AppConfig.Keys(path).ToArray();

    /// <summary>A note on the customer's account, the way ebunny's actions leave a trail.</summary>
    private static void AccountNote(ActionContext c, int customerId, string body, string? category = null, string? action = null) =>
        c.Db.Notes.Add(new Note { CustomerId = customerId, UserId = c.Me.UserId, Author = c.Me.Name, Body = body, Category = category, Action = action, Disposition = action });

    private static void Notify(ActionContext c, int userId, string kind, string title, string? body, string? url) =>
        c.Db.AdminNotifications.Add(new AdminNotification { UserId = userId, Kind = kind, Title = title, Body = body, Url = url });

    private static JsonArray Arr(params string[] items) => new(items.Select(i => (JsonNode?)i).ToArray());
}

public static partial class ResourceRegistry
{
    /// <summary>Every field name in every definition must exist on its entity; run at start-up (a typo fails fast).</summary>
    public static IEnumerable<string> Validate()
    {
        foreach (var d in All)
        {
            bool Has(string p) => d.Entity.GetProperty(p) != null;
            foreach (var p in d.List.Concat(d.Search).Concat(d.Filters).Concat(d.ReadOnly).Concat(d.Hidden).Concat(d.Options.Keys).Concat(d.Fixed.Keys).Append(d.Sort.TrimStart('-')))
                if (!Has(p)) yield return $"{d.Key}: {d.Entity.Name} has no {p}";
            foreach (var (_, res, fk) in d.Children)
                if (Get(res) is not { } child) yield return $"{d.Key}: no child resource {res}";
                else if (child.Entity.GetProperty(fk) is null) yield return $"{d.Key}: {child.Entity.Name} has no {fk}";
        }
    }
}
