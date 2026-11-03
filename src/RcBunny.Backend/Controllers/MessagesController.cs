using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;

namespace RcBunny.Controllers;

/// <summary>Ranger → Web Messages: what people sent from the website's Contact Us page.</summary>
[ApiController]
[Route("api/messages")]
[RequirePermission("ranger")]
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

    /// <summary>Turn a message into a ticket, so it can be worked and answered in Ranger.</summary>
    [HttpPost("{id:int}/ticket")]
    public async Task<IActionResult> MakeTicket(int id)
    {
        var message = await db.ContactMessages.FindAsync(id);
        if (message is null) return NotFound();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == message.Email);
        var ticket = new SupportTicket { CustomerId = customer?.Id, Name = message.Name, Email = message.Email, Subject = $"{message.Topic} (from the website)", Topic = message.Topic };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();
        db.TicketMessages.Add(new TicketMessage { TicketId = ticket.Id, Author = message.Name, FromCustomer = true, Body = message.Message, CreatedAt = message.CreatedAt });
        message.Handled = true;
        await db.SaveChangesAsync();
        return Ok(new { ticket.Id });
    }
}
