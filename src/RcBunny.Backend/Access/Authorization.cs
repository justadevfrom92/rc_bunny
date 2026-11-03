using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace RcBunny.Backend.Access;

public static class Claims
{
    public const string UserId = ClaimTypes.NameIdentifier;
    public const string Stamp = "rcb:stamp";
    /// <summary>Set while an admin is logged in as someone else: the admin's own user id.</summary>
    public const string Impersonator = "rcb:imp";
}

/// <summary>[RequirePerm("corral")] on a controller or action: the person must have that app or right.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermAttribute(string perm) : AuthorizeAttribute(PermPolicy.Prefix + perm)
{
    public string Perm { get; } = perm;
}

public sealed record PermRequirement(string Perm) : IAuthorizationRequirement;

/// <summary>Turns "perm:xyz" policy names into a requirement, so there's no list of policies to keep in sync.</summary>
public sealed class PermPolicy(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
            return await base.GetPolicyAsync(policyName);
        return new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new PermRequirement(policyName[Prefix.Length..]))
            .Build();
    }
}

/// <summary>Checks the requirement against the cached access snapshot (never against claims, which can be stale).</summary>
public sealed class PermHandler(AccessService access, IHttpContextAccessor http) : AuthorizationHandler<PermRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermRequirement requirement)
    {
        var id = context.User.UserId();
        if (id is null) return;
        var snap = await access.GetAsync(id.Value);
        if (snap?.Has(requirement.Perm) == true)
            context.Succeed(requirement);
        else if (http.HttpContext is { } ctx)
            ctx.Items["rcb:missing-perm"] = requirement.Perm;
    }
}

public static class PrincipalExtensions
{
    public static int? UserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(Claims.UserId), out var id) ? id : null;

    public static int? ImpersonatorId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(Claims.Impersonator), out var id) ? id : null;
}

/// <summary>
/// Runs on every request that carries the sign-in cookie: the person must still exist, be active, be within
/// their access dates, and the cookie's security stamp must match (password change / disable kills old sign-ins).
/// Cheap, because the snapshot comes from cache.
/// </summary>
public sealed class CookieEvents(AccessService access) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.UserId();
        var snap = id is null ? null : await access.GetAsync(id.Value);
        if (snap is null || !snap.CanSignIn || snap.SecurityStamp != context.Principal!.FindFirstValue(Claims.Stamp))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.HttpContext.Response.Headers["X-Signed-Out"] = snap is null ? "gone" : !snap.CanSignIn ? "disabled" : "stamp";
            return;
        }
        // Lets the client notice a permission change on its very next call (see ClientApp/src/auth).
        context.HttpContext.Response.Headers["X-Access-Version"] = snap.AccessVersion.ToString();
        context.HttpContext.Items[typeof(AccessSnapshot)] = snap;
    }

    // An API never redirects to a login page: 401 / 403 with a JSON body the client understands.
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new { code = "signed_out", message = "Please sign in." });
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        var perm = context.HttpContext.Items["rcb:missing-perm"] as string;
        return context.Response.WriteAsJsonAsync(new
        {
            code = "missing_perm",
            perm,
            message = perm is null ? "You don't have access to that." : $"You need {AdminApps.Label(perm)} access for that.",
        });
    }
}
