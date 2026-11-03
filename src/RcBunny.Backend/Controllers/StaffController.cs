using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

[ApiController]
[Route("api/staff")]
[RequirePermission("staff")]
public class StaffController(AppDbContext db, IMemoryCache cache) : ControllerBase
{
    [HttpGet]
    public async Task<object> List() => new
    {
        users = await db.Users.Include(u => u.Role).OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Email, u.RoleId, role = u.Role!.Name, u.Active }).ToListAsync(),
        roles = (await db.Roles.OrderBy(r => r.Name).ToListAsync()).Select(r => new { r.Id, r.Name, permissions = r.PermissionList() }),
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
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.RoleId = update.RoleId;
        user.Active = update.Active;
        await db.SaveChangesAsync();
        cache.Remove(Permissions.CacheKey(id));
        return Ok();
    }

    public record RoleUpdate(List<string> Permissions);

    [HttpPut("roles/{id:int}")]
    public async Task<IActionResult> UpdateRole(int id, RoleUpdate update)
    {
        var role = await db.Roles.FindAsync(id);
        if (role is null) return NotFound();
        role.Permissions = string.Join(",", update.Permissions.Where(Permissions.All.ContainsKey).Distinct());
        await db.SaveChangesAsync();
        foreach (var userId in await db.Users.Where(u => u.RoleId == id).Select(u => u.Id).ToListAsync())
            cache.Remove(Permissions.CacheKey(userId));
        return Ok();
    }
}
