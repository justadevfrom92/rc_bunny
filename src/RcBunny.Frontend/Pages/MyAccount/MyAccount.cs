using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Frontend.Pages.MyAccount;

public class LoginModel(AppDbContext db) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; private set; }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        var name = Username.Trim().ToLower();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Username == name || c.Email == name);
        if (customer is null || !Passwords.Verify(customer.PasswordHash, Password))
        {
            Error = "That username and password don't match.";
            return Page();
        }
        // The sign-in goes in the account's Logins log (Corral → account → Logs)
        db.ActorName = customer.Name;
        db.Record(customer.Id, "Logins", "ItemLogin_model", "Customer", customer.Id, "signed in", $"Signed in to My Account from {HttpContext.Connection.RemoteIpAddress}");
        await db.SaveChangesAsync();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString())], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/myaccount");
    }
}

public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync();
        return Redirect("/");
    }
}

/// <summary>The signed-in customer's page: balance, plan, bills and payments.</summary>
public class IndexModel(AppDbContext db) : PageModel
{
    public Customer Customer { get; private set; } = null!;
    public List<Bill> Bills { get; private set; } = [];
    public List<Payment> Payments { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var customer = await db.Customers.Include(c => c.Plan).FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null) return Redirect("/myaccount/login");
        Customer = customer;
        Bills = await db.Bills.Where(b => b.CustomerId == id).OrderByDescending(b => b.BilledOn).ToListAsync();
        Payments = await db.Payments.Where(p => p.CustomerId == id).OrderByDescending(p => p.PaidOn).ToListAsync();
        return Page();
    }
}

/// <summary>Pay the bill. There's no card processor connected yet, so this records the payment only.</summary>
public class PayModel(AppDbContext db) : PageModel
{
    public Customer Customer { get; private set; } = null!;
    [BindProperty] public decimal Amount { get; set; }
    [BindProperty] public string CardNumber { get; set; } = "";
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Customer = await Load();
        Amount = Math.Max(Customer.Balance, 0);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Customer = await Load();
        var digits = new string(CardNumber.Where(char.IsDigit).ToArray());
        if (Amount <= 0 || Amount > 5000) Error = "Enter an amount between $0.01 and $5,000.";
        else if (digits.Length < 12) Error = "Enter your full card number.";
        if (Error != null) return Page();

        db.Payments.Add(new Payment
        {
            CustomerId = Customer.Id, Reference = $"MA-{Customer.Account[^4..]}{DateTime.Now:MMddHHmmss}", PaidOn = DateOnly.FromDateTime(DateTime.Today),
            PaidTime = TimeOnly.FromDateTime(DateTime.Now), Amount = Amount, Method = $"Card ending {digits[^4..]}", Source = "My Account",
        });
        db.ActorName = Customer.Name;
        Customer.Balance -= Amount;
        db.Notes.Add(new Note { CustomerId = Customer.Id, Author = "My Account", Body = $"Paid {Amount:C} online." });
        await db.SaveChangesAsync();
        TempData["paid"] = $"Thanks! Your {Amount:C} payment is in.";
        return Redirect("/myaccount");
    }

    private async Task<Customer> Load()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await db.Customers.FirstAsync(c => c.Id == id);
    }
}
