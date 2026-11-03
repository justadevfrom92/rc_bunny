using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Data;

namespace RcBunny.Auth;

public static class Permissions
{
    /// <summary>Every permission a role can have. The admin's menus and buttons use the same keys.</summary>
    public static readonly Dictionary<string, string> All = new()
    {
        ["corral"] = "Corral: customers, orders, texts and calls, CRM, products, ERCOT, exception queues",
        ["refunds"] = "Approve refunds, credits and debits, and reverse payments",
        ["plans"] = "Change plans and rates",
        ["caboose"] = "Caboose: payments, credits and debits, refunds, deposits, aging, the journal, MSIDs and promo codes",
        ["bounty"] = "Bounty: reward offers, earning rules, redemptions, members and the monthly drawing",
        ["ranger"] = "Ranger: support tickets and website messages",
        ["sheriff"] = "Sheriff: staff, roles, heartbeat, crons, markets and integrations",
    };

    public record SignedIn(int Id, string Name, string Email, string Role, List<string> Permissions);

    /// <summary>
    /// The signed-in employee as the database has them now (kept in memory for 30 seconds). Returns null when signed out
    /// or disabled. Staff changes clear this cache, so taking access away works on the person's next click.
    /// </summary>
    public static async Task<SignedIn?> CurrentAsync(HttpContext http)
    {
        if (!int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        var cache = http.RequestServices.GetRequiredService<IMemoryCache>();
        return await cache.GetOrCreateAsync(CacheKey(id), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            var db = http.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id && u.Active);
            return user is null ? null : new SignedIn(user.Id, user.Name, user.Email, user.Role!.Name, user.Role.PermissionList());
        });
    }

    public static string CacheKey(int userId) => $"user:{userId}";
}

/// <summary>[RequirePermission("customers")] on a controller or action: 401 if signed out, 403 without the permission.</summary>
public class RequirePermissionAttribute(string permission) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var me = await Permissions.CurrentAsync(context.HttpContext);
        if (me is null) context.Result = new UnauthorizedResult();
        else if (!me.Permissions.Contains(permission)) context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
    }
}
