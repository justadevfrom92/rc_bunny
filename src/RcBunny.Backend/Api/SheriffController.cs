using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using RcBunny.Backend.Access;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Api;

/// <summary>Sheriff: the staff directory, per-person access, roles, the timeline and caches.</summary>
[Route("api/sheriff"), RequirePerm("sheriff")]
public class SheriffController(AppDbContext db, AccessService access, HybridCache cache) : ApiControllerBase
{
    private const string DirectoryTag = "staff";
    private static readonly HybridCacheEntryOptions DirectoryCache = new() { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromMinutes(1) };

    public record StaffRow(int Id, string Name, string Email, string? EmployeeId, string? Title, string? Department, string? Company,
        string? Role, bool Active, DateOnly? AccessEndsOn, DateTime? LastLoginAt, bool HasExceptions, IReadOnlyList<string> Perms);

    /// <summary>Everyone, with their effective access. Filtering happens client-side on the cached list (hundreds of rows, not millions).</summary>
    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct) => Ok(await cache.GetOrCreateAsync("sheriff:staff", async token =>
    {
        var users = await db.Users.Include(u => u.Role).OrderBy(u => u.Name).ToListAsync(token);
        return users.Select(u => new StaffRow(u.Id, u.Name, u.Email, u.EmployeeId, u.Title, u.Department, u.Company, u.Role?.Name,
            u.Active, u.AccessEndsOn, u.LastLoginAt, u.ExtraPerms.Count + u.DeniedPerms.Count > 0, AccessService.EffectivePerms(u))).ToList();
    }, DirectoryCache, [DirectoryTag, AccessService.Tag], ct));

    [HttpGet("users/{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var u = await db.Users.Include(x => x.Role).Include(x => x.Manager).FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();
        var target = AccessService.Build(u);
        var reports = await db.Users.Where(x => x.ManagerId == id).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Title }).ToListAsync();
        var recent = await db.AuditEntries.Where(a => a.UserId == id).OrderByDescending(a => a.Id).Take(25).ToListAsync();
        return Ok(new
        {
            u.Id, u.Name, u.Email, u.EmployeeId, u.Title, u.Department, u.Company, u.AccessEndsOn, u.PhoneExt, u.Location, u.HiredOn,
            u.Active, u.LastLoginAt, u.RoleId, role = u.Role?.Name, rolePerms = u.Role?.Perms ?? [],
            u.ExtraPerms, u.DeniedPerms, u.AccessNote, u.ManagerId, manager = u.Manager?.Name,
            perms = target.Perms, reports, recent,
            canLogInAs = AccessService.CannotLogInAs(Me, target) is null,
            cannotLogInAs = AccessService.CannotLogInAs(Me, target),
        });
    }

    public record UserForm(
        string Name, string Email, string? EmployeeId, string? Title, string? Department, int? ManagerId, string? PhoneExt,
        string? Location, DateOnly? HiredOn, string? Company, DateOnly? AccessEndsOn, int RoleId,
        /// <summary>perm → "role" (as the role says) | "allow" (add) | "deny" (take away)</summary>
        Dictionary<string, string>? Access, string? AccessNote, string? Password);

    [HttpPost("users")]
    public async Task<IActionResult> Create(UserForm form)
    {
        if (await Validate(form, null) is { } bad) return bad;
        var user = new User { Active = true };
        await Apply(user, form);
        db.Users.Add(user);
        Audit(db, "user_created", user.Name, string.Join(", ", AccessService.EffectivePerms(user)));
        await db.SaveChangesAsync();
        await cache.RemoveByTagAsync(DirectoryTag);
        return Ok(new { id = user.Id });
    }

    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> Update(int id, UserForm form)
    {
        var user = await db.Users.AsTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (await Validate(form, user) is { } bad) return bad;

        var before = AccessService.EffectivePerms(user);
        var (roleBefore, companyBefore, endsBefore) = (user.RoleId, user.Company, user.AccessEndsOn);
        await Apply(user, form);
        var after = AccessService.EffectivePerms(user);

        if (user.Id == Me.UserId && (user.RoleId != roleBefore || !after.Contains("sheriff")))
            return Problem422("You can't change your own role or remove your own Sheriff access.", "roleId");

        if (!before.SequenceEqual(after) || roleBefore != user.RoleId || companyBefore != user.Company || endsBefore != user.AccessEndsOn)
        {
            user.AccessVersion++;
            Audit(db, "access_changed", user.Name,
                $"added: {string.Join(", ", after.Except(before))}; removed: {string.Join(", ", before.Except(after))}");
        }
        else Audit(db, "user_updated", user.Name);

        await db.SaveChangesAsync();
        await access.InvalidateAsync(id);
        await cache.RemoveByTagAsync(DirectoryTag);
        return Ok(new { id });
    }

    public record ActiveRequest(bool Active);

    /// <summary>Enable / disable. Disabling ends every sign-in they have, on their next request.</summary>
    [HttpPost("users/{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, ActiveRequest req)
    {
        if (id == Me.UserId) return Problem422("You can't disable your own account.");
        var user = await db.Users.AsTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        user.Active = req.Active;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AccessVersion++;
        Audit(db, req.Active ? "user_enabled" : "user_disabled", user.Name);
        await db.SaveChangesAsync();
        await access.InvalidateAsync(id);
        await cache.RemoveByTagAsync(DirectoryTag);
        return Ok(new { user.Active });
    }

    private async Task<IActionResult?> Validate(UserForm f, User? user)
    {
        if (string.IsNullOrWhiteSpace(f.Name) || f.Name.Length > 150) return Problem422("Enter a name.", "name");
        var email = f.Email?.Trim().ToLowerInvariant() ?? "";
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _)) return Problem422("Enter a valid email.", "email");
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != (user == null ? 0 : user.Id))) return Problem422("Someone already uses that email.", "email");
        if (!string.IsNullOrWhiteSpace(f.EmployeeId) && await db.Users.AnyAsync(u => u.EmployeeId == f.EmployeeId && u.Id != (user == null ? 0 : user.Id)))
            return Problem422("That employee ID is taken.", "employeeId");
        if (f.Department is not null && !AdminApps.Departments.Contains(f.Department)) return Problem422("Pick a department from the list.", "department");
        if (!await db.Roles.AnyAsync(r => r.Id == f.RoleId)) return Problem422("Pick a role.", "roleId");
        if (f.ManagerId is { } m && (m == user?.Id || !await db.Users.AnyAsync(u => u.Id == m))) return Problem422("Pick a different manager.", "managerId");
        if (user is null && string.IsNullOrEmpty(f.Password)) return Problem422("Set a temporary password.", "password");
        if (!string.IsNullOrEmpty(f.Password) && f.Password.Length < 12) return Problem422("Passwords need at least 12 characters.", "password");
        return null;
    }

    private async Task Apply(User user, UserForm f)
    {
        var role = await db.Roles.AsTracking().FirstAsync(r => r.Id == f.RoleId);   // tracked: same instance as user.Role
        user.Name = f.Name.Trim();
        user.Email = f.Email.Trim().ToLowerInvariant();
        user.EmployeeId = Blank(f.EmployeeId);
        user.Title = Blank(f.Title);
        user.Department = f.Department;
        user.ManagerId = f.ManagerId;
        user.PhoneExt = Blank(f.PhoneExt);
        user.Location = Blank(f.Location);
        user.HiredOn = f.HiredOn;
        user.Company = Blank(f.Company);
        user.AccessEndsOn = f.AccessEndsOn;
        user.RoleId = role.Id;
        user.Role = role;

        // Per-person access: "allow" adds what the role lacks, "deny" takes away what it has.
        user.ExtraPerms = [];
        user.DeniedPerms = [];
        foreach (var (perm, choice) in f.Access ?? [])
        {
            if (!AdminApps.IsPerm(perm)) continue;
            var inRole = role.Perms.Contains(perm);
            if (choice == "allow" && !inRole) user.ExtraPerms.Add(perm);
            else if (choice == "deny" && inRole) user.DeniedPerms.Add(perm);
        }
        user.AccessNote = user.ExtraPerms.Count + user.DeniedPerms.Count > 0 ? Blank(f.AccessNote) : null;

        if (!string.IsNullOrEmpty(f.Password))
        {
            user.Password = Passwords.Hash(f.Password);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [HttpGet("roles")]
    public async Task<IActionResult> Roles() => Ok(await db.Roles.OrderBy(r => r.Id)
        .Select(r => new { r.Id, r.Name, r.Perms, administrator = r.Name == "Administrator", users = r.Users.Count }).ToListAsync());

    /// <summary>The Roles matrix: roleId → perms for each role being saved. Administrator always keeps everything.</summary>
    [HttpPut("roles")]
    public async Task<IActionResult> SaveRoles(Dictionary<int, List<string>> perms)
    {
        var changed = new List<string>();
        foreach (var role in await db.Roles.AsTracking().ToListAsync())
        {
            // Only roles in the request change (a partial save never wipes the others); Administrator keeps everything.
            if (role.IsAdministrator || !perms.TryGetValue(role.Id, out var wanted)) continue;
            var next = AdminApps.AllPerms.Where(wanted.Contains).ToList();
            if (next.SequenceEqual(role.Perms)) continue;
            Audit(db, "role_changed", role.Name, $"added: {string.Join(", ", next.Except(role.Perms))}; removed: {string.Join(", ", role.Perms.Except(next))}");
            role.Perms = next;
            changed.Add(role.Name);
        }
        if (changed.Count == 0) return Ok(new { changed });

        // Everyone holding a changed role gets a new access version so their client refreshes.
        var roleNames = changed.ToHashSet();
        foreach (var u in await db.Users.AsTracking().Include(u => u.Role).Where(u => u.Role != null && roleNames.Contains(u.Role.Name)).ToListAsync())
            u.AccessVersion++;
        await db.SaveChangesAsync();
        await access.InvalidateAllAsync();
        return Ok(new { changed });
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> Timeline([FromQuery] string? q, [FromQuery] int? userId, [FromQuery] long? before)
    {
        var query = db.AuditEntries.AsQueryable();
        if (userId is { } uid) query = query.Where(a => a.UserId == uid);
        if (before is { } b) query = query.Where(a => a.Id < b);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(a => a.Actor.Contains(q) || a.Action.Contains(q) || (a.Subject != null && a.Subject.Contains(q)) || (a.Detail != null && a.Detail.Contains(q)));
        return Ok(await query.OrderByDescending(a => a.Id).Take(100).ToListAsync());
    }

    /// <summary>Drops every server-side cache entry (they rebuild on the next request).</summary>
    [HttpPost("cache/clear")]
    public async Task<IActionResult> ClearCache([FromQuery] string? tag)
    {
        string[] tags = tag is null ? ["*"] : [tag];
        await cache.RemoveByTagAsync(tags);
        Audit(db, "cache_cleared", tag ?? "all");
        await db.SaveChangesAsync();
        return Ok(new { cleared = tags });
    }
}
