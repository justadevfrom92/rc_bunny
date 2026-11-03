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

    /// <summary>Sample sign-ins shown on the login page, in Development only.</summary>
    [HttpGet("demo-logins")]
    public object DemoLogins() => env.IsDevelopment()
        ? new[] { "admin@example.com", "csr@example.com", "pricing@example.com", "finance@example.com" }
        : Array.Empty<string>();
}
