using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Sheriff → Staff Directory and Roles.</summary>
[ApiController]
[Route("api/staff")]
[RequirePermission("sheriff")]
public class StaffController(AppDbContext db, IMemoryCache cache) : ControllerBase
{
    /// <summary>The apps a person can open (key → name, like ebunny's launcher) and the extra rights.</summary>
    public static readonly Dictionary<string, string> Apps = new()
    {
        ["corral"] = "Corral", ["ranger"] = "Ranger", ["plans"] = "Astro", ["caboose"] = "Caboose", ["bounty"] = "Bounty", ["sheriff"] = "Sheriff",
    };
    public static readonly Dictionary<string, string> Rights = new() { ["refunds"] = "Refunds & reversals", ["delete"] = "Delete accounts and records" };
    public static readonly string[] Departments = ["Customer Service", "Pricing", "Marketing", "Finance", "AI Operations", "IT", "Management"];

    private object Row(User u) => new
    {
        u.Id, u.Name, u.Email, u.Title, u.EmployeeId, u.PhoneExt, u.Department, u.Company, u.AccessEndsOn, u.AccessNote, u.LastLoginAt, u.Active,
        role = u.Role?.Name, manager = u.Manager?.Name, has = u.PermissionList(), added = u.ExtraList(), removed = u.DeniedList(),
    };

    /// <summary>Staff Directory (ebunny's sheriff/users): employees by department, 3rd-party users by company, then disabled people.</summary>
    [HttpGet]
    public async Task<object> List()
    {
        var users = await db.Users.Include(u => u.Role).Include(u => u.Manager).OrderBy(u => u.Name).ToListAsync();
        return new
        {
            groups = users.Where(u => u.Active && u.Company == null).GroupBy(u => u.Department ?? "No department").OrderBy(g => g.Key)
                .Select(g => new { name = g.Key, external = false, people = g.Select(Row) })
                .Concat(users.Where(u => u.Active && u.Company != null).GroupBy(u => u.Company!).OrderBy(g => g.Key)
                    .Select(g => new { name = g.Key, external = true, people = g.Select(Row) })),
            disabled = users.Where(u => !u.Active).Select(Row),
            apps = Apps, rights = Rights,
            me = (await Permissions.CurrentAsync(HttpContext))!.Id,
            // Roles → Roles page
            roles = (await db.Roles.OrderBy(r => r.Name).ToListAsync())
                .Select(r => new { r.Id, r.Name, permissions = r.PermissionList(), people = users.Count(u => u.RoleId == r.Id) }),
            permissions = Permissions.All,
        };
    }

    /// <summary>One person's profile (ebunny's sheriff/user-show).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var u = await db.Users.Include(x => x.Role).Include(x => x.Manager).FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();
        return Ok(new
        {
            user = new { u.Id, u.Name, u.Email, u.Title, u.EmployeeId, u.PhoneExt, u.Department, u.Company, u.Location, u.HiredOn, u.AccessEndsOn, u.AccessNote, u.LastLoginAt, u.Active,
                role = u.Role?.Name, u.ManagerId, manager = u.Manager?.Name, has = u.PermissionList(), added = u.ExtraList(), removed = u.DeniedList() },
            reports = await db.Users.Where(x => x.ManagerId == id).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Title, x.Company }).ToListAsync(),
            logins = await db.AuditEntries.Where(a => a.Category == "sign-in" && a.Who == u.Name).OrderByDescending(a => a.CreatedAt).Take(5).Select(a => new { a.CreatedAt, summary = a.What }).ToListAsync(),
            activity = await db.History.Where(h => h.UserId == id).OrderByDescending(h => h.CreatedAt).Take(10).Select(h => new { h.Id, h.CreatedAt, h.Summary }).ToListAsync(),
            apps = Apps, rights = Rights,
        });
    }

    /// <summary>Everything a person changed (ebunny's users/{user}/history).</summary>
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id, int page = 1)
    {
        var u = await db.Users.FindAsync(id);
        if (u is null) return NotFound();
        var q = db.History.Where(h => h.UserId == id);
        return Ok(new
        {
            user = new { u.Id, u.Name, u.Title, u.Department, u.Company }, page, total = await q.CountAsync(),
            items = await q.OrderByDescending(h => h.CreatedAt).Skip((Math.Max(page, 1) - 1) * 25).Take(25)
                .Select(h => new { h.Id, h.CreatedAt, h.Model, h.Action, h.Summary, account = h.CustomerId == null ? null : db.Customers.Where(c => c.Id == h.CustomerId).Select(c => c.Account).FirstOrDefault() })
                .ToListAsync(),
        });
    }

    /// <summary>The New User / Edit form (ebunny's sheriff/user-form).</summary>
    [HttpGet("form")]
    public async Task<object> Form(int? id) => new
    {
        user = id == null ? null : await db.Users.Where(u => u.Id == id).Select(u => new
        {
            u.Id, u.Name, u.Email, u.Title, u.EmployeeId, u.PhoneExt, u.Department, u.Company, u.Location, u.HiredOn, u.AccessEndsOn, u.AccessNote, u.Active,
            u.RoleId, u.ManagerId, u.ExtraPermissions, u.DeniedPermissions,
        }).FirstOrDefaultAsync(),
        me = (await Permissions.CurrentAsync(HttpContext))!.Id,
        roles = (await db.Roles.OrderBy(r => r.Id).ToListAsync()).Select(r => new { r.Id, r.Name, permissions = r.PermissionList() }),
        managers = await db.Users.Where(u => u.Active && u.Id != id).OrderBy(u => u.Name).Select(u => new { u.Id, u.Name, u.Title }).ToListAsync(),
        departments = Departments, apps = Apps, rights = Rights,
    };

    public record UserInput(string Name, string Email, string? Password, int RoleId, string? Company, DateOnly? AccessEndsOn, string? EmployeeId, string? Title,
        string? Department, int? ManagerId, string? Location, string? PhoneExt, DateOnly? HiredOn, Dictionary<string, string>? Access, string? AccessNote);

    [HttpPost]
    public async Task<IActionResult> Add(UserInput input)
    {
        if (string.IsNullOrEmpty(input.Password) || input.Password.Length < 12) return BadRequest(new { error = "The temporary password needs at least 12 characters." });
        var user = new User();
        if (Fill(user, input) is { } problem) return problem;
        if (await db.Users.AnyAsync(u => u.Email == user.Email)) return BadRequest(new { error = "Someone already has that email." });
        db.Users.Add(user);
        Audit.Add(db, await MyName(), "access", $"Added {user.Name} ({user.Email}).");
        await db.SaveChangesAsync();
        return Ok(new { user.Id, message = $"{user.Name} added." });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UserInput input)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (!string.IsNullOrEmpty(input.Password) && input.Password.Length < 12) return BadRequest(new { error = "A new password needs at least 12 characters." });
        if (Fill(user, input) is { } problem) return problem;
        if (await db.Users.AnyAsync(u => u.Email == user.Email && u.Id != id)) return BadRequest(new { error = "Someone already has that email." });
        Audit.Add(db, await MyName(), "access", $"Updated {user.Name}.");
        await db.SaveChangesAsync();
        cache.Remove(Permissions.CacheKey(id));
        return Ok(new { user.Id, message = $"{user.Name} saved." });
    }

    /// <summary>Disable or enable someone's sign-in. Takes effect on their next click.</summary>
    [HttpPost("{id:int}/active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var me = (await Permissions.CurrentAsync(HttpContext))!;
        if (id == me.Id) return BadRequest(new { error = "You can't turn off your own sign-in." });
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.Active = !user.Active;
        Audit.Add(db, me.Name, "access", $"{(user.Active ? "Enabled" : "Disabled")} {user.Name}'s sign-in.");
        await db.SaveChangesAsync();
        cache.Remove(Permissions.CacheKey(id));
        return Ok(new { message = user.Active ? $"{user.Name} can sign in again." : $"{user.Name} can't sign in now." });
    }

    private BadRequestObjectResult? Fill(User u, UserInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Email) || !input.Email.Contains('@')) return BadRequest(new { error = "Enter a name and email." });
        u.Name = input.Name.Trim(); u.Email = input.Email.Trim().ToLower(); u.RoleId = input.RoleId;
        if (!string.IsNullOrEmpty(input.Password)) u.PasswordHash = Passwords.Hash(input.Password);
        string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        u.Company = Clean(input.Company); u.AccessEndsOn = input.AccessEndsOn; u.EmployeeId = Clean(input.EmployeeId); u.Title = Clean(input.Title);
        u.Department = Departments.Contains(input.Department) ? input.Department : null; u.ManagerId = input.ManagerId == u.Id ? null : input.ManagerId;
        u.Location = Clean(input.Location); u.PhoneExt = Clean(input.PhoneExt); u.HiredOn = input.HiredOn; u.AccessNote = Clean(input.AccessNote);
        var access = input.Access ?? [];
        u.ExtraPermissions = string.Join(",", access.Where(a => a.Value == "allow" && Permissions.All.ContainsKey(a.Key)).Select(a => a.Key));
        u.DeniedPermissions = string.Join(",", access.Where(a => a.Value == "deny" && Permissions.All.ContainsKey(a.Key)).Select(a => a.Key));
        return null;
    }

    public record RoleInput(string Name, List<string> Permissions);

    [HttpPost("roles")]
    public async Task<IActionResult> AddRole(RoleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { error = "Name the role." });
        if (await db.Roles.AnyAsync(r => r.Name == input.Name.Trim())) return BadRequest(new { error = "There's already a role with that name." });
        db.Roles.Add(new Role { Name = input.Name.Trim(), Permissions = Clean(input.Permissions) });
        Audit.Add(db, await MyName(), "access", $"Added the {input.Name.Trim()} role.");
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("roles/{id:int}")]
    public async Task<IActionResult> UpdateRole(int id, RoleInput input)
    {
        var role = await db.Roles.FindAsync(id);
        if (role is null) return NotFound();
        role.Permissions = Clean(input.Permissions);
        Audit.Add(db, await MyName(), "access", $"{role.Name} can now use: {(role.Permissions == "" ? "nothing" : role.Permissions.Replace(",", ", "))}.");
        await db.SaveChangesAsync();
        foreach (var userId in await db.Users.Where(u => u.RoleId == id).Select(u => u.Id).ToListAsync())
            cache.Remove(Permissions.CacheKey(userId));
        return Ok();
    }

    private static string Clean(List<string> permissions) => string.Join(",", permissions.Where(Permissions.All.ContainsKey).Distinct());

    private async Task<string> MyName() => (await Permissions.CurrentAsync(HttpContext))!.Name;
}
