using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Corral → SMS and Phone Calls. No text provider is connected yet, so sent texts are logged only.</summary>
[ApiController]
[Route("api")]
[RequirePermission("corral")]
public class CommunicationsController(AppDbContext db) : ControllerBase
{
    [HttpGet("texts")]
    public async Task<object> Texts() => await db.TextMessages.OrderByDescending(t => t.CreatedAt).Take(200)
        .Select(t => new { t.Id, t.CustomerId, customer = db.Customers.Where(c => c.Id == t.CustomerId).Select(c => c.Name).FirstOrDefault(), t.Phone, t.Body, t.Direction, t.Author, t.CreatedAt })
        .ToListAsync();

    public record NewText(int? CustomerId, string Phone, string Body);

    [HttpPost("texts")]
    public async Task<IActionResult> SendText(NewText text)
    {
        if (string.IsNullOrWhiteSpace(text.Phone) || string.IsNullOrWhiteSpace(text.Body)) return BadRequest(new { error = "Enter a phone number and a message." });
        if (text.Body.Length > 320) return BadRequest(new { error = "Keep texts to 320 characters." });
        var me = await Permissions.CurrentAsync(HttpContext);
        db.TextMessages.Add(new TextMessage { CustomerId = text.CustomerId, Phone = text.Phone.Trim(), Body = text.Body.Trim(), Author = me!.Name });
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("calls")]
    public async Task<object> Calls() => await db.PhoneCalls.OrderByDescending(c => c.CreatedAt).Take(200)
        .Select(c => new { c.Id, c.CustomerId, customer = db.Customers.Where(x => x.Id == c.CustomerId).Select(x => x.Name).FirstOrDefault(), c.Phone, c.Direction, c.Minutes, c.Outcome, c.Notes, c.Author, c.CreatedAt })
        .ToListAsync();

    public record NewCall(int? CustomerId, string Phone, string Direction, int Minutes, string Outcome, string? Notes);

    [HttpPost("calls")]
    public async Task<IActionResult> LogCall(NewCall call)
    {
        if (string.IsNullOrWhiteSpace(call.Phone) || string.IsNullOrWhiteSpace(call.Outcome)) return BadRequest(new { error = "Enter the phone number and what happened." });
        var me = await Permissions.CurrentAsync(HttpContext);
        db.PhoneCalls.Add(new PhoneCall
        {
            CustomerId = call.CustomerId, Phone = call.Phone.Trim(), Direction = call.Direction == "out" ? "out" : "in",
            Minutes = Math.Max(0, call.Minutes), Outcome = call.Outcome.Trim(), Notes = call.Notes?.Trim(), Author = me!.Name,
        });
        await db.SaveChangesAsync();
        return Ok();
    }
}
