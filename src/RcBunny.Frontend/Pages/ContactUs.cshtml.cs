using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RcBunny.Data;
using RcBunny.Data.Entities;
using RcBunny.Frontend.Services;

namespace RcBunny.Frontend.Pages;

/// <summary>
/// Contact Us → Ranger → Web Messages in the admin. The page itself is never personalised, so the GET is
/// output-cached like the other marketing pages; the POST is rate limited per IP.
/// It's an anonymous form that changes nothing for anyone signed in, so it doesn't use an antiforgery token
/// (which would make every page view set a cookie and defeat the page cache).
/// </summary>
[IgnoreAntiforgeryToken]
[Microsoft.AspNetCore.OutputCaching.OutputCache(PolicyName = "Public")]
public class ContactUsModel(AppDbContext db, Catalog catalog) : PageModel
{
    public static readonly (string Value, string Label)[] Topics =
        [("general", "General question"), ("signup", "Sign up for service"), ("billing", "Billing & payments"), ("business", "Business service"), ("rewards", "Rewards")];

    public sealed class ContactForm
    {
        [Required, MaxLength(100)] public string? First { get; set; }
        [Required, MaxLength(100)] public string? Last { get; set; }
        [Required, EmailAddress, MaxLength(200)] public string? Email { get; set; }
        [MaxLength(40)] public string? Phone { get; set; }
        [MaxLength(40)] public string? Topic { get; set; } = "general";
        [Required, MaxLength(5000)] public string? Message { get; set; }
    }

    [BindProperty] public ContactForm Form { get; set; } = new();
    public bool Sent { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(string? topic, string? plan)
    {
        if (Topics.Any(t => t.Value == topic)) Form.Topic = topic;
        if (plan is not null && (await catalog.GetAsync()).Plan(plan) is { } p)
        {
            Form.Topic = "signup";
            Form.Message = $"I'd like to sign up for {p.Name}.";
        }
        else if (plan == "BYOP")
        {
            Form.Topic = "signup";
            Form.Message = "I'd like to sign up for a Build Your Own Plan.";
        }
    }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync()
    {
        var wantsJson = Request.Headers.Accept.ToString().Contains("application/json");
        if (!ModelState.IsValid || !Topics.Any(t => t.Value == Form.Topic))
        {
            Error = "Please fill in your name, a valid email and a message.";
            return wantsJson ? new JsonResult(new { message = Error }) { StatusCode = 422 } : Page();
        }

        db.ContactMessages.Add(new ContactMessage
        {
            FirstName = Form.First!.Trim(), LastName = Form.Last!.Trim(), Email = Form.Email!.Trim(), Phone = Form.Phone?.Trim(),
            Topic = Form.Topic!, Message = Form.Message!.Trim(), Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
        });
        await db.SaveChangesAsync();

        if (wantsJson) return new JsonResult(new { ok = true }) { StatusCode = 201 };
        Sent = true;
        Form = new ContactForm();
        return Page();
    }
}
