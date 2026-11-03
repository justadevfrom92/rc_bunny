using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Messages sent from the website's Contact Us page.</summary>
[ApiController]
[Route("api/messages")]
[RequirePermission("messages")]
public class MessagesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<ContactMessage>> List(bool handled = false) =>
        await db.ContactMessages.Where(m => m.Handled == handled).OrderByDescending(m => m.CreatedAt).Take(200).ToListAsync();

    [HttpPost("{id:int}/handled")]
    public async Task<IActionResult> MarkHandled(int id)
    {
        var message = await db.ContactMessages.FindAsync(id);
        if (message is null) return NotFound();
        message.Handled = true;
        await db.SaveChangesAsync();
        return Ok();
    }
}
