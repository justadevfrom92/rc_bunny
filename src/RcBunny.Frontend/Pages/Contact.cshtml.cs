using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RcBunny.Data;

namespace RcBunny.Frontend.Pages;

/// <summary>Contact Us. Messages show up in the admin under Web Messages.</summary>
public class ContactModel(AppDbContext db) : PageModel
{
    public static readonly string[] Topics = ["New service", "Billing", "My plan", "Moving", "Something else"];

    public class ContactInput
    {
        [Required, MaxLength(100)] public string Name { get; set; } = "";
        [Required, EmailAddress, MaxLength(200)] public string Email { get; set; } = "";
        [MaxLength(30)] public string? Phone { get; set; }
        public string Topic { get; set; } = Topics[0];
        [Required, MaxLength(4000)] public string Message { get; set; } = "";
    }

    [BindProperty] public ContactInput Input { get; set; } = new();
    public bool Sent { get; private set; }

    /// <summary>"Sign Up" on a plan card comes here with ?plan=… and starts the message for them.</summary>
    public void OnGet(string? plan)
    {
        if (plan is null) return;
        Input.Message = $"I'd like to sign up for {plan}.";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        db.ContactMessages.Add(new ContactMessage
        {
            Name = Input.Name.Trim(), Email = Input.Email.Trim(), Phone = Input.Phone?.Trim(),
            Topic = Topics.Contains(Input.Topic) ? Input.Topic : Topics[^1], Message = Input.Message.Trim(),
        });
        await db.SaveChangesAsync();
        Sent = true;
        return Page();
    }
}
