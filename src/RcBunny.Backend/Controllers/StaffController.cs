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
    [HttpGet]
    public async Task<object> List() => new
    {
        users = await db.Users.Include(u => u.Role).OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Email, u.RoleId, role = u.Role!.Name, u.Active }).ToListAsync(),
        roles = (await db.Roles.OrderBy(r => r.Name).ToListAsync())
            .Select(r => new { r.Id, r.Name, permissions = r.PermissionList(), people = db.Users.Count(u => u.RoleId == r.Id) }),
        permissions = Permissions.All,
    };

    public record NewUser(string Name, string Email, string Password, int RoleId);

    [HttpPost]
    public async Task<IActionResult> Add(NewUser user)
    {
        var email = user.Email.Trim().ToLower();
        if (string.IsNullOrWhiteSpace(user.Name) || !email.Contains('@')) return BadRequest(new { error = "Enter a name and email." });
        if (user.Password.Length < 8) return BadRequest(new { error = "Use a password of at least 8 characters." });
        if (await db.Users.AnyAsync(u => u.Email == email)) return BadRequest(new { error = "Someone already has that email." });
        db.Users.Add(new User { Name = user.Name.Trim(), Email = email, PasswordHash = Passwords.Hash(user.Password), RoleId = user.RoleId });
        Audit.Add(db, await MyName(), "access", $"Added {user.Name.Trim()} ({email}).");
        await db.SaveChangesAsync();
        return Ok();
    }

    public record UserUpdate(int RoleId, bool Active);

    /// <summary>Change someone's role or turn their sign-in off. Takes effect on their next click.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UserUpdate update)
    {
        var me = await Permissions.CurrentAsync(HttpContext);
        if (id == me!.Id && !update.Active) return BadRequest(new { error = "You can't turn off your own sign-in." });
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (user.RoleId != update.RoleId)
            Audit.Add(db, me.Name, "access", $"Changed {user.Name}'s role from {user.Role!.Name} to {(await db.Roles.FindAsync(update.RoleId))?.Name}.");
        if (user.Active != update.Active)
            Audit.Add(db, me.Name, "access", $"{(update.Active ? "Turned on" : "Turned off")} {user.Name}'s sign-in.");
        user.RoleId = update.RoleId;
        user.Active = update.Active;
        await db.SaveChangesAsync();
        cache.Remove(Permissions.CacheKey(id));
        return Ok();
    }

    public record NewPassword(string Password);

    [HttpPost("{id:int}/password")]
    public async Task<IActionResult> SetPassword(int id, NewPassword input)
    {
        if (input.Password.Length < 8) return BadRequest(new { error = "Use a password of at least 8 characters." });
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.PasswordHash = Passwords.Hash(input.Password);
        Audit.Add(db, await MyName(), "access", $"Set a new password for {user.Name}.");
        await db.SaveChangesAsync();
        return Ok();
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
