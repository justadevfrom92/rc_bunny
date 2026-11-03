using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Access;

/// <summary>
/// Everything needed to answer "may this person do X?" without touching the database.
/// Built from the user row + their role, cached, and thrown away whenever either changes.
/// </summary>
public sealed record AccessSnapshot(
    int UserId,
    string Name,
    string Email,
    string? Title,
    string? RoleName,
    string? Company,
    DateOnly? AccessEndsOn,
    bool Active,
    int AccessVersion,
    string SecurityStamp,
    IReadOnlyList<string> Perms)
{
    /// <summary>Signed-in and allowed right now: active and before their access end date.</summary>
    public bool CanSignIn => Active && (AccessEndsOn is null || AccessEndsOn >= DateOnly.FromDateTime(DateTime.Today));

    public bool Has(string perm) => CanSignIn && Perms.Contains(perm);

    public bool IsExternal => !string.IsNullOrEmpty(Company);
}

public class AccessService(AppDbContext db, HybridCache cache)
{
    public const string Tag = "access";
    private static readonly HybridCacheEntryOptions Options = new()
    {
        // Short local TTL: on a web farm another node's change is picked up within a minute even without a
        // shared (Redis) L2. With an L2 configured the tag removal reaches every node immediately.
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
        Expiration = TimeSpan.FromMinutes(10),
    };

    private static string Key(int userId) => $"access:{userId}";

    public ValueTask<AccessSnapshot?> GetAsync(int userId, CancellationToken ct = default) =>
        cache.GetOrCreateAsync(Key(userId), async token => await LoadAsync(userId, token), Options, [Tag, Key(userId)], ct);

    private async Task<AccessSnapshot?> LoadAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId, ct);
        AdminApps.CustomKeys = (await db.AdminApps.Select(a => a.Key).ToListAsync(ct)).ToArray();
        return user is null ? null : Build(user);
    }

    public static AccessSnapshot Build(User user) => new(
        user.Id, user.Name, user.Email, user.Title, user.Role?.Name, user.Company, user.AccessEndsOn, user.Active,
        user.AccessVersion, user.SecurityStamp, EffectivePerms(user));

    /// <summary>Role perms + added − taken away, in the canonical order (apps, then rights).</summary>
    public static List<string> EffectivePerms(User user)
    {
        var role = user.Role?.Perms ?? [];
        var admin = user.Role?.IsAdministrator == true;   // Administrator always has every app and right
        return AdminApps.AllPerms
            .Where(p => !user.DeniedPerms.Contains(p) && (admin || role.Contains(p) || user.ExtraPerms.Contains(p)))
            .ToList();
    }

    /// <summary>Call after changing one person.</summary>
    public ValueTask InvalidateAsync(int userId) => cache.RemoveByTagAsync(Key(userId));

    /// <summary>Call after changing a role (affects everyone who holds it).</summary>
    public ValueTask InvalidateAllAsync() => cache.RemoveByTagAsync(Tag);

    /// <summary>
    /// "Log in as": allowed with Sheriff, for an active person other than yourself, and only when they
    /// have nothing you lack (you can't gain access that way). Null when allowed, otherwise why not.
    /// </summary>
    public static string? CannotLogInAs(AccessSnapshot me, AccessSnapshot target)
    {
        if (!me.Has("sheriff")) return "Needs Sheriff.";
        if (me.UserId == target.UserId) return "This is you.";
        if (!target.CanSignIn) return "Their sign-in is disabled or their access has ended.";
        var extra = target.Perms.Except(me.Perms).ToList();
        return extra.Count > 0 ? $"They have access you don't ({string.Join(", ", extra.Select(AdminApps.Label))})." : null;
    }
}
