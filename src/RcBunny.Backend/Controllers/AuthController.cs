using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

[ApiController]
[Route("api")]
public class AuthController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    public record LoginRequest(string Email, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLower());
        if (user is null || !user.Active || !Passwords.Verify(user.PasswordHash, request.Password))
            return BadRequest(new { error = "That email and password don't match." });
        if (user.AccessEndsOn < DateOnly.FromDateTime(DateTime.Today))
            return BadRequest(new { error = "Your access to the admin has ended. Ask your contact at Energy Texas." });
        user.LastLoginAt = DateTime.UtcNow;
        Audit.Add(db, user.Name, "sign-in", $"{user.Name} signed in.");
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.User = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(HttpContext.User);
        return Ok(await Permissions.CurrentAsync(HttpContext));
    }

    /// <summary>Settings → Switch User (Development only): sign in as another sample user.</summary>
    [HttpPost("login/test/{id:int}")]
    public async Task<IActionResult> LoginTest(int id)
    {
        if (!env.IsDevelopment() || await Permissions.CurrentAsync(HttpContext) is null) return NotFound();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Active);
        if (user is null) return NotFound();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.User = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(HttpContext.User);
        return Ok(await Permissions.CurrentAsync(HttpContext));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return Ok();
    }

    /// <summary>Who's signed in and what they can do. The admin hides menus and buttons with this; the server still checks every call.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var me = await Permissions.CurrentAsync(HttpContext);
        return me is null ? Unauthorized() : Ok(me);
    }

    /// <summary>Test sign-ins shown on the login page (Development only): every active employee, with their role and apps.</summary>
    [HttpGet("demo-logins")]
    public async Task<object> DemoLogins()
    {
        if (!env.IsDevelopment()) return Array.Empty<object>();
        var users = await db.Users.Include(u => u.Role).Where(u => u.Active).OrderBy(u => u.Id).ToListAsync();
        return users.Select(u => new { u.Email, u.Name, role = u.Role!.Name, permissions = u.PermissionList() });
    }
}
