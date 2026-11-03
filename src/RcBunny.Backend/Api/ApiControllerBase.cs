using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RcBunny.Backend.Access;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Backend.Api;

/// <summary>Every API controller: signed-in by default, JSON, with the caller's access snapshot at hand.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The signed-in person's access (set by CookieEvents.ValidatePrincipal for this request).</summary>
    protected AccessSnapshot Me => HttpContext.Items[typeof(AccessSnapshot)] as AccessSnapshot
        ?? throw new InvalidOperationException("No signed-in user on this request.");

    protected bool Can(string perm) => Me.Has(perm);

    protected ObjectResult Problem422(string message, string? field = null) =>
        UnprocessableEntity(new { code = "invalid", message, field });

    protected ObjectResult Forbidden(string perm) =>
        StatusCode(StatusCodes.Status403Forbidden, new { code = "missing_perm", perm, message = $"You need {AdminApps.Label(perm)} access for that." });

    protected void Audit(AppDbContext db, string action, string? subject = null, string? detail = null) =>
        db.AuditEntries.Add(new AuditEntry
        {
            UserId = Me.UserId,
            Actor = User.ImpersonatorId() is { } imp ? $"{Me.Name} (by #{imp})" : Me.Name,
            Action = action, Subject = subject, Detail = detail,
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
        });
}
