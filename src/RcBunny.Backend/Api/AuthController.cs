using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RcBunny.Backend.Access;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Api;

[Route("api/auth")]
public class AuthController(AppDbContext db, AccessService access, IWebHostEnvironment env, IConfiguration config) : ApiControllerBase
{
    public record LoginRequest(string Email, string Password);

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await db.Users.AsTracking().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == req.Email.Trim().ToLower());
        if (user is null || !Passwords.Verify(user.Password, req.Password))
            return Unauthorized(new { code = "bad_credentials", message = "That email and password don't match." });
        return await SignIn(user, null, "signed in");
    }

    /// <summary>Sample sign-ins for local testing (Development only, like ebunny's test_logins).</summary>
    [AllowAnonymous, HttpGet("test-logins")]
    public async Task<IActionResult> TestLogins()
    {
        if (!TestLoginsOn) return NotFound();
        var users = await db.Users.Include(u => u.Role).Where(u => u.Active).OrderBy(u => u.Company).ThenBy(u => u.Id)
            .Select(u => new { u.Id, u.Name, u.Email, role = u.Role!.Name, u.Company }).ToListAsync();
        return Ok(users);
    }

    [AllowAnonymous, HttpPost("test-logins/{id:int}")]
    public async Task<IActionResult> TestLogin(int id)
    {
        if (!TestLoginsOn) return NotFound();
        var user = await db.Users.AsTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? NotFound() : await SignIn(user, null, "signed in (test login)");
    }

    private bool TestLoginsOn => env.IsDevelopment() && config.GetValue("Admin:TestLogins", true);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>The client's copy of who's signed in and what they may do. Never cached by the browser.</summary>
    [HttpGet("me")]
    public IActionResult MeInfo()
    {
        Response.Headers.CacheControl = "no-store";
        var me = Me;
        return Ok(new
        {
            id = me.UserId, name = me.Name, email = me.Email, title = me.Title, role = me.RoleName,
            company = me.Company, external = me.IsExternal, accessEndsOn = me.AccessEndsOn,
            accessVersion = me.AccessVersion, perms = me.Perms,
            impersonatedBy = User.ImpersonatorId(),
            apps = AdminApps.All.Where(a => me.Has(a.Key)).Select(a => a.Key),
        });
    }

    /// <summary>Sheriff → Staff Directory → Log in as. Only to someone whose access is a subset of yours.</summary>
    [HttpPost("log-in-as/{id:int}"), RequirePerm("sheriff")]
    public async Task<IActionResult> LogInAs(int id)
    {
        var target = await access.GetAsync(id);
        if (target is null) return NotFound();
        if (AccessService.CannotLogInAs(Me, target) is { } why) return Problem422(why);
        var user = await db.Users.AsTracking().Include(u => u.Role).FirstAsync(u => u.Id == id);
        Audit(db, "log_in_as", user.Name);
        return await SignIn(user, User.ImpersonatorId() ?? Me.UserId, $"logged in as by {Me.Name}");
    }

    /// <summary>Back to yourself after "Log in as".</summary>
    [HttpPost("return")]
    public async Task<IActionResult> Return()
    {
        if (User.ImpersonatorId() is not { } original) return Problem422("You aren't logged in as someone else.");
        var user = await db.Users.AsTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == original);
        if (user is null) return await Logout();
        return await SignIn(user, null, "returned from log in as");
    }

    private async Task<IActionResult> SignIn(User user, int? impersonator, string action)
    {
        var snap = AccessService.Build(user);
        if (!snap.CanSignIn)
            return Unauthorized(new { code = "disabled", message = user.Active ? "Your access has ended." : "This account is disabled." });

        var claims = new List<Claim>
        {
            new(Claims.UserId, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(Claims.Stamp, user.SecurityStamp),
        };
        if (impersonator is { } imp) claims.Add(new Claim(Claims.Impersonator, imp.ToString()));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        if (impersonator is null) user.LastLoginAt = DateTime.UtcNow;
        db.AuditEntries.Add(new AuditEntry
        {
            UserId = user.Id, Actor = user.Name, Action = "sign_in", Detail = action,
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
        });
        await db.SaveChangesAsync();
        return Ok(new { id = user.Id, name = user.Name });
    }
}
