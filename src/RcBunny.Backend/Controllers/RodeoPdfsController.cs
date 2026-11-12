using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Services;

namespace RcBunny.Controllers;

/// <summary>
/// Rodeo → PDFs (ebunny's PdfTemplateController and PdfPartController): PDF templates (letters and notices) written like
/// email templates, with fill-ins, a preview for any account, and Make PDF to create one for an account; every PDF made
/// is kept with the wording it was made from. And the PDF headers and footers templates pick from.
/// </summary>
[ApiController]
[Route("api/rodeo")]
[RequirePermission("rodeo")]
public class RodeoPdfsController(AppDbContext db, IConfiguration config, IWebHostEnvironment env, Mailer mailer) : ControllerBase
{
    private async Task<User> Me() => (await db.Users.FindAsync((await Permissions.CurrentAsync(HttpContext))!.Id))!;

    /// <summary>The pages as SVG (to show) and the PDF itself (base64, for View and Download).</summary>
    private static object Pages(SimplePdf pdf, string title) => new { pages = pdf.Svgs(), pdf64 = Convert.ToBase64String(pdf.Output(title)) };

    // ---- PDF templates ------------------------------------------------------------------------------------------

    [HttpGet("pdfs")]
    public async Task<object> Index(string? category, int page = 1)
    {
        var only = Rodeo.PdfCategories.Contains(category) ? category : null;
        var q = db.PdfTemplates.Where(t => only == null || t.Category == only);
        var made = await db.PdfGenerations.GroupBy(g => g.PdfTemplateId).Select(g => new { g.Key, n = g.Count(), last = g.Max(x => x.CreatedAt) }).ToDictionaryAsync(x => x.Key);
        return new
        {
            only, categories = Rodeo.PdfCategories, total = await q.CountAsync(), page,
            counts = await db.PdfTemplates.Where(t => t.Category != null).GroupBy(t => t.Category!).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n),
            all = await db.PdfTemplates.CountAsync(),
            templates = (await q.OrderBy(t => t.Name).Skip((Math.Max(page, 1) - 1) * 10).Take(10).ToListAsync())
                .Select(t => new { t.Id, t.Name, t.Title, t.Category, t.Status, t.UpdatedAt, made = made.GetValueOrDefault(t.Id)?.n ?? 0, lastMade = made.GetValueOrDefault(t.Id)?.last }),
        };
    }

    private async Task<Customer?> FindAccountAsync(string? account) => string.IsNullOrWhiteSpace(account) ? null : await db.Customers.FirstOrDefaultAsync(c => c.Account == account.Trim());

    /// <summary>A template's page (or New PDF Template): its fields, the preview for an account, tests and PDFs made, and customers found for Make PDF.</summary>
    [HttpGet("pdfs/new")]
    public Task<IActionResult> NewForm(string? account) => Form(null, account, null);

    [HttpGet("pdfs/{id:int}")]
    public async Task<IActionResult> Form(int? id, string? account, string? find)
    {
        var t = id == null ? new PdfTemplate { Title = "Notice", Body = "Hi {{first_name}},\n\n", ShowAddress = true, Status = "Draft", Category = "Billing" } : await db.PdfTemplates.FindAsync(id);
        if (t == null) return NotFound();
        var sample = await FindAccountAsync(account) ?? await db.Customers.OrderBy(c => c.Id).FirstOrDefaultAsync();
        var acct = await PdfTemplates.AccountAsync(db, sample?.Id);
        var pdf = PdfTemplates.Build(await PdfTemplates.DocumentAsync(db, t), acct);
        var parts = await db.PdfParts.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name).Select(p => new { p.Id, p.Kind, p.Name, p.IsDefault }).ToListAsync();
        return Ok(new
        {
            template = new { id, t.Name, t.Category, t.Status, t.Title, t.Body, t.Footer, t.PdfHeaderId, t.PdfFooterId, t.ShowAddress, t.UpdatedAt },
            categories = Rodeo.PdfCategories, headers = parts.Where(p => p.Kind == "header"), footers = parts.Where(p => p.Kind == "footer"),
            sample = sample == null ? null : new { sample.Account, sample.Name, sample.FirstName },
            fields = PdfTemplates.Fields.Select(f => new { key = f.Key, label = f.Value, value = PdfTemplates.Fill("{{" + f.Key + "}}", acct) }),
            preview = Pages(pdf, t.Title), fileName = PdfTemplates.FileName(t, sample), made = id == null ? 0 : await db.PdfGenerations.CountAsync(g => g.PdfTemplateId == id),
            tests = id == null ? [] : (await db.PdfTestSends.Include(x => x.User).Include(x => x.Customer).Where(x => x.PdfTemplateId == id).OrderByDescending(x => x.Id).Take(10).ToListAsync())
                .Select(x => (object)new { x.Id, x.CreatedAt, x.Recipients, account = x.Customer?.Account, by = x.User?.Name, x.Status, x.Error }),
            recent = id == null ? [] : (await db.PdfGenerations.Include(g => g.Customer).Include(g => g.User).Where(g => g.PdfTemplateId == id).OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id).Take(10).ToListAsync())
                .Select(g => (object)new { g.Id, g.CreatedAt, g.Pages, by = g.User?.Name, customer = g.Customer == null ? null : new { g.Customer.Id, g.Customer.Account, g.Customer.Name } }),
            // Make a PDF for an Account: customers matching the search
            found = id == null || string.IsNullOrWhiteSpace(find) ? null : await Find(find),
            delivers = mailer.Delivers,
        });
    }

    /// <summary>Customers by name, phone, account, email or address (ebunny's CustomerFinder).</summary>
    private async Task<object> Find(string q)
    {
        var like = $"%{q.Trim()}%";
        var digits = new string(q.Where(char.IsDigit).ToArray());
        var phone = digits.Length >= 4 ? $"%{digits}%" : null;
        return await db.Customers.Where(c => EF.Functions.Like(c.Name, like) || EF.Functions.Like(c.LastName!, like) || EF.Functions.Like(c.Account, like) || EF.Functions.Like(c.Email!, like)
                || EF.Functions.Like(c.Address, like) || (phone != null && EF.Functions.Like(c.Phone!.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", ""), phone)))
            .OrderBy(c => c.Name).Take(25).Select(c => new { c.Id, c.Account, c.Name, c.City, c.Phone, c.Email, c.Status }).ToListAsync();
    }

    public record TemplateInput(string? Name, string? Category, string? Title, string? Body, string? Footer, int? PdfHeaderId, int? PdfFooterId, bool ShowAddress, string? Status);

    [HttpPost("pdfs")]
    public Task<IActionResult> Add(TemplateInput i) => Save(null, i);

    [HttpPut("pdfs/{id:int}")]
    public Task<IActionResult> Update(int id, TemplateInput i) => Save(id, i);

    private async Task<IActionResult> Save(int? id, TemplateInput i)
    {
        var t = id == null ? new PdfTemplate() : await db.PdfTemplates.FindAsync(id);
        if (t == null) return NotFound();
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 100) return BadRequest(new { error = "Give the template a name (up to 100 characters)." });
        if (await db.PdfTemplates.AnyAsync(x => x.Name == i.Name.Trim() && x.Id != t.Id)) return BadRequest(new { error = "Another PDF template has that name." });
        if (!string.IsNullOrEmpty(i.Category) && !Rodeo.PdfCategories.Contains(i.Category)) return BadRequest(new { error = "Pick a category." });
        if (string.IsNullOrWhiteSpace(i.Title) || i.Title.Length > 120) return BadRequest(new { error = "Write the title (up to 120 characters)." });
        if (string.IsNullOrWhiteSpace(i.Body)) return BadRequest(new { error = "Write the body." });
        if (i.Footer is { Length: > 600 }) return BadRequest(new { error = "Keep the footer to 600 characters." });
        if (i.Status is not ("Active" or "Draft")) return BadRequest(new { error = "Pick Active or Draft." });
        if (i.PdfHeaderId != null && !await db.PdfParts.AnyAsync(p => p.Id == i.PdfHeaderId && p.Kind == "header")) return BadRequest(new { error = "Pick a header." });
        if (i.PdfFooterId != null && !await db.PdfParts.AnyAsync(p => p.Id == i.PdfFooterId && p.Kind == "footer")) return BadRequest(new { error = "Pick a footer." });
        (t.Name, t.Category, t.Title, t.Body, t.Footer) = (i.Name.Trim(), string.IsNullOrEmpty(i.Category) ? null : i.Category, i.Title.Trim(), i.Body, string.IsNullOrWhiteSpace(i.Footer) ? null : i.Footer.Trim());
        (t.PdfHeaderId, t.PdfFooterId, t.ShowAddress, t.Status, t.UpdatedAt) = (i.PdfHeaderId, i.PdfFooterId, i.ShowAddress, i.Status, DateTime.UtcNow);
        if (id == null) db.PdfTemplates.Add(t);
        await db.SaveChangesAsync();
        return Ok(new { t.Id, message = "PDF template saved" });
    }

    [HttpDelete("pdfs/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await db.PdfTemplates.FindAsync(id) is not { } t) return NotFound();
        db.PdfTemplates.Remove(t); // the PDFs made from it go too
        await db.SaveChangesAsync();
        return Ok(new { message = $"{t.Name} deleted" });
    }

    public record AccountInput(string? Account);

    /// <summary>Make the PDF for an account and keep it (with a note on the account).</summary>
    [HttpPost("pdfs/{id:int}/make")]
    public async Task<IActionResult> Make(int id, AccountInput i)
    {
        if (await db.PdfTemplates.FindAsync(id) is not { } t) return NotFound();
        if (await FindAccountAsync(i.Account) is not { } c) return BadRequest(new { error = "No account has that number." });
        var doc = await PdfTemplates.DocumentAsync(db, t);
        var pdf = PdfTemplates.Build(doc, await PdfTemplates.AccountAsync(db, c.Id));
        var me = await Me();
        var g = new PdfGeneration
        {
            PdfTemplateId = t.Id, CustomerId = c.Id, UserId = me.Id, FileName = PdfTemplates.FileName(t, c), Pages = pdf.PageCount, Size = pdf.Output(t.Title).Length,
            Title = doc.Title, Body = doc.Body, Footer = doc.Footer, ShowAddress = doc.ShowAddress, Header = doc.Header == null ? null : JsonSerializer.Serialize(doc.Header),
        };
        db.PdfGenerations.Add(g);
        db.Notes.Add(new Note { CustomerId = c.Id, UserId = me.Id, Author = me.Name, Category = "Contact", Action = "Contact - Letter", Priority = "Low", Body = $"Made the PDF \"{t.Name}\" ({g.FileName})." });
        await db.SaveChangesAsync();
        return Ok(new { g.Id, message = $"PDF made for account {c.Account}" });
    }

    public record TestInput(string? To, string? Account);

    /// <summary>Send Test: the PDF (filled in for an account) attached to an email, marked as a test, to up to 5 addresses.</summary>
    [HttpPost("pdfs/{id:int}/test")]
    public async Task<IActionResult> Test(int id, TestInput i)
    {
        if (await db.PdfTemplates.FindAsync(id) is not { } t) return NotFound();
        var to = Regex.Split(i.To ?? "", @"[\s,;]+").Select(e => e.Trim().ToLowerInvariant()).Where(e => e != "").Distinct().ToList();
        var bad = to.FirstOrDefault(e => !Regex.IsMatch(e, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"));
        if (to.Count > 5) return BadRequest(new { error = "Send a test to 5 addresses at most." });
        if (to.Count == 0 || bad != null) return BadRequest(new { error = $"Not an email address: {bad ?? "(none)"}" });
        if (!string.IsNullOrWhiteSpace(i.Account) && await FindAccountAsync(i.Account) == null) return BadRequest(new { error = "No account has that number." });
        var c = await FindAccountAsync(i.Account) ?? await db.Customers.OrderBy(x => x.Id).FirstAsync();
        var acct = await PdfTemplates.AccountAsync(db, c.Id);
        var pdf = PdfTemplates.Build(await PdfTemplates.DocumentAsync(db, t), acct).Output(t.Title);
        var me = await Me();
        var send = new PdfTestSend { PdfTemplateId = t.Id, UserId = me.Id, CustomerId = c.Id, Recipients = string.Join(", ", to) };
        var subject = $"[TEST] {PdfTemplates.Fill(t.Title, acct)}";
        var html = $"<p><b>Test PDF</b> from Rodeo, sent by {System.Net.WebUtility.HtmlEncode(me.Name)}: {System.Net.WebUtility.HtmlEncode(t.Name)}, filled in for account {c.Account}. The PDF is attached.</p>";
        try
        {
            foreach (var addr in to) await mailer.SendAsync(addr, null, subject, html, new Attachment(new MemoryStream(pdf), PdfTemplates.FileName(t, c), "application/pdf"));
            send.Status = "sent";
        }
        catch (Exception e) { (send.Status, send.Error) = ("failed", e.Message.Length > 500 ? e.Message[..500] : e.Message); }
        db.PdfTestSends.Add(send);
        await db.SaveChangesAsync();
        if (send.Status == "failed") return BadRequest(new { error = $"The test did not send: {send.Error}" });
        return Ok(new { message = $"Test PDF sent to {send.Recipients}" + (mailer.Delivers ? "" : " (written to the mail log: set an SMTP host to deliver to inboxes)") });
    }

    // ---- PDFs made ----------------------------------------------------------------------------------------------

    [HttpGet("pdfs/made")]
    public async Task<object> Made(int? template, int page = 1)
    {
        var q = db.PdfGenerations.Include(g => g.Template).Include(g => g.Customer).Include(g => g.User).Where(g => template == null || g.PdfTemplateId == template);
        return new
        {
            only = template, total = await q.CountAsync(), page,
            templates = await db.PdfTemplates.OrderBy(t => t.Name).Select(t => new { t.Id, t.Name }).ToListAsync(),
            rows = (await q.OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id).Skip((Math.Max(page, 1) - 1) * 10).Take(10).ToListAsync()).Select(g => new
            {
                g.Id, g.CreatedAt, g.FileName, g.Size, g.Pages, by = g.User?.Name, template = g.Template == null ? null : new { g.Template.Name, g.Template.Category },
                customer = g.Customer == null ? null : new { g.Customer.Id, g.Customer.Account, g.Customer.Name },
            }),
        };
    }

    [HttpGet("pdfs/made/{id:int}")]
    public async Task<IActionResult> MadeShow(int id)
    {
        var g = await db.PdfGenerations.Include(x => x.Template).Include(x => x.Customer).Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (g == null) return NotFound();
        var pdf = PdfTemplates.Build(PdfTemplates.DocumentOf(g), await PdfTemplates.AccountAsync(db, g.CustomerId));
        return Ok(new
        {
            made = new { g.Id, g.Title, g.FileName, g.Pages, g.Size, g.CreatedAt, by = g.User?.Name, template = g.Template == null ? null : new { g.Template.Id, g.Template.Name },
                customer = g.Customer == null ? null : new { g.Customer.Id, g.Customer.Account, g.Customer.Name } },
            preview = Pages(pdf, g.Title),
        });
    }

    /// <summary>The PDF file itself (inline, or ?download=1).</summary>
    [HttpGet("pdfs/made/{id:int}/pdf")]
    public async Task<IActionResult> MadePdf(int id, bool download = false)
    {
        if (await db.PdfGenerations.FindAsync(id) is not { } g) return NotFound();
        var bytes = PdfTemplates.Build(PdfTemplates.DocumentOf(g), await PdfTemplates.AccountAsync(db, g.CustomerId)).Output(g.Title);
        Response.Headers.CacheControl = "private, no-store";
        return download ? File(bytes, "application/pdf", g.FileName) : File(bytes, "application/pdf");
    }

    // ---- PDF headers & footers ----------------------------------------------------------------------------------

    [HttpGet("pdf-headers-footers")]
    public async Task<object> Parts()
    {
        var parts = await db.PdfParts.OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name).ToListAsync();
        var headers = await db.PdfTemplates.Where(t => t.PdfHeaderId != null).GroupBy(t => t.PdfHeaderId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var footers = await db.PdfTemplates.Where(t => t.PdfFooterId != null).GroupBy(t => t.PdfFooterId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var none = new { header = await db.PdfTemplates.CountAsync(t => t.PdfHeaderId == null), footer = await db.PdfTemplates.CountAsync(t => t.PdfFooterId == null) };
        return Rodeo.PartKinds.ToDictionary(k => k.Key, k => parts.Where(p => p.Kind == k.Key).Select(p => new
        {
            p.Id, p.Name, p.IsDefault, p.UpdatedAt,
            looks = k.Key == "header" ? $"{(string.IsNullOrEmpty(p.Title) ? Rodeo.Brand : p.Title)} · {Rodeo.PdfColors.GetValueOrDefault(p.Color, p.Color)}" : string.IsNullOrEmpty(p.Body) ? "(empty)" : p.Body,
            templates = (k.Key == "header" ? headers : footers).GetValueOrDefault(p.Id) + (p.IsDefault ? (k.Key == "header" ? none.header : none.footer) : 0),
        }));
    }

    [HttpGet("pdf-headers-footers/new")]
    public Task<IActionResult> NewPart(string? kind) => Part(null, kind);

    [HttpGet("pdf-headers-footers/{id:int}")]
    public async Task<IActionResult> Part(int? id, string? kind)
    {
        var p = id == null ? new PdfPart { Kind = kind == "header" ? "header" : "footer", Color = "navy" } : await db.PdfParts.FindAsync(id);
        if (p == null) return NotFound();
        // Preview: a short sample letter with this header or footer
        var defHeader = await db.PdfParts.FirstOrDefaultAsync(x => x.Kind == "header" && x.IsDefault);
        var defFooter = await db.PdfParts.FirstOrDefaultAsync(x => x.Kind == "footer" && x.IsDefault);
        var header = p.Kind == "header" ? p : defHeader;
        var doc = new PdfTemplates.Doc("Sample Letter", $"Hi {{{{first_name}}}},\n\nThis is how the {p.Kind} looks on a PDF.", p.Kind == "footer" ? p.Body : defFooter?.Body, true,
            header == null ? null : new PdfTemplates.Header(header.Title, header.Subtitle, header.Color));
        var sample = await db.Customers.OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync();
        var pdf = PdfTemplates.Build(doc, await PdfTemplates.AccountAsync(db, sample));
        var templates = id == null ? [] : await (p.Kind == "header" ? db.PdfTemplates.Where(t => t.PdfHeaderId == id || (p.IsDefault && t.PdfHeaderId == null))
            : db.PdfTemplates.Where(t => t.PdfFooterId == id || (p.IsDefault && t.PdfFooterId == null))).OrderBy(t => t.Name).Select(t => new { t.Id, t.Name, t.Category, t.Status }).ToListAsync();
        return Ok(new { part = new { id, p.Kind, p.Name, p.Title, p.Subtitle, p.Color, p.Body, p.IsDefault, p.UpdatedAt }, colors = Rodeo.PdfColors, preview = Pages(pdf, "Sample"), templates });
    }

    public record PartInput(string? Kind, string? Name, string? Title, string? Subtitle, string? Color, string? Body, bool IsDefault);

    [HttpPost("pdf-headers-footers")]
    public Task<IActionResult> AddPart(PartInput i) => SavePart(null, i);

    [HttpPut("pdf-headers-footers/{id:int}")]
    public Task<IActionResult> UpdatePart(int id, PartInput i) => SavePart(id, i);

    private async Task<IActionResult> SavePart(int? id, PartInput i)
    {
        var p = id == null ? new PdfPart { Kind = i.Kind ?? "" } : await db.PdfParts.FindAsync(id);
        if (p == null) return NotFound();
        if (!Rodeo.PartKinds.ContainsKey(p.Kind)) return BadRequest(new { error = "A header or a footer?" });
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 100) return BadRequest(new { error = "Give it a name (up to 100 characters)." });
        if (await db.PdfParts.AnyAsync(x => x.Kind == p.Kind && x.Name == i.Name.Trim() && x.Id != p.Id)) return BadRequest(new { error = $"Another {p.Kind} has that name." });
        if (i.Color != null && !Rodeo.PdfColors.ContainsKey(i.Color)) return BadRequest(new { error = "Pick a look." });
        if (i.Title is { Length: > 120 } || i.Subtitle is { Length: > 160 } || i.Body is { Length: > 600 }) return BadRequest(new { error = "That's too long." });
        string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        (p.Name, p.Title, p.Subtitle, p.Color, p.Body, p.UpdatedAt) = (i.Name.Trim(), Blank(i.Title), Blank(i.Subtitle), i.Color ?? "navy", Blank(i.Body), DateTime.UtcNow);
        if (id == null) db.PdfParts.Add(p);
        await db.SaveChangesAsync();
        if (i.IsDefault && !p.IsDefault)
        {
            await db.PdfParts.Where(x => x.Kind == p.Kind && x.Id != p.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false));
            p.IsDefault = true;
            await db.SaveChangesAsync();
        }
        return Ok(new { p.Id, message = $"PDF {p.Kind} saved" });
    }

    /// <summary>Templates using it go back to the default. The default itself can't be deleted.</summary>
    [HttpDelete("pdf-headers-footers/{id:int}")]
    public async Task<IActionResult> DeletePart(int id)
    {
        if (await db.PdfParts.FindAsync(id) is not { } p) return NotFound();
        if (p.IsDefault) return UnprocessableEntity(new { error = "Pick another default first." });
        if (p.Kind == "header") await db.PdfTemplates.Where(t => t.PdfHeaderId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.PdfHeaderId, (int?)null));
        else await db.PdfTemplates.Where(t => t.PdfFooterId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.PdfFooterId, (int?)null));
        db.PdfParts.Remove(p);
        await db.SaveChangesAsync();
        return Ok(new { message = $"{p.Name} deleted. Its templates use the default {p.Kind} now." });
    }
}
