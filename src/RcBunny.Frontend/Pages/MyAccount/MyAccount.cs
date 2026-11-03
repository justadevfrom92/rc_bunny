using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;
using RcBunny.Data.Entities;

namespace RcBunny.Frontend.Pages.MyAccount;

/// <summary>My Account pages: the signed-in customer, loaded once per request. Never cached (personal data).</summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class MyAccountPage(AppDbContext db) : PageModel
{
    protected AppDbContext Db => db;
    public Customer Customer { get; private set; } = null!;

    protected async Task<bool> LoadCustomerAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;
        var c = await db.Customers.Include(x => x.Plan).Include(x => x.Market).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return false;
        Customer = c;
        return true;
    }

    protected async Task<IActionResult> SignedOut()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/MyAccount/Login");
    }
}

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class LoginModel(AppDbContext db, IWebHostEnvironment env) : PageModel
{
    [BindProperty] public string? Username { get; set; }
    [BindProperty] public string? Password { get; set; }
    public string? Error { get; private set; }
    public bool TestLogin => env.IsDevelopment();

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? Redirect("/myaccount") : Page();

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        var name = Username?.Trim().ToLowerInvariant() ?? "";
        var c = name.Length == 0 ? null : await db.Customers.FirstOrDefaultAsync(x => x.Username == name || x.Email == name);
        if (c is null || !Passwords.Verify(c.Password, Password ?? ""))
        {
            Error = "That username and password don't match.";
            return Page();
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, c.Id.ToString()), new Claim(ClaimTypes.Name, c.FirstName ?? c.Name)],
            CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/myaccount");
    }
}

public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }
}

public class DashboardModel(AppDbContext db) : MyAccountPage(db)
{
    public IReadOnlyList<Bill> Bills { get; private set; } = [];
    public Payment? LastPayment { get; private set; }
    public object UsageProps { get; private set; } = new { };

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        Bills = await Db.Bills.Where(b => b.CustomerId == Customer.Id).OrderByDescending(b => b.BilledOn).Take(12).ToListAsync();
        LastPayment = await Db.Payments.Where(p => p.CustomerId == Customer.Id && p.Status == "Success").OrderByDescending(p => p.PaidOn).FirstOrDefaultAsync();
        UsageProps = new
        {
            months = Bills.OrderBy(b => b.BilledOn).Select(b => new { label = b.BilledOn.AddMonths(-1).ToString("MMM yy"), kwh = b.Kwh, amount = b.Amount }),
        };
        return Page();
    }
}

public class BillsModel(AppDbContext db) : MyAccountPage(db)
{
    public IReadOnlyList<Bill> Bills { get; private set; } = [];
    public IReadOnlyList<Payment> Payments { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        Bills = await Db.Bills.Where(b => b.CustomerId == Customer.Id).OrderByDescending(b => b.BilledOn).ToListAsync();
        Payments = await Db.Payments.Where(p => p.CustomerId == Customer.Id).OrderByDescending(p => p.PaidOn).ToListAsync();
        return Page();
    }
}
