using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Frontend.Pages.MyAccount;

/// <summary>My Account → Support: open a ticket, read our replies, answer back, and rate us when it's solved. Staff work these in Ranger.</summary>
public class SupportModel(AppDbContext db, IConfiguration config, IWebHostEnvironment env) : PageModel
{

    public List<SupportTicket> Tickets { get; private set; } = [];
    public SupportTicket? Ticket { get; private set; }
    public List<TicketMessage> Messages { get; private set; } = [];
    public List<TicketAttachment> Files { get; private set; } = [];
    public string? Error { get; private set; }

    private int CustomerId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Tickets = await db.SupportTickets.Where(t => t.CustomerId == CustomerId).OrderByDescending(t => t.UpdatedAt).ToListAsync();
        if (id is not null)
        {
            Ticket = Tickets.FirstOrDefault(t => t.Id == id);
            if (Ticket is null) return NotFound();
            Messages = (await db.TicketMessages.Where(m => m.TicketId == id).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync()).Where(m => m.CustomerSees).ToList();
            Files = await db.TicketAttachments.Where(a => a.TicketId == id).ToListAsync();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostNewAsync(string subject, string topic, string message, List<IFormFile>? files)
    {
        if (BadFiles(files) is { } bad) { Error = bad; return await OnGetAsync(null); }
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message)) { Error = "Add a subject and a message."; return await OnGetAsync(null); }
        var customer = await db.Customers.FirstAsync(c => c.Id == CustomerId);
        var ticket = new SupportTicket
        {
            CustomerId = customer.Id, Name = customer.Name, Email = customer.Email ?? "",
            Subject = subject.Trim()[..Math.Min(200, subject.Trim().Length)], Topic = Ranger.Topics.ContainsKey(topic) ? topic : "other",
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();
        await PostAsync(ticket, message, files);
        return Redirect($"/myaccount/support/{ticket.Id}");
    }

    public async Task<IActionResult> OnPostReplyAsync(int id, string message, List<IFormFile>? files)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id && t.CustomerId == CustomerId);
        if (ticket is null) return NotFound();
        if (BadFiles(files) is { } bad) { Error = bad; return await OnGetAsync(id); }
        if (!string.IsNullOrWhiteSpace(message)) await PostAsync(ticket, message, files);
        return Redirect($"/myaccount/support/{id}");
    }

    /// <summary>The file itself (only the customer's own tickets).</summary>
    public async Task<IActionResult> OnGetFileAsync(int id, int fileId)
    {
        var file = await db.TicketAttachments.FirstOrDefaultAsync(a => a.Id == fileId && a.TicketId == id && db.SupportTickets.Any(t => t.Id == id && t.CustomerId == CustomerId));
        var full = file == null ? null : Path.Combine(Storage, file.Path);
        return file == null || !System.IO.File.Exists(full) ? NotFound() : PhysicalFile(full, file.Mime, file.Name);
    }

    /// <summary>The customer's message (it moves a waiting or solved ticket back to us), with up to 5 files.</summary>
    private async Task PostAsync(SupportTicket ticket, string message, List<IFormFile>? files)
    {
        var m = Ranger.Post(db, ticket, "customer", ticket.Name, message.Trim());
        await db.SaveChangesAsync();
        foreach (var f in files ?? [])
        {
            await using var stream = f.OpenReadStream();
            db.TicketAttachments.Add(await Ranger.SaveFileAsync(Storage, m, f.FileName, stream, null, true));
        }
        await db.SaveChangesAsync();
    }

    private static string? BadFiles(List<IFormFile>? files) =>
        files is { Count: > 5 } ? "Attach up to 5 files." :
        files?.Any(f => f.Length is 0 or > 10_485_760 || !Ranger.AttachmentTypes.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())) == true
            ? "Files can be PDFs, pictures, text, Word or Excel, up to 10 MB each." : null;

    private string Storage => Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage"));

    public async Task<IActionResult> OnPostRateAsync(int id, string rating, string? comment)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id && t.CustomerId == CustomerId && (t.Status == "solved" || t.Status == "closed"));
        if (ticket is null) return NotFound();
        ticket.Rating = rating == "good" ? "good" : "bad";
        ticket.RatingComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        ticket.RatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Redirect($"/myaccount/support/{id}");
    }
}
