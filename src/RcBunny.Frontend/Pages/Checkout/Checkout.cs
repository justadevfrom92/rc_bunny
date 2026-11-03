using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;
using RcBunny.Data.Entities;
using RcBunny.Frontend.Services;

namespace RcBunny.Frontend.Pages.Checkout;

/// <summary>
/// Website sign-up (/checkout): address → plan → about you → review → submitted. What's entered so far is kept on a
/// signups row (cookie token), so "Save and finish later" just emails the link back. Never cached.
/// </summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class SignupPage(AppDbContext db) : PageModel
{
    protected const string Cookie = "rcb.signup";
    protected AppDbContext Db => db;
    public SignupData Data { get; private set; } = new();
    public string Step { get; protected set; } = "address";
    public static readonly string[] Steps = ["address", "plan", "about", "review"];
    private Signup? row;

    protected async Task LoadAsync()
    {
        var token = Request.Cookies[Cookie];
        row = token is null ? null : await db.Signups.AsTracking().FirstOrDefaultAsync(s => s.Token == token && s.CustomerId == null);
        if (row?.Data is JsonObject o) Data = o.Deserialize<SignupData>() ?? new();
    }

    protected async Task SaveAsync(string step)
    {
        if (row is null)
        {
            row = new Signup { Token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(), Step = step, Data = new JsonObject() };
            db.Signups.Add(row);
            Response.Cookies.Append(Cookie, row.Token, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(30), IsEssential = true });
        }
        row.Step = step;
        row.Email = Data.Email ?? row.Email;
        row.Data = JsonSerializer.SerializeToNode(Data)!.AsObject();
        await db.SaveChangesAsync();
    }

    protected async Task MarkSubmitted(int customerId)
    {
        if (row != null) { row.CustomerId = customerId; row.Step = "submitted"; await db.SaveChangesAsync(); }
        Response.Cookies.Delete(Cookie);
    }

    protected string? Error(string field) => ModelState.TryGetValue(field, out var e) && e.Errors.Count > 0 ? e.Errors[0].ErrorMessage : null;
    public string? Err(string field) => Error(field);
}

public class AddressModel(AppDbContext db) : SignupPage(db)
{
    public class Input
    {
        public bool Biz { get; set; }
        [Required(ErrorMessage = "Enter your zip code."), RegularExpression(@"^\d{5}$", ErrorMessage = "Enter a 5-digit zip.")] public string? Zip { get; set; }
        [Required(ErrorMessage = "Enter your street address."), MaxLength(200)] public string? Address { get; set; }
        [MaxLength(20)] public string? Unit { get; set; }
        [Required(ErrorMessage = "Enter your city."), MaxLength(100)] public string? City { get; set; }
        [RegularExpression(@"^\d{17,22}$", ErrorMessage = "An ESIID is 17 to 22 digits. Leave it blank if you don't know it.")] public string? Esiid { get; set; }
        public string EnrollmentType { get; set; } = "Switch";
        public DateOnly? StartDate { get; set; }
    }

    [BindProperty] public Input Form { get; set; } = new();

    public async Task OnGetAsync(string? zip, string? plan, string? type)
    {
        await LoadAsync();
        Form = new Input { Biz = Data.Biz || type == "business", Zip = zip ?? Data.Zip, Address = Data.Address, Unit = Data.Unit, City = Data.City, Esiid = Data.Esiid, EnrollmentType = Data.EnrollmentType, StartDate = Data.StartDate };
        if (plan != null && await Db.Plans.FirstOrDefaultAsync(p => p.Internal == plan && p.Active) is { } pl) { Data.PlanId = pl.Id; await SaveAsync("address"); }
        if (Request.Query["msid"] is { Count: > 0 } msid) { Data.Msid = msid.ToString(); await SaveAsync("address"); }
    }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (Form.EnrollmentType == "Move-In" && (Form.StartDate is null || Form.StartDate <= DateOnly.FromDateTime(DateTime.Today) || Form.StartDate > DateOnly.FromDateTime(DateTime.Today.AddDays(90))))
            ModelState.AddModelError("Form.StartDate", "Pick the day you move in (within the next 90 days).");
        var market = Form.Zip is null ? null : await Enrollment.MarketForZipAsync(Db, Form.Zip);
        if (Form.Zip != null && market is null) ModelState.AddModelError("Form.Zip", $"We don't serve {Form.Zip} yet. It may be outside the deregulated Texas market.");
        if (!ModelState.IsValid) return Page();
        (Data.Biz, Data.Zip, Data.Address, Data.Unit, Data.City, Data.Esiid, Data.EnrollmentType, Data.StartDate, Data.MarketId) =
            (Form.Biz, Form.Zip, Form.Address, Form.Unit, Form.City, Form.Esiid, Form.EnrollmentType, Form.StartDate, market);
        await SaveAsync("plan");
        return RedirectToPage("Plan");
    }
}

public class PlanModel(AppDbContext db, Catalog catalog) : SignupPage(db)
{
    public List<(PlanView Plan, decimal? Price)> Plans { get; private set; } = [];
    public MarketView? Market { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Data.MarketId is null) return RedirectToPage("Index");
        Step = "plan";
        var c = await catalog.GetAsync();
        Market = c.Markets.FirstOrDefault(m => m.Id == Data.MarketId);
        Plans = c.Plans.Where(p => p.Type == (Data.Biz ? "Biz" : "Resi")).Select(p => (p, c.Price(p.Internal, Market!.Name))).Where(x => x.Item2 != null).OrderBy(x => x.p.Term).ThenBy(x => x.Item2).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int planId)
    {
        await LoadAsync();
        if (!await Db.Plans.AnyAsync(p => p.Id == planId && p.Active)) return RedirectToPage();
        Data.PlanId = planId;
        await SaveAsync("about");
        return RedirectToPage("About");
    }
}

public class AboutModel(AppDbContext db) : SignupPage(db)
{
    public class Input
    {
        [MaxLength(150)] public string? BusinessName { get; set; }
        [Required(ErrorMessage = "Enter your first name."), MaxLength(60)] public string? FirstName { get; set; }
        [Required(ErrorMessage = "Enter your last name."), MaxLength(60)] public string? LastName { get; set; }
        [Required(ErrorMessage = "Enter your email."), EmailAddress(ErrorMessage = "Enter a valid email."), MaxLength(200)] public string? Email { get; set; }
        [Required(ErrorMessage = "Enter your phone number.")] public string? Phone { get; set; }
        public string PhoneType { get; set; } = "mobile";
        public string Language { get; set; } = "English";
        [Required(ErrorMessage = "Enter the last 4 of your SSN."), RegularExpression(@"^\d{4}$", ErrorMessage = "Enter 4 digits.")] public string? SsnLast4 { get; set; }
        public bool CreditFrozen { get; set; }
        public bool MarketingOptIn { get; set; } = true;
        [RegularExpression(@"^[A-Za-z0-9._-]{4,40}$", ErrorMessage = "4–40 letters, numbers, dots, dashes or underscores.")] public string? Username { get; set; }
        [MinLength(8, ErrorMessage = "Use at least 8 characters.")] public string? Password { get; set; }
        public string? PasswordConfirm { get; set; }
    }

    [BindProperty] public Input Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Data.PlanId is null) return RedirectToPage("Plan");
        Step = "about";
        Form = new Input { BusinessName = Data.BusinessName, FirstName = Data.FirstName, LastName = Data.LastName, Email = Data.Email, Phone = Data.Phone, PhoneType = Data.PhoneType ?? "mobile", Language = Data.Language ?? "English", MarketingOptIn = Data.MarketingOptIn, Username = Data.Username };
        return Page();
    }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        Step = "about";
        if (Data.Biz && string.IsNullOrWhiteSpace(Form.BusinessName)) ModelState.AddModelError("Form.BusinessName", "Enter the business name.");
        if (Form.Phone is { } ph && Regex.Replace(ph, @"\D", "").Length != 10) ModelState.AddModelError("Form.Phone", "Enter a 10-digit phone number.");
        if (!string.IsNullOrWhiteSpace(Form.Username))
        {
            if (string.IsNullOrEmpty(Form.Password)) ModelState.AddModelError("Form.Password", "Choose a password for My Account.");
            else if (Form.Password != Form.PasswordConfirm) ModelState.AddModelError("Form.PasswordConfirm", "The passwords don't match.");
            if (await Db.Customers.AnyAsync(c => c.Username == Form.Username.ToLower())) ModelState.AddModelError("Form.Username", "That username is taken.");
        }
        if (!ModelState.IsValid) return Page();
        (Data.BusinessName, Data.FirstName, Data.LastName, Data.Email, Data.Phone, Data.PhoneType, Data.Language, Data.SsnLast4, Data.MarketingOptIn, Data.Username) =
            (Form.BusinessName, Form.FirstName!.Trim(), Form.LastName!.Trim(), Form.Email!.Trim(), Form.Phone, Form.PhoneType, Form.Language, Form.SsnLast4, Form.MarketingOptIn, Form.Username);
        Data.PasswordHash = string.IsNullOrEmpty(Form.Password) ? null : Passwords.Hash(Form.Password);
        await SaveAsync(Form.CreditFrozen ? "frozen" : "review");
        return Form.CreditFrozen ? RedirectToPage("Info", new { kind = "frozen" }) : RedirectToPage("Review");
    }
}

public class ReviewModel(AppDbContext db, Catalog catalog) : SignupPage(db)
{
    public PlanView? Plan { get; private set; }
    public decimal? Price { get; private set; }
    public MarketView? Market { get; private set; }
    [BindProperty] public bool AgreeEfl { get; set; }
    [BindProperty] public bool AgreeTos { get; set; }
    [BindProperty] public bool AgreeYrac { get; set; }
    [BindProperty] public bool Autopay { get; set; } = true;
    [BindProperty] public bool Paperless { get; set; } = true;
    [BindProperty] public bool PeakPerks { get; set; }
    [BindProperty] public string? PromoCode { get; set; }
    public string? Error { get; private set; }

    private async Task<bool> Prepare()
    {
        await LoadAsync();
        Step = "review";
        if (Data.FirstName is null || Data.PlanId is null || Data.MarketId is null) return false;
        var c = await catalog.GetAsync();
        Plan = c.Plans.FirstOrDefault(p => p.Id == Data.PlanId);
        Market = c.Markets.FirstOrDefault(m => m.Id == Data.MarketId);
        Price = Plan is null || Market is null ? null : c.Price(Plan.Internal, Market.Name);
        return Plan != null;
    }

    public async Task<IActionResult> OnGetAsync() => await Prepare() ? Page() : RedirectToPage("Index");

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync()
    {
        if (!await Prepare()) return RedirectToPage("Index");
        if (!AgreeEfl || !AgreeTos || !AgreeYrac) { Error = "Please confirm you have read and agree to each document."; return Page(); }
        if (!string.IsNullOrWhiteSpace(PromoCode))
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (!await Db.PromoCodes.AnyAsync(p => p.Code == PromoCode.Trim().ToUpper() && p.Active && (p.StartsOn == null || p.StartsOn <= today) && (p.EndsOn == null || p.EndsOn >= today))) { Error = "That promo code isn't valid."; return Page(); }
            Data.PromoCode = PromoCode.Trim().ToUpper();
        }
        (Data.Autopay, Data.Paperless, Data.PeakPerks) = (Autopay, Paperless, PeakPerks);
        var customer = await Enrollment.CreateAsync(Db, Data, HttpContext.Connection.RemoteIpAddress?.ToString());
        await MarkSubmitted(customer.Id);
        TempData["account"] = customer.Account;
        TempData["deposit"] = customer.DepositDue.ToString("0.00");
        TempData["login"] = customer.Username != null;
        return RedirectToPage("Accepted");
    }

    /// <summary>Save and finish later: the link goes to the email (logged until mail is set up).</summary>
    public async Task<IActionResult> OnPostSaveAsync(string email)
    {
        await LoadAsync();
        Data.Email ??= email;
        await SaveAsync(Step);
        TempData["saved"] = $"/checkout/resume/{Request.Cookies[Cookie]}";
        return RedirectToPage("Info", new { kind = "saved" });
    }
}

public class AcceptedModel : PageModel
{
    public string? Account { get; private set; }
    public decimal Deposit { get; private set; }
    public bool Login { get; private set; }

    public IActionResult OnGet()
    {
        if (TempData["account"] is not string a) return RedirectToPage("Index");
        Account = a;
        Deposit = decimal.TryParse(TempData["deposit"]?.ToString(), out var d) ? d : 0;
        Login = TempData["login"] is true;
        return Page();
    }
}

public class ResumeModel(AppDbContext db) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string token)
    {
        var s = await db.Signups.FirstOrDefaultAsync(x => x.Token == token && x.CustomerId == null);
        if (s is null) return RedirectToPage("Index");
        Response.Cookies.Append("rcb.signup", token, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(30), IsEssential = true });
        return RedirectToPage(s.Step switch { "plan" => "Plan", "about" => "About", "review" or "frozen" => "Review", _ => "Index" });
    }
}

/// <summary>Deposit lookup (account + zip) and the info pages: saved, frozen credit, alternatives, call to start, cancel.</summary>
public class InfoModel(AppDbContext db) : PageModel
{
    public string Kind { get; private set; } = "";
    public Customer? Customer { get; private set; }
    public bool Looked { get; private set; }

    public async Task<IActionResult> OnGetAsync(string kind, string? account, string? zip)
    {
        if (kind is not ("saved" or "frozen" or "alternatives" or "start-call" or "cancel" or "error" or "deposit")) return NotFound();
        Kind = kind;
        if (kind == "deposit" && !string.IsNullOrEmpty(account) && !string.IsNullOrEmpty(zip))
        {
            Looked = true;
            Customer = await db.Customers.FirstOrDefaultAsync(c => c.Account == account && c.Zip == zip);
        }
        return Page();
    }
}
