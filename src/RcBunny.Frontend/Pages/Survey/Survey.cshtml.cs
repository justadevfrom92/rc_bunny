using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Frontend.Pages.Survey;

/// <summary>
/// A customer survey (built in Rodeo → Surveys), like ebunny's SurveyController. Survey emails link here with a signed
/// ?c=account so the answer is tied to that customer; a signed-in My Account customer is used otherwise, and anyone else
/// answers anonymously.
/// </summary>
public class SurveyModel(AppDbContext db) : PageModel
{
    public Data.Survey Survey { get; private set; } = null!;
    public Customer? Customer { get; private set; }
    public List<List<SurveyQuestion>> Groups { get; private set; } = [];
    public string? Error { get; private set; }
    public bool Thanks { get; private set; }
    public Dictionary<int, List<string>> Old { get; private set; } = [];

    private bool Signed => Request.Query["c"] is { Count: 1 } c && Request.Query["signature"] == Rodeo.Sign(Survey.Slug, c[0]!);

    private async Task<bool> LoadAsync(string slug)
    {
        var s = await db.Surveys.Include(x => x.Questions).ThenInclude(q => q.Category).FirstOrDefaultAsync(x => x.Slug == slug);
        if (s == null) return false;
        Survey = s;
        Groups = s.Questions.OrderBy(q => q.Position).GroupBy(q => q.SurveyCategoryId ?? 0).Select(g => g.ToList()).ToList();
        // The customer this answer belongs to: from the signed email link, else the My Account sign-in
        if (Signed) Customer = await db.Customers.FirstOrDefaultAsync(c => c.Account == Request.Query["c"].ToString());
        else if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) Customer = await db.Customers.FindAsync(id);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        if (!await LoadAsync(slug)) return NotFound();
        Thanks = TempData["thanks"] is true;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug)
    {
        if (!await LoadAsync(slug)) return NotFound();
        if (Survey.State != "open") return StatusCode(410);
        var answers = new Dictionary<string, object>();
        foreach (var q in Survey.Questions.OrderBy(q => q.Position))
        {
            var given = Request.Form[$"a[{q.Id}]"].Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).ToList();
            Old[q.Id] = given;
            if (given.Count == 0) { if (q.Required) Error ??= $"Please answer “{q.Question}”"; continue; }
            var ok = q.Type switch
            {
                "rating" => int.TryParse(given[0], out var n) && n is >= 0 and <= 10,
                "text" => given[0].Length <= 2000,
                "multi" => given.All(q.Choices().Contains),
                _ => q.Choices().Contains(given[0]),
            };
            if (!ok) { Error ??= $"Please pick one of the answers for “{q.Question}”"; continue; }
            answers[q.Id.ToString()] = q.Type == "multi" ? given : given[0];
        }
        if (Error != null) return Page();
        db.SurveyResponses.Add(new SurveyResponse
        {
            SurveyId = Survey.Id, CustomerId = Customer?.Id, Answers = JsonSerializer.Serialize(answers),
            Source = Signed ? "email" : User.Identity?.IsAuthenticated == true ? "my-account" : "website",
        });
        await db.SaveChangesAsync();
        TempData["thanks"] = true;
        return Redirect($"/survey/{Survey.Slug}");
    }
}
