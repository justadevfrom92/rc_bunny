using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Frontend.Pages.MyAccount;

/// <summary>My Account → Support: open a ticket, read our replies, answer back, and rate us when it's solved. Staff work these in Ranger.</summary>
public class SupportModel(AppDbContext db) : PageModel
{
    public static readonly string[] Topics = ["Billing", "My plan", "Moving", "Something else"];

    public List<SupportTicket> Tickets { get; private set; } = [];
    public SupportTicket? Ticket { get; private set; }
    public List<TicketMessage> Messages { get; private set; } = [];
    public string? Error { get; private set; }

    private int CustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Tickets = await db.SupportTickets.Where(t => t.CustomerId == CustomerId).OrderByDescending(t => t.UpdatedAt).ToListAsync();
        if (id is not null)
        {
            Ticket = Tickets.FirstOrDefault(t => t.Id == id);
            if (Ticket is null) return NotFound();
            Messages = await db.TicketMessages.Where(m => m.TicketId == id && !m.Internal).OrderBy(m => m.CreatedAt).ToListAsync();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostNewAsync(string subject, string topic, string message)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message)) { Error = "Add a subject and a message."; return await OnGetAsync(null); }
        var customer = await db.Customers.FirstAsync(c => c.Id == CustomerId);
        var ticket = new SupportTicket
        {
            CustomerId = customer.Id, Name = customer.Name, Email = customer.Email ?? "",
            Subject = subject.Trim()[..Math.Min(200, subject.Trim().Length)], Topic = Topics.Contains(topic) ? topic : Topics[^1],
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();
        db.TicketMessages.Add(new TicketMessage { TicketId = ticket.Id, Author = customer.Name, FromCustomer = true, Body = message.Trim() });
        await db.SaveChangesAsync();
        return Redirect($"/myaccount/support/{ticket.Id}");
    }

    public async Task<IActionResult> OnPostReplyAsync(int id, string message)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id && t.CustomerId == CustomerId);
        if (ticket is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(message))
        {
            db.TicketMessages.Add(new TicketMessage { TicketId = id, Author = ticket.Name, FromCustomer = true, Body = message.Trim() });
            ticket.Status = "open"; // back to us
            ticket.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return Redirect($"/myaccount/support/{id}");
    }

    public async Task<IActionResult> OnPostRateAsync(int id, string rating, string? comment)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id && t.CustomerId == CustomerId && (t.Status == "solved" || t.Status == "closed"));
        if (ticket is null) return NotFound();
        ticket.Rating = rating == "good" ? "good" : "bad";
        ticket.RatingComment = comment?.Trim();
        await db.SaveChangesAsync();
        return Redirect($"/myaccount/support/{id}");
    }
}
