using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Frontend.Pages.MyAccount;

public class LoginModel(AppDbContext db, IWebHostEnvironment env) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; private set; }
    /// <summary>Sample customers for the Test Sign-Ins list (development only).</summary>
    public List<Customer> TestCustomers { get; private set; } = [];

    public async Task OnGetAsync() => TestCustomers = await TestCustomersAsync();

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        var name = Username.Trim().ToLower();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Username == name || c.Email == name);
        if (customer is null || !Passwords.Verify(customer.PasswordHash, Password))
        {
            Error = "That username and password don't match.";
            TestCustomers = await TestCustomersAsync();
            return Page();
        }
        return await SignInAsync(customer, returnUrl);
    }

    /// <summary>Testing only: open My Account as a sample customer without a password.</summary>
    public async Task<IActionResult> OnPostTestAsync(int id)
    {
        var customer = (await TestCustomersAsync()).FirstOrDefault(c => c.Id == id);
        return customer is null ? NotFound() : await SignInAsync(customer, null);
    }

    private Task<List<Customer>> TestCustomersAsync() => env.IsDevelopment()
        ? db.Customers.Where(c => c.PasswordHash != null && c.Username != null && c.Email != null && c.Email.EndsWith("@example.com")).OrderBy(c => c.Id).Take(12).ToListAsync()
        : Task.FromResult(new List<Customer>());

    private async Task<IActionResult> SignInAsync(Customer customer, string? returnUrl)
    {
        // The sign-in goes in the account's Logins log (Corral → account → Logs)
        db.ActorName = customer.Name;
        db.Record(customer.Id, "Logins", "ItemLogin_model", "Customer", customer.Id, "signed in", $"Signed in to My Account from {HttpContext.Connection.RemoteIpAddress}");
        await db.SaveChangesAsync();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, customer.Id.ToString())], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/myaccount/dashboard");
    }
}

[IgnoreAntiforgeryToken] // the header's Sign Out button (main.js) posts here
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

/// <summary>Pay Bill: how much (balance, last bill or another amount) with one of the saved payment methods.</summary>
public class PayModel(AppDbContext db) : PageModel
{
    public Customer Customer { get; private set; } = null!;
    public List<PaymentMethod> Methods { get; private set; } = [];
    /// <summary>The amounts offered: key → (label, amount or null for "another amount", note).</summary>
    public List<(string Key, string Label, decimal? Amount, string? Note)> Amounts { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string choice, decimal? amount, int method)
    {
        await LoadAsync();
        var pick = Amounts.FirstOrDefault(a => a.Key == choice);
        var pay = pick.Key == null ? null : pick.Amount ?? amount;
        var with = Methods.FirstOrDefault(m => m.Id == method);
        if (pay is not > 0 || pay > 10000) Error = "Enter an amount between $1 and $10,000.";
        else if (with is null) Error = "Pick how to pay.";
        if (Error != null) return Page();

        db.Payments.Add(new Payment
        {
            CustomerId = Customer.Id, Reference = $"MA-{Customer.Account[^4..]}{DateTime.Now:MMddHHmmss}", PaidOn = DateOnly.FromDateTime(DateTime.Today),
            PaidTime = TimeOnly.FromDateTime(DateTime.Now), Amount = pay!.Value, Method = with!.Label(), Source = "My Account",
        });
        db.ActorName = Customer.Name;
        Customer.Balance -= pay.Value;
        db.Notes.Add(new Note { CustomerId = Customer.Id, Author = "My Account", Body = $"Paid {pay:C} online with {with.Label()}." });
        await db.SaveChangesAsync();
        TempData["paid"] = $"Thanks! Your {pay:C} payment is in.";
        return Redirect("/myaccount/dashboard");
    }

    private async Task LoadAsync()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        Customer = await db.Customers.FirstAsync(c => c.Id == id);
        Methods = await db.PaymentMethods.Where(m => m.CustomerId == id && m.RemovedAt == null).OrderByDescending(m => m.Autopay).ThenBy(m => m.Id).ToListAsync();
        var last = await db.Bills.Where(b => b.CustomerId == id).OrderByDescending(b => b.BilledOn).FirstOrDefaultAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var pastDue = Customer.DueDate is { } due && due < today ? Math.Max(0, Customer.Balance) : 0;
        Amounts = [];
        if (Customer.Balance > 0) Amounts.Add(("balance", "Current balance", Customer.Balance, Customer.DueDate is { } d ? $"Due {d:MMM d, yyyy}" : null));
        if (pastDue > 0) Amounts.Add(("past_due", "Past due amount", pastDue, "Pay this to avoid a late fee or disconnection"));
        if (last != null) Amounts.Add(("last_bill", "Last bill", last.Amount, $"Billed {last.BilledOn:MMM d, yyyy}"));
        Amounts.Add(("other", "Another amount", null, "Pay part of your balance, or extra toward your next bill"));
    }
}

/// <summary>A file on one of the customer's own tickets (ebunny's /myaccount/support/{ticket}/files/{attachment}).</summary>
public class SupportFileModel(AppDbContext db, IConfiguration config, IWebHostEnvironment env) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id, int fileId)
    {
        var customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var file = await db.TicketAttachments.FirstOrDefaultAsync(a => a.Id == fileId && a.TicketId == id && db.SupportTickets.Any(t => t.Id == id && t.CustomerId == customerId));
        var storage = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage"));
        var full = file == null ? null : Path.Combine(storage, file.Path);
        return file == null || !System.IO.File.Exists(full) ? NotFound() : PhysicalFile(full, file.Mime, file.Name);
    }
}
