using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;
using RcBunny.Data.Entities;
using RcBunny.Frontend.Services;

namespace RcBunny.Frontend.Pages.MyAccount;

/// <summary>A My Account page whose only job on GET is to load the customer; the flash message shows after a POST.</summary>
public abstract class AccountPage(AppDbContext db) : MyAccountPage(db)
{
    public string? Flash => TempData["flash"] as string;
    public string? Problem { get; protected set; }
    protected IActionResult Done(string message) { TempData["flash"] = message; return RedirectToPage(); }
    protected static bool Live(string key) => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key));
    protected void AddNote(string body, string? category = null, string? action = null) =>
        Db.Notes.Add(new Note { CustomerId = Customer.Id, Author = "My Account", Body = body, Category = category, Action = action, Disposition = action });
    protected async Task<Customer> Tracked() => await Db.Customers.AsTracking().FirstAsync(c => c.Id == Customer.Id);
}

public class PayModel(AppDbContext db) : AccountPage(db)
{
    public List<PaymentMethod> Methods { get; private set; } = [];
    public List<ScheduledPayment> Scheduled { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        Methods = await Db.PaymentMethods.Where(m => m.CustomerId == Customer.Id && m.RemovedAt == null).ToListAsync();
        Scheduled = await Db.ScheduledPayments.Where(s => s.CustomerId == Customer.Id && s.Status == "scheduled").OrderBy(s => s.PayAt).ToListAsync();
        return Page();
    }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync(decimal amount, int methodId, DateOnly? payOn)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var m = await Db.PaymentMethods.FirstOrDefaultAsync(x => x.Id == methodId && x.CustomerId == Customer.Id && x.RemovedAt == null);
        if (m is null || amount <= 0 || amount > 5000) { Problem = "Pick a payment method and an amount between $0.01 and $5,000."; return await OnGetAsync(); }
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (payOn is { } d && d > today)
        {
            Db.ScheduledPayments.Add(new ScheduledPayment { CustomerId = Customer.Id, PaymentMethodId = m.Id, Amount = amount, PayAt = d.ToDateTime(new TimeOnly(8, 0)), Status = "scheduled" });
            await Db.SaveChangesAsync();
            return Done($"Payment of {amount:C} scheduled for {d:MMMM d}.");
        }
        var c = await Tracked();
        var live = Live("STRIPE_SECRET");
        Db.Payments.Add(new Payment { CustomerId = c.Id, Reference = $"MA-{c.Account[^6..]}-{DateTime.UtcNow:yyMMddHHmmss}", PaidOn = today, PaidTime = TimeOnly.FromDateTime(DateTime.Now), Amount = amount, Method = m.Type, Source = "My Account", Status = live ? "Success" : "Pending", PaymentMethodId = m.Id, Confirmation = "C" + Random.Shared.Next(100000, 999999) });
        if (live) c.Balance -= amount;
        await Db.SaveChangesAsync();
        return Done(live ? $"Thanks! {amount:C} paid." : $"Thanks! Your {amount:C} payment is pending (card processing isn't connected yet).");
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        await Db.ScheduledPayments.Where(s => s.Id == id && s.CustomerId == Customer.Id && s.Status == "scheduled").ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "cancelled"));
        return Done("Scheduled payment cancelled.");
    }
}

public class MethodsModel(AppDbContext db) : AccountPage(db)
{
    public List<PaymentMethod> Methods { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        Methods = await Db.PaymentMethods.Where(m => m.CustomerId == Customer.Id && m.RemovedAt == null).OrderByDescending(m => m.Autopay).ToListAsync();
        return Page();
    }

    /// <summary>Only the last 4 and expiry are kept here; the card itself goes to the processor (Stripe) when it's connected.</summary>
    public async Task<IActionResult> OnPostAddAsync(string type, string number, string? expires, string? nickname)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var digits = new string((number ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 8) { Problem = "Enter the full card or account number."; return await OnGetAsync(); }
        Db.PaymentMethods.Add(new PaymentMethod { CustomerId = Customer.Id, Type = type == "Bank Account" ? "Bank Account" : "Credit Card", Last4 = digits[^4..], Expires = expires, Nickname = nickname, Vendor = type == "Bank Account" ? "Auth.net" : "Stripe" });
        await Db.SaveChangesAsync();
        return Done("Payment method added.");
    }

    public async Task<IActionResult> OnPostRemoveAsync(int id)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        await Db.PaymentMethods.Where(m => m.Id == id && m.CustomerId == Customer.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.RemovedAt, DateTime.UtcNow).SetProperty(m => m.Autopay, false));
        return Done("Removed.");
    }

    public async Task<IActionResult> OnPostAutopayAsync(int id)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        await Db.PaymentMethods.Where(m => m.CustomerId == Customer.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Autopay, m => m.Id == id));
        (await Tracked()).Autopay = true;
        await Db.SaveChangesAsync();
        return Done("AutoPay will use this method.");
    }
}

public class ProductsModel(AppDbContext db) : AccountPage(db)
{
    public record Product(string Key, string Name, string Description, string Price, string How, bool Enrolled);
    public List<Product> Products { get; private set; } = [];

    private static readonly Dictionary<string, string> Map = new() { ["autopay"] = "AutoPay", ["paperless-billing"] = "Paperless Billing", ["giddy-up"] = "Giddyup Guarantee", ["balanced-billing"] = "Balanced Billing", ["peak-perks"] = "Peak Perks" };

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var have = await Db.CustomerProducts.Where(p => p.CustomerId == Customer.Id && p.RemovedAt == null).Select(p => p.Product).ToListAsync();
        Products = (AppConfig.At("myaccount.products") as JsonObject ?? []).Select(kv =>
        {
            var a = kv.Value!.AsArray();
            var enrolled = kv.Key switch { "autopay" => Customer.Autopay, "paperless-billing" => Customer.Paperless, _ => have.Contains(Map.GetValueOrDefault(kv.Key) ?? a[0]!.ToString()) };
            return new Product(kv.Key, a[0]!.ToString(), a[1]!.ToString(), a[2]!.ToString(), a[3]!.ToString(), enrolled);
        }).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string key)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        if (AppConfig.At($"myaccount.products.{key}") is not JsonArray def || def[3]!.ToString() != "self") return Done("Call us to change that one.");
        var c = await Tracked();
        var name = Map.GetValueOrDefault(key) ?? def[0]!.ToString();
        bool on;
        if (key == "autopay") on = c.Autopay = !c.Autopay;
        else if (key == "paperless-billing") on = c.Paperless = !c.Paperless;
        else
        {
            var row = await Db.CustomerProducts.AsTracking().FirstOrDefaultAsync(p => p.CustomerId == c.Id && p.Product == name && p.RemovedAt == null);
            if (row is null) Db.CustomerProducts.Add(new CustomerProduct { CustomerId = c.Id, Product = name }); else row.RemovedAt = DateTime.UtcNow;
            on = row is null;
        }
        AddNote($"{(on ? "Enrolled in" : "Removed")} {name} in My Account.", "Products", on ? $"Add {name}" : $"Remove {name}");
        await Db.SaveChangesAsync();
        return Done(on ? $"You're enrolled in {name}." : $"{name} is off.");
    }
}

public class InsightsModel(AppDbContext db) : AccountPage(db)
{
    public List<Bill> Bills { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        Bills = await Db.Bills.Where(b => b.CustomerId == Customer.Id).OrderBy(b => b.BilledOn).ToListAsync();
        return Page();
    }
}

public class ProfileModel(AppDbContext db) : AccountPage(db)
{
    public async Task<IActionResult> OnGetAsync() => await LoadCustomerAsync() ? Page() : await SignedOut();

    public async Task<IActionResult> OnPostContactAsync(string email, string phone, string? phoneType, string language, bool marketing)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _)) { Problem = "Enter a valid email."; return Page(); }
        var c = await Tracked();
        (c.Email, c.Phone, c.PhoneType, c.Language, c.MarketingOptIn) = (email.Trim().ToLowerInvariant(), phone, phoneType, language == "Spanish" ? "Spanish" : "English", marketing);
        AddNote("Contact details updated in My Account.", "Inquiry", "Update Contact Info");
        await Db.SaveChangesAsync();
        return Done("Saved.");
    }

    public async Task<IActionResult> OnPostPasswordAsync(string current, string password, string confirm)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var c = await Tracked();
        if (!Passwords.Verify(c.Password, current)) { Problem = "Your current password isn't right."; return Page(); }
        if (password.Length < 8 || password != confirm) { Problem = "Use at least 8 characters, and type it the same twice."; return Page(); }
        c.Password = Passwords.Hash(password);
        await Db.SaveChangesAsync();
        return Done("Password changed.");
    }

    public async Task<IActionResult> OnPostAuthorizedAsync(string name, string relationship, string? phone)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        if (string.IsNullOrWhiteSpace(name)) { Problem = "Enter their name."; return Page(); }
        var c = await Tracked();
        var list = c.AuthorizedUsers as JsonArray ?? [];
        if (list.Count >= 5) { Problem = "Up to 5 authorized users."; return Page(); }
        list.Add(new JsonObject { ["name"] = name.Trim(), ["relationship"] = relationship, ["phone"] = phone });
        c.AuthorizedUsers = list.DeepClone();
        AddNote($"Authorized user added: {name.Trim()} ({relationship}).", "Inquiry", "Update Contact Info");
        await Db.SaveChangesAsync();
        return Done($"{name.Trim()} can now call about this account.");
    }

    public async Task<IActionResult> OnPostRemoveAuthorizedAsync(int index)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var c = await Tracked();
        if (c.AuthorizedUsers is JsonArray list && index >= 0 && index < list.Count) { var copy = (JsonArray)list.DeepClone(); copy.RemoveAt(index); c.AuthorizedUsers = copy; await Db.SaveChangesAsync(); }
        return Done("Removed.");
    }

    /// <summary>Link another account you own (account number + service zip) so you can switch between them.</summary>
    public async Task<IActionResult> OnPostLinkAsync(string account, string zip)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var other = await Db.Customers.FirstOrDefaultAsync(x => x.Account == account.Trim() && x.Zip == zip.Trim() && x.Id != Customer.Id);
        if (other is null) { Problem = "We couldn't find that account and zip."; return Page(); }
        var c = await Tracked();
        var list = c.LinkedAccounts as JsonArray ?? [];
        if (list.Any(x => x?.ToString() == other.Account)) return Done("Already linked.");
        var copy = (JsonArray)list.DeepClone(); copy.Add(other.Account); c.LinkedAccounts = copy;
        await Db.SaveChangesAsync();
        return Done($"Linked {other.Account}.");
    }

    public async Task<IActionResult> OnPostSwitchAsync(string account)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        if ((Customer.LinkedAccounts as JsonArray)?.Any(x => x?.ToString() == account) != true) return Done("That account isn't linked.");
        var other = await Db.Customers.FirstAsync(x => x.Account == account);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, other.Id.ToString()), new Claim(ClaimTypes.Name, other.FirstName ?? other.Name)], CookieAuthenticationDefaults.AuthenticationScheme)));
        return Redirect("/myaccount");
    }
}

public class PlanPageModel(AppDbContext db, Catalog catalog) : AccountPage(db)
{
    public PlanTerm? Term { get; private set; }
    public List<(PlanView Plan, decimal? Price)> Options { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        Term = await Db.PlanTerms.Where(t => t.CustomerId == Customer.Id).OrderByDescending(t => t.OrderedAt).FirstOrDefaultAsync();
        var c = await catalog.GetAsync();
        var market = c.Markets.FirstOrDefault(m => m.Id == Customer.MarketId)?.Name ?? CatalogSnapshot.DefaultMarket;
        var group = c.Group("renew");
        var plans = group.Count > 0 ? group : c.Plans.Where(p => p.Type == (Customer.Type == "Residential" ? "Resi" : "Biz") && p.Term >= 12).ToList();
        Options = plans.Select(p => (p, c.Price(p.Internal, market))).Where(x => x.Item2 != null).OrderBy(x => x.p.Term).ToList();
        return Page();
    }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync(int planId)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var plan = await Db.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.Active);
        if (plan is null) return Done("Pick a plan.");
        var current = await Db.PlanTerms.Where(t => t.CustomerId == Customer.Id && t.Status == "current").OrderByDescending(t => t.ContractEnd).FirstOrDefaultAsync();
        var start = current?.ContractEnd ?? DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var rate = await Db.Rates.Where(r => r.PlanId == plan.Id && r.MarketId == Customer.MarketId).OrderByDescending(r => r.EffectiveOn).Select(r => r.Energy).FirstOrDefaultAsync();
        Db.PlanTerms.Add(new PlanTerm { CustomerId = Customer.Id, PlanId = plan.Id, RateClass = Customer.RateClass ?? "Residential", EnergyCharge = rate / 100m, Rate2000 = rate, OrderedAt = DateTime.UtcNow, ContractStart = start, ContractEnd = start.AddMonths(plan.Term), Status = "not started" });
        AddNote($"Renewed in My Account: {plan.Name}, starting {start:MMM d, yyyy}.", "Order", "Renewal");
        Db.ContactLogs.Add(new ContactLog { CustomerId = Customer.Id, Channel = "Email", Template = "Renewal - Confirmation", Status = "logged", SentAt = DateTime.UtcNow, Direction = "out" });
        await Db.SaveChangesAsync();
        return Done($"You're renewed on {plan.Name} starting {start:MMMM d}. A confirmation email is on its way.");
    }
}

public class TransferModel(AppDbContext db) : AccountPage(db)
{
    public async Task<IActionResult> OnGetAsync() => await LoadCustomerAsync() ? Page() : await SignedOut();

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync(string street, string? unit, string city, string zip, DateOnly moveOut, DateOnly moveIn)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var market = await Enrollment.MarketForZipAsync(Db, zip);
        if (market is null) { Problem = $"We don't serve {zip}. Call us and we'll help you close the account."; return Page(); }
        if (moveIn < DateOnly.FromDateTime(DateTime.Today.AddDays(1))) { Problem = "Pick a move-in date from tomorrow on."; return Page(); }
        Db.ServiceAddresses.Add(new ServiceAddress { CustomerId = Customer.Id, Street = street + (string.IsNullOrWhiteSpace(unit) ? "" : " " + unit), City = city, Zip = zip, OrderedAt = DateTime.UtcNow, ServiceStart = moveIn });
        await Db.ServiceAddresses.Where(a => a.CustomerId == Customer.Id && a.ServiceEnd == null && a.Street == Customer.Address).ExecuteUpdateAsync(s => s.SetProperty(a => a.ServiceEnd, moveOut));
        Db.WorkItems.Add(new WorkItem { CustomerId = Customer.Id, Queue = "unapplied-transfers", Summary = $"Transfer to {street}, {city} {zip} on {moveIn:MM/dd}" });
        AddNote($"Transfer requested: stop {moveOut:MM/dd} at {Customer.Address}, start {moveIn:MM/dd} at {street}, {city} {zip}.", "Order", "Transfer of Service");
        await Db.SaveChangesAsync();
        return Done($"Got it! Service starts at {street} on {moveIn:MMMM d} and stops at your current address on {moveOut:MMMM d}.");
    }
}

public class RewardsModel(AppDbContext db) : AccountPage(db)
{
    public List<StarEntry> History { get; private set; } = [];
    public List<RewardOffer> Offers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        History = await Db.StarEntries.Where(s => s.CustomerId == Customer.Id).OrderByDescending(s => s.CreatedAt).Take(50).ToListAsync();
        Offers = await Db.RewardOffers.Where(o => o.Active).OrderBy(o => o.Position).ToListAsync();
        return Page();
    }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync(int offerId)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var offer = await Db.RewardOffers.FirstOrDefaultAsync(o => o.Id == offerId && o.Active);
        var c = await Tracked();
        if (offer is null) return Done("That offer isn't available.");
        if (c.Stars < offer.Stars) return Done($"{offer.Name} needs {offer.Stars:N0} stars; you have {c.Stars:N0}.");
        if (offer.Effect == "drawing")
        {
            var month = DateTime.UtcNow.ToString("yyyy-MM");
            if ((await Db.HistoryItems.Where(h => h.CustomerId == c.Id && h.Model == "ItemDrawingEntry_model").ToListAsync()).Any(h => h.Data?["month"]?.ToString() == month)) return Done("You're already in this month's drawing.");
            Db.HistoryItems.Add(new HistoryItem { CustomerId = c.Id, Model = "ItemDrawingEntry_model", Group = "Products", RecordId = c.Id, Action = "created", Summary = "Monthly drawing entry", Data = new JsonObject { ["month"] = month } });
        }
        c.Stars -= offer.Stars;
        Db.StarEntries.Add(new StarEntry { CustomerId = c.Id, Stars = -offer.Stars, Reason = "Redeemed: " + offer.Name, RewardOfferId = offer.Id, Fulfillment = offer.Effect is "bill_credit" or "drawing" ? "fulfilled" : "pending" });
        if (offer.Effect == "bill_credit" && decimal.TryParse(offer.Value, out var credit)) c.Balance -= credit;
        await Db.SaveChangesAsync();
        return Done(offer.Effect == "drawing" ? "You're entered in this month's drawing. Good luck!" : $"Redeemed {offer.Name}.");
    }
}

public class ReferModel(AppDbContext db) : AccountPage(db)
{
    public int Referred { get; private set; }
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        Referred = await Db.Customers.CountAsync(c => c.ReferredBy == Customer.ReferralCode);
        return Page();
    }
}

public class MessagesModel(AppDbContext db) : AccountPage(db)
{
    public List<ContactLog> Messages { get; private set; } = [];
    public ContactLog? Open { get; private set; }
    public string? Html { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        Messages = await Db.ContactLogs.Where(l => l.CustomerId == Customer.Id && l.Direction != "in").OrderByDescending(l => l.CreatedAt).Take(100).ToListAsync();
        if (id is { } i && Messages.FirstOrDefault(m => m.Id == i) is { } m)
        {
            Open = m;
            var t = await Db.EmailTemplates.FirstOrDefaultAsync(x => x.Name == m.Template);
            Html = t is null ? System.Net.WebUtility.HtmlEncode(m.Body ?? "") : TemplateText.Render(t.Body, Customer, Customer.Plan?.Name, slug => SurveyLinks.For(slug, Customer.Id));
            if (m.OpenedAt is null) await Db.ContactLogs.Where(x => x.Id == i).ExecuteUpdateAsync(s => s.SetProperty(x => x.OpenedAt, DateTime.UtcNow));
        }
        return Page();
    }
}

public class SupportModel(AppDbContext db, IWebHostEnvironment env, IConfiguration config) : AccountPage(db)
{
    public List<SupportTicket> Tickets { get; private set; } = [];
    public SupportTicket? Ticket { get; private set; }
    public List<SupportTicketMessage> Thread { get; private set; } = [];
    public List<SupportTicketAttachment> Files { get; private set; } = [];
    public static readonly string[] Topics = ["billing", "service", "plan", "rewards", "account", "outage", "other"];

    private string Storage => Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage"));

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        Tickets = await Db.SupportTickets.Where(t => t.CustomerId == Customer.Id).OrderByDescending(t => t.LastActivityAt).ToListAsync();
        if (id is { } i)
        {
            Ticket = Tickets.FirstOrDefault(t => t.Id == i);
            if (Ticket is null) return NotFound();
            // Customers see replies and status changes, never internal notes
            Thread = await Db.SupportTicketMessages.Include(m => m.User).Where(m => m.SupportTicketId == i).OrderBy(m => m.CreatedAt).ToListAsync();
            Files = await Db.SupportTicketAttachments.Where(a => a.SupportTicketId == i).ToListAsync();
        }
        return Page();
    }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostNewAsync(string subject, string topic, string message, IFormFile? file)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message)) { Problem = "Add a subject and a message."; return await OnGetAsync(null); }
        var t = new SupportTicket { CustomerId = Customer.Id, Name = Customer.Name, Email = Customer.Email ?? "", Subject = subject.Trim()[..Math.Min(200, subject.Trim().Length)], Topic = Topics.Contains(topic) ? topic : "other", Channel = "My Account", Status = "new", Priority = topic == "outage" ? "high" : "normal", LastActivityAt = DateTime.UtcNow };
        var msg = new SupportTicketMessage { Author = "customer", Body = message.Trim(), Via = "portal" };
        t.Messages.Add(msg);
        Db.SupportTickets.Add(t);
        await Db.SaveChangesAsync();
        await Attach(t, msg, file);
        return Redirect($"/myaccount/support/{t.Id}");
    }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostReplyAsync(int id, string message, IFormFile? file)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var t = await Db.SupportTickets.AsTracking().FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == Customer.Id);
        if (t is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(message) || file != null)
        {
            var msg = new SupportTicketMessage { SupportTicketId = id, Author = "customer", Body = string.IsNullOrWhiteSpace(message) ? "(file)" : message.Trim(), Via = "portal" };
            Db.SupportTicketMessages.Add(msg);
            t.Status = t.Status is "solved" or "closed" or "waiting" ? "open" : t.Status; t.LastActivityAt = DateTime.UtcNow;
            await Db.SaveChangesAsync();
            await Attach(t, msg, file);
        }
        return Redirect($"/myaccount/support/{id}");
    }

    public async Task<IActionResult> OnPostRateAsync(int id, string rating, string? comment)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        await Db.SupportTickets.Where(x => x.Id == id && x.CustomerId == Customer.Id && (x.Status == "solved" || x.Status == "closed"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Rating, rating == "good" ? "good" : "bad").SetProperty(x => x.RatingComment, comment).SetProperty(x => x.RatedAt, DateTime.UtcNow));
        TempData["flash"] = "Thanks for the feedback!";
        return Redirect($"/myaccount/support/{id}");
    }

    public async Task<IActionResult> OnGetFileAsync(int id, int fileId)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var a = await Db.SupportTicketAttachments.FirstOrDefaultAsync(x => x.Id == fileId && x.SupportTicketId == id && x.SupportTicket!.CustomerId == Customer.Id);
        var path = a is null ? null : Path.Combine(Storage, a.Path);
        return path is null || !System.IO.File.Exists(path) ? NotFound() : PhysicalFile(path, a!.Mime, a.Name);
    }

    private async Task Attach(SupportTicket t, SupportTicketMessage msg, IFormFile? file)
    {
        if (file is null || file.Length == 0) return;
        if (file.Length > 10_000_000) return;
        var ok = new[] { "image/png", "image/jpeg", "image/gif", "application/pdf", "text/plain" };
        if (!ok.Contains(file.ContentType)) return;
        var rel = $"ranger/{t.Id}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var full = Path.Combine(Storage, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using (var f = System.IO.File.Create(full)) await file.CopyToAsync(f);
        Db.SupportTicketAttachments.Add(new SupportTicketAttachment { SupportTicketId = t.Id, SupportTicketMessageId = msg.Id, FromCustomer = true, Name = Path.GetFileName(file.FileName), Path = rel, Mime = file.ContentType, Size = (int)file.Length });
        await Db.SaveChangesAsync();
    }
}

/// <summary>Shop checkout (needs My Account): pay by card on file, or add it to the electricity bill.</summary>
public class ShopCheckoutModel(AppDbContext db, ShopCatalog shop, Cart cart) : AccountPage(db)
{
    public List<(ShopProduct Product, int Qty)> Lines { get; private set; } = [];
    public List<PaymentMethod> Methods { get; private set; } = [];
    public decimal Subtotal => Lines.Sum(l => l.Product.Price * l.Qty);

    private async Task Fill()
    {
        var products = await shop.ProductsAsync();
        Lines = cart.Lines().Select(l => (products.FirstOrDefault(p => p.Sku == l.Sku && p.Stock), l.Qty)).Where(x => x.Item1 != null).Select(x => (x.Item1!, x.Qty)).ToList();
        Methods = await Db.PaymentMethods.Where(m => m.CustomerId == Customer.Id && m.RemovedAt == null).ToListAsync();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        await Fill();
        return Lines.Count == 0 ? Redirect("/shop/cart") : Page();
    }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync(string payWith, int? methodId, string street, string city, string zip)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        await Fill();
        if (Lines.Count == 0) return Redirect("/shop/cart");
        if (payWith == "card" && !Methods.Any(m => m.Id == methodId)) { Problem = "Pick a card."; return Page(); }
        var sub = Subtotal; var tax = Math.Round(sub * Cart.TaxRate, 2); var ship = Cart.Shipping(sub); var total = sub + tax + ship;
        var stars = Lines.Sum(l => l.Product.Stars * l.Qty);
        var order = new ShopOrder
        {
            Number = $"SO-{DateTime.UtcNow:yyMMddHHmmss}", CustomerId = Customer.Id, Subtotal = sub, Tax = tax, Shipping = ship, Total = total, Stars = stars,
            Items = new JsonArray(Lines.Select(l => (JsonNode?)new JsonObject { ["sku"] = l.Product.Sku, ["name"] = l.Product.Name, ["qty"] = l.Qty, ["price"] = l.Product.Price }).ToArray()),
            PayWith = payWith == "bill" ? "bill" : "card", PaymentMethodId = payWith == "card" ? methodId : null, ShipTo = new JsonObject { ["name"] = Customer.Name, ["street"] = street, ["city"] = city, ["state"] = "TX", ["zip"] = zip },
            Status = "placed", PaymentStatus = payWith == "bill" ? "on bill" : Live("STRIPE_SECRET") ? "paid" : "pending",
        };
        Db.ShopOrders.Add(order);
        var c = await Tracked();
        if (payWith == "bill") Db.LedgerEntries.Add(new LedgerEntry { CustomerId = c.Id, Kind = "debit", Amount = total, Description = $"Shop order {order.Number}", Status = "pending" });
        c.Stars += stars;
        Db.StarEntries.Add(new StarEntry { CustomerId = c.Id, Stars = stars, Reason = $"Shop order {order.Number}" });
        await Db.SaveChangesAsync();
        cart.Save([]);
        TempData["flash"] = $"Order {order.Number} placed. You earned {stars:N0} stars!";
        return Redirect($"/myaccount/orders/{order.Id}");
    }
}

public class OrdersModel(AppDbContext db) : AccountPage(db)
{
    public List<ShopOrder> Orders { get; private set; } = [];
    public ShopOrder? Order { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        Orders = await Db.ShopOrders.Where(o => o.CustomerId == Customer.Id).OrderByDescending(o => o.CreatedAt).ToListAsync();
        if (id is { } i && (Order = Orders.FirstOrDefault(o => o.Id == i)) is null) return NotFound();
        return Page();
    }
}

public class BillPdfModel(AppDbContext db) : MyAccountPage(db)
{
    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadCustomerAsync()) return await SignedOut();
        var b = await Db.Bills.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == Customer.Id);
        if (b is null) return NotFound();
        var brand = AppConfig.At("brand.name")?.ToString() ?? "Energy Texas";
        var pdf = new SimplePdf().Box(0, 0, SimplePdf.Width, 70, "#102247").Text(54, 22, brand, 18, true, "#FFFFFF").Text(400, 26, "Electricity Bill", 12, true, "#FFFFFF");
        var y = 100.0;
        foreach (var (k, v) in new[] { ("Account", Customer.Account), ("Name", Customer.Name), ("Service address", $"{Customer.Address}, {Customer.City} {Customer.Zip}"), ("Invoice", b.Invoice ?? b.Reference), ("Bill date", b.BilledOn.ToString("MMMM d, yyyy")), ("Service period", $"{b.PeriodStart:MMM d} – {b.PeriodEnd:MMM d, yyyy}"), ("Usage", $"{b.Kwh:N0} kWh"), ("Plan", Customer.Plan?.Name ?? "") })
        { pdf.Text(54, y, k, 10, false, "#96999d").Text(200, y, v, 11); y += 22; }
        pdf.Line(54, y + 6, SimplePdf.Width - 54, y + 6).Text(54, y + 20, "Amount due", 14, true, "#102247").Text(400, y + 20, b.Amount.ToString("C"), 14, true, "#102247");
        pdf.Text(54, y + 46, $"Due {b.DueOn:MMMM d, yyyy}. Pay online at /myaccount/pay or set up AutoPay.", 10);
        return File(pdf.Output($"Bill {b.Invoice}"), "application/pdf", $"bill-{b.Invoice ?? b.Reference}.pdf");
    }
}

/// <summary>QuickPay: pay without signing in (account number + service zip).</summary>
[AllowAnonymous, IgnoreAntiforgeryToken]
public class QuickPayModel(AppDbContext db) : PageModel
{
    public Customer? Customer { get; private set; }
    public string? Message { get; private set; }
    public string? Problem { get; private set; }
    [BindProperty(SupportsGet = true)] public decimal? Amount { get; set; }

    public async Task OnGetAsync(string? account, string? zip)
    {
        if (account != null) { Customer = await db.Customers.FirstOrDefaultAsync(c => c.Account == account.Trim() && c.Zip == (zip ?? "").Trim()); if (Customer is null) Problem = "We couldn't find that account and zip."; }
    }

    [EnableRateLimiting("forms")]
    public async Task OnPostAsync(string account, string zip, decimal amount, string card, string expires)
    {
        await OnGetAsync(account, zip);
        if (Customer is null) return;
        var digits = new string((card ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 12 || amount <= 0 || amount > 5000) { Problem = "Check the card number and amount."; return; }
        var live = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STRIPE_SECRET"));
        db.Payments.Add(new Payment { CustomerId = Customer.Id, Reference = $"QP-{Customer.Account[^6..]}-{DateTime.UtcNow:yyMMddHHmmss}", PaidOn = DateOnly.FromDateTime(DateTime.Today), Amount = amount, Method = "Card", Source = "QuickPay", Status = live ? "Success" : "Pending", Confirmation = $"x{digits[^4..]}" });
        if (live) await db.Customers.Where(c => c.Id == Customer.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Balance, c => c.Balance - amount));
        await db.SaveChangesAsync();
        Message = live ? $"Thank you! {amount:C} paid." : $"Thank you! Your {amount:C} payment is pending (card processing isn't connected yet).";
    }
}

/// <summary>Create a My Account login for an existing account (account number, zip and the email on file).</summary>
[AllowAnonymous]
public class RegisterModel(AppDbContext db) : PageModel
{
    public string? Problem { get; private set; }

    public IActionResult OnGet() => Page();

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync(string account, string zip, string email, string username, string password, string confirm)
    {
        var c = await db.Customers.AsTracking().FirstOrDefaultAsync(x => x.Account == account.Trim() && x.Zip == zip.Trim());
        if (c is null || !string.Equals(c.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)) { Problem = "Those details don't match an account."; return Page(); }
        if (c.Password != null) { Problem = "This account already has a login. Use Forgot Password if you need it."; return Page(); }
        username = username.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[a-z0-9._-]{4,40}$")) { Problem = "Usernames are 4–40 letters, numbers, dots, dashes or underscores."; return Page(); }
        if (await db.Customers.AnyAsync(x => x.Username == username)) { Problem = "That username is taken."; return Page(); }
        if (password.Length < 8 || password != confirm) { Problem = "Use at least 8 characters, and type it the same twice."; return Page(); }
        c.Username = username; c.Password = Passwords.Hash(password);
        await db.SaveChangesAsync();
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, c.Id.ToString()), new Claim(ClaimTypes.Name, c.FirstName ?? c.Name)], CookieAuthenticationDefaults.AuthenticationScheme)));
        return Redirect("/myaccount");
    }
}

/// <summary>Forgot password / username: a reset link (or the username) goes to the email on file. Mail is logged until set up.</summary>
[AllowAnonymous]
public class ForgotModel(AppDbContext db, IWebHostEnvironment env) : PageModel
{
    public string What { get; private set; } = "password";
    public string? Message { get; private set; }
    public string? DevLink { get; private set; }

    public IActionResult OnGet(string what) { if (what is not ("password" or "username")) return NotFound(); What = what; return Page(); }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync(string what, string email)
    {
        What = what;
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Email == email.Trim().ToLower() && x.Password != null);
        Message = "If that email has a My Account login, we've sent " + (what == "username" ? "your username" : "a link to reset your password") + " to it.";
        if (c is null) return Page();
        if (what == "password")
        {
            var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            await db.CustomerPasswordResets.Where(r => r.CustomerId == c.Id).ExecuteDeleteAsync();
            db.CustomerPasswordResets.Add(new CustomerPasswordReset { CustomerId = c.Id, Token = Passwords.Hash(token) });
            db.ContactLogs.Add(new ContactLog { CustomerId = c.Id, Channel = "Email", Template = "MyAccount - Password Reset", Status = "logged", SentAt = DateTime.UtcNow, Direction = "out" });
            await db.SaveChangesAsync();
            if (env.IsDevelopment()) DevLink = $"/myaccount/reset-password/{c.Id}-{token}";
        }
        else if (env.IsDevelopment()) DevLink = $"(Development) the username is {c.Username}";
        return Page();
    }
}

[AllowAnonymous]
public class ResetModel(AppDbContext db) : PageModel
{
    public string? Problem { get; private set; }

    private async Task<CustomerPasswordReset?> Find(string token)
    {
        var dash = token.IndexOf('-');
        if (dash < 1 || !int.TryParse(token[..dash], out var id)) return null;
        var r = await db.CustomerPasswordResets.AsTracking().FirstOrDefaultAsync(x => x.CustomerId == id);
        return r != null && r.CreatedAt > DateTime.UtcNow.AddMinutes(-60) && Passwords.Verify(r.Token, token[(dash + 1)..]) ? r : null;
    }

    public async Task<IActionResult> OnGetAsync(string token) { if (await Find(token) is null) Problem = "That link has expired. Ask for a new one."; return Page(); }

    [EnableRateLimiting("forms")]
    public async Task<IActionResult> OnPostAsync(string token, string password, string confirm)
    {
        var r = await Find(token);
        if (r is null) { Problem = "That link has expired. Ask for a new one."; return Page(); }
        if (password.Length < 8 || password != confirm) { Problem = "Use at least 8 characters, and type it the same twice."; return Page(); }
        var c = await db.Customers.AsTracking().FirstAsync(x => x.Id == r.CustomerId);
        c.Password = Passwords.Hash(password);
        db.CustomerPasswordResets.Remove(r);
        await db.SaveChangesAsync();
        TempData["flash"] = "Password changed. Sign in with the new one.";
        return Redirect("/myaccount/login");
    }
}
