using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Services;

namespace RcBunny.Controllers;

/// <summary>
/// Rodeo → Surveys (ebunny's SurveyController and SurveyEmailController): every survey with its status and scores, the
/// survey builder (questions picked from the question bank, filed under categories, with answers from answer sets),
/// results by category, all responses, the bank, answer sets and categories, and the automatic survey emails.
/// </summary>
[ApiController]
[Route("api/rodeo")]
[RequirePermission("rodeo")]
public class RodeoSurveysController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private string Website => (config["Website:Url"] ?? "http://localhost:5100").TrimEnd('/');
    private static List<string> Opts(string? json) => json == null ? [] : JsonSerializer.Deserialize<List<string>>(json)!;

    // ---- All surveys --------------------------------------------------------------------------------------------

    [HttpGet("surveys")]
    public async Task<object> Index(string? state)
    {
        var surveys = await db.Surveys.Include(s => s.Questions).ThenInclude(q => q.Category).OrderByDescending(s => s.CreatedAt).ToListAsync();
        var responses = (await db.SurveyResponses.ToListAsync()).ToLookup(r => r.SurveyId);
        var since = DateTime.UtcNow.AddDays(-30);
        var recent = responses.SelectMany(g => g).Where(r => r.CreatedAt >= since).ToList();
        var byId = surveys.ToDictionary(s => s.Id);
        return new
        {
            website = Website, state,
            counts = surveys.GroupBy(s => s.State).ToDictionary(g => g.Key, g => g.Count()),
            recent = recent.Count, all = responses.Sum(g => g.Count()), bank = await db.SurveyBankQuestions.CountAsync(),
            nps = Surveys.Nps(recent.Select(r => byId.TryGetValue(r.SurveyId, out var s) ? Surveys.Score(r, s.Questions) : null).Where(n => n != null).Select(n => n!.Value).ToList()),
            surveys = surveys.Where(s => state == null || s.State == state).Select(s => new
            {
                s.Id, s.Title, s.Slug, s.State, s.StateLabel, s.ClosesOn, responses = responses[s.Id].Count(), nps = Surveys.SurveyNps(s, responses[s.Id]),
                lastAt = responses[s.Id].Select(r => (DateTime?)r.CreatedAt).Max(),
                categories = s.Questions.GroupBy(q => q.Category?.Name ?? "Uncategorized").Select(g => new { name = g.Key, color = g.First().Category?.Color ?? "#96999d", n = g.Count() }),
            }),
        };
    }

    // ---- Builder ------------------------------------------------------------------------------------------------

    private async Task<object> BuilderOptions() => new
    {
        categories = await db.SurveyCategories.OrderBy(c => c.Position).ThenBy(c => c.Name).Select(c => new { c.Id, c.Name, c.Color }).ToListAsync(),
        sets = (await db.SurveyAnswerSets.OrderBy(s => s.Group).ThenBy(s => s.Name).ToListAsync()).Select(s => new { s.Id, s.Name, s.Type, options = Opts(s.Options) }),
        bank = await db.SurveyBankQuestions.Include(b => b.Category).OrderBy(b => b.Question)
            .Select(b => new { b.Id, b.Question, b.Type, category = b.SurveyCategoryId, categoryName = b.Category!.Name, set = b.SurveyAnswerSetId, b.Help }).ToListAsync(),
        types = Rodeo.QuestionTypes,
    };

    [HttpGet("surveys/new")]
    public async Task<object> New() => new { survey = new { status = "active" }, questions = Array.Empty<object>(), responses = 0, options = await BuilderOptions(), website = Website };

    [HttpGet("surveys/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await db.Surveys.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        return Ok(new
        {
            survey = new { s.Id, s.Title, s.Slug, s.Intro, s.ThankYou, s.Status, s.OpensOn, s.ClosesOn, s.State, s.StateLabel },
            questions = s.Questions.OrderBy(q => q.Position).Select(q => new
            {
                q.Id, bank = q.SurveyBankQuestionId, category = q.SurveyCategoryId, q.Question, q.Type, answerSet = q.SurveyAnswerSetId,
                options = q.SurveyAnswerSetId != null ? "" : string.Join("\n", Opts(q.Options)), q.Required, q.Help,
            }),
            responses = await db.SurveyResponses.CountAsync(r => r.SurveyId == id), options = await BuilderOptions(), website = Website,
        });
    }

    public record QuestionInput(int? Id, int? Bank, int? Category, string? Question, string? Type, int? AnswerSet, string? Options, bool Required, string? Help);
    public record SurveyInput(string? Title, string? Intro, string? ThankYou, string? Status, DateOnly? OpensOn, DateOnly? ClosesOn, List<QuestionInput>? Questions);

    [HttpPost("surveys")]
    public Task<IActionResult> Add(SurveyInput i) => Save(null, i);

    [HttpPut("surveys/{id:int}")]
    public Task<IActionResult> Update(int id, SurveyInput i) => Save(id, i);

    private async Task<IActionResult> Save(int? id, SurveyInput i)
    {
        var s = id == null ? null : await db.Surveys.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id);
        if (id != null && s == null) return NotFound();
        if (string.IsNullOrWhiteSpace(i.Title) || i.Title.Length > 150) return BadRequest(new { error = "Give the survey a title (up to 150 characters)." });
        if (i.Status is not ("active" or "closed")) return BadRequest(new { error = "Pick a status." });
        if (i.OpensOn != null && i.ClosesOn < i.OpensOn) return BadRequest(new { error = "It can't close before it opens." });
        var questions = i.Questions ?? [];
        if (questions.Count == 0) return BadRequest(new { error = "Add at least one question." });
        var sets = await db.SurveyAnswerSets.ToDictionaryAsync(x => x.Id);
        var rows = new List<SurveyQuestion>();
        foreach (var (q, n) in questions.Select((q, n) => (q, n)))
        {
            if (string.IsNullOrWhiteSpace(q.Question)) return BadRequest(new { error = "Every question needs its wording." });
            if (q.Type == null || !Rodeo.QuestionTypes.ContainsKey(q.Type)) return BadRequest(new { error = $"Question {n + 1} needs an answer type." });
            var set = q.AnswerSet is { } sid ? sets.GetValueOrDefault(sid) : null;
            List<string>? options = null;
            if (Rodeo.Listed.Contains(q.Type))
            {
                options = set != null ? Opts(set.Options) : (q.Options ?? "").Split('\n').Select(o => o.Trim()).Where(o => o != "").ToList();
                if (options.Count < 2) return BadRequest(new { error = $"Question {n + 1} (“{(q.Question.Length > 40 ? q.Question[..40] + "…" : q.Question)}”) needs an answer set or at least two answers." });
            }
            rows.Add(new SurveyQuestion
            {
                Id = q.Id ?? 0, SurveyCategoryId = q.Category, Question = q.Question.Trim(), Type = q.Type, SurveyAnswerSetId = Rodeo.Listed.Contains(q.Type) ? set?.Id : null,
                Options = options == null ? null : JsonSerializer.Serialize(options), Required = q.Required, Help = string.IsNullOrWhiteSpace(q.Help) ? null : q.Help.Trim(), SurveyBankQuestionId = q.Bank, Position = n,
            });
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        if (s == null)
        {
            var slug = Regex.Replace(i.Title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            s = new Survey { Slug = $"{slug}-{Guid.NewGuid().ToString("N")[..4]}" };
            db.Surveys.Add(s);
        }
        (s.Title, s.Intro, s.ThankYou, s.Status, s.OpensOn, s.ClosesOn, s.UpdatedAt) =
            (i.Title.Trim(), string.IsNullOrWhiteSpace(i.Intro) ? null : i.Intro.Trim(), string.IsNullOrWhiteSpace(i.ThankYou) ? null : i.ThankYou.Trim(), i.Status, i.OpensOn, i.ClosesOn, DateTime.UtcNow);
        var existing = s.Questions.ToDictionary(q => q.Id);
        var kept = new HashSet<int>();
        foreach (var row in rows)
        {
            if (row.Id != 0 && existing.TryGetValue(row.Id, out var q))
            {
                (q.SurveyCategoryId, q.Question, q.Type, q.SurveyAnswerSetId, q.Options, q.Required, q.Help, q.SurveyBankQuestionId, q.Position) =
                    (row.SurveyCategoryId, row.Question, row.Type, row.SurveyAnswerSetId, row.Options, row.Required, row.Help, row.SurveyBankQuestionId, row.Position);
                kept.Add(q.Id);
            }
            else { row.Id = 0; s.Questions.Add(row); }
        }
        // Removed questions go; answers already given to them stay in the responses
        db.SurveyQuestions.RemoveRange(s.Questions.Where(q => q.Id != 0 && !kept.Contains(q.Id)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { s.Id, message = "Survey saved" });
    }

    /// <summary>A copy of a survey's questions and wording as a new closed survey, ready to change.</summary>
    [HttpPost("surveys/{id:int}/copy")]
    public async Task<IActionResult> Copy(int id)
    {
        var s = await db.Surveys.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        var copy = new Survey
        {
            Title = s.Title + " (copy)", Slug = $"{Regex.Replace(s.Title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-')}-{Guid.NewGuid().ToString("N")[..4]}",
            Intro = s.Intro, ThankYou = s.ThankYou, Status = "closed",
            Questions = s.Questions.Select(q => new SurveyQuestion
            {
                SurveyCategoryId = q.SurveyCategoryId, SurveyAnswerSetId = q.SurveyAnswerSetId, SurveyBankQuestionId = q.SurveyBankQuestionId, Question = q.Question, Type = q.Type,
                Options = q.Options, Required = q.Required, Help = q.Help, Position = q.Position,
            }).ToList(),
        };
        db.Surveys.Add(copy);
        await db.SaveChangesAsync();
        return Ok(new { copy.Id, message = "Copied. The copy is closed until you open it." });
    }

    [HttpDelete("surveys/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await db.Surveys.FindAsync(id) is not { } s) return NotFound();
        if (await db.SurveyResponses.AnyAsync(r => r.SurveyId == id)) return UnprocessableEntity(new { error = "Surveys with responses can be closed, not deleted." });
        db.Surveys.Remove(s);
        await db.SaveChangesAsync();
        return Ok(new { message = "Survey deleted" });
    }

    // ---- Results ------------------------------------------------------------------------------------------------

    private async Task<List<SurveyResponse>> FilteredAsync(int id, DateOnly? from, DateOnly? to, string? source)
    {
        var q = db.SurveyResponses.Include(r => r.Customer).Where(r => r.SurveyId == id);
        if (from is { } f) q = q.Where(r => r.CreatedAt >= f.ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        if (to is { } t) q = q.Where(r => r.CreatedAt < t.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        if (!string.IsNullOrEmpty(source)) q = q.Where(r => r.Source == source);
        return await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    [HttpGet("surveys/{id:int}/results")]
    public async Task<IActionResult> Results(int id, DateOnly? from, DateOnly? to, string? source)
    {
        var s = await db.Surveys.Include(x => x.Questions).ThenInclude(q => q.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        var questions = s.Questions.OrderBy(q => q.Position).ToList();
        var responses = await FilteredAsync(id, from, to, source);
        var all = await db.SurveyResponses.Where(r => r.SurveyId == id).Select(r => r.CreatedAt).ToListAsync();
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        var weeks = Enumerable.Range(0, 12).Reverse().Select(i => monday.AddDays(-7 * i))
            .Select(w => new { label = w.ToString("MMM d"), count = all.Count(a => a.ToLocalTime() >= w && a.ToLocalTime() < w.AddDays(7)) });
        var complete = responses.Count(r => { var a = Surveys.Answers(r); return questions.All(q => a.ContainsKey(q.Id)); });
        var firstScale = questions.FirstOrDefault(q => q.Type == "scale");
        // Every question's results, grouped by category in the order they appear in the survey
        var groups = questions.GroupBy(q => q.SurveyCategoryId ?? 0).Select(g => new
        {
            category = g.First().Category is { } c ? new { c.Name, c.Color, c.Description } : null, questions = g.Select(q => Surveys.Question(q, responses)),
        });
        return Ok(new
        {
            survey = new { s.Id, s.Title, s.Slug, s.StateLabel, questions = questions.Count, required = questions.Count(q => q.Required) },
            responses = responses.Count, known = responses.Count(r => r.CustomerId != null), nps = Surveys.SurveyNps(s, responses),
            scale = firstScale == null ? null : Surveys.Question(firstScale, responses), complete = responses.Count == 0 ? (int?)null : (int)Math.Round(100.0 * complete / responses.Count),
            weeks, groups, sources = Rodeo.ResponseSources,
        });
    }

    /// <summary>One row per response, one column per question.</summary>
    [HttpGet("surveys/{id:int}/export")]
    public async Task<IActionResult> Export(int id, DateOnly? from, DateOnly? to, string? source)
    {
        var s = await db.Surveys.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        var questions = s.Questions.OrderBy(q => q.Position).ToList();
        string Cell(string? v)
        {
            v ??= "";
            // Keep spreadsheet apps from running cell text as a formula
            if (Regex.IsMatch(v, "^[=+\\-@]")) v = "'" + v;
            return v.Contains('"') || v.Contains(',') || v.Contains('\n') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
        }
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", new[] { "Response", "Date", "Account", "Customer", "Source" }.Concat(questions.Select(q => q.Question)).Select(Cell)));
        foreach (var r in await FilteredAsync(id, from, to, source))
        {
            var a = Surveys.Answers(r);
            csv.AppendLine(string.Join(",", new[] { r.Id.ToString(), r.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), r.Customer?.Account, r.Customer?.Name, Rodeo.ResponseSources.GetValueOrDefault(r.Source ?? "", r.Source) }
                .Concat(questions.Select(q => a.TryGetValue(q.Id, out var v) ? string.Join("; ", Surveys.Values(v)) : null)).Select(Cell)));
        }
        var name = $"{Regex.Replace(s.Title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-')}-responses-{DateTime.Today:yyyy-MM-dd}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", name);
    }

    // ---- Responses ----------------------------------------------------------------------------------------------

    [HttpGet("surveys/responses")]
    public async Task<object> Responses(int? survey, string? band, bool comments = false, string? source = null, string? account = null, DateOnly? from = null, DateOnly? to = null, int page = 1)
    {
        var q = db.SurveyResponses.Include(r => r.Survey).ThenInclude(s => s!.Questions).Include(r => r.Customer).AsQueryable();
        if (survey != null) q = q.Where(r => r.SurveyId == survey);
        if (!string.IsNullOrEmpty(source)) q = q.Where(r => r.Source == source);
        if (!string.IsNullOrWhiteSpace(account)) q = q.Where(r => r.Customer!.Account == account.Trim());
        if (from is { } f) q = q.Where(r => r.CreatedAt >= f.ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        if (to is { } t) q = q.Where(r => r.CreatedAt < t.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime());
        var rows = (await q.OrderByDescending(r => r.CreatedAt).ToListAsync())
            .Select(r => (r, score: Surveys.Score(r, r.Survey!.Questions), comment: Surveys.Comment(r, r.Survey!.Questions))).ToList();
        // Score bands and comments live inside the answers, so they filter here
        if (band != null) rows = rows.Where(x => x.score is { } n && (band == "promoter" ? n >= 9 : band == "passive" ? n is >= 7 and <= 8 : n <= 6)).ToList();
        if (comments) rows = rows.Where(x => x.comment != null).ToList();
        return new
        {
            total = rows.Count, page, sources = Rodeo.ResponseSources,
            surveys = await db.Surveys.OrderBy(s => s.Title).Select(s => new { s.Id, s.Title }).ToListAsync(),
            rows = rows.Skip((Math.Max(page, 1) - 1) * 25).Take(25).Select(x => new
            {
                x.r.Id, x.r.CreatedAt, survey = x.r.Survey!.Title, x.score, x.comment, source = Rodeo.ResponseSources.GetValueOrDefault(x.r.Source ?? "", "—"),
                customer = x.r.Customer == null ? null : new { x.r.Customer.Id, x.r.Customer.Account, x.r.Customer.Name },
            }),
        };
    }

    [HttpGet("surveys/responses/{id:int}")]
    public async Task<IActionResult> Response(int id)
    {
        var r = await db.SurveyResponses.Include(x => x.Survey).ThenInclude(s => s!.Questions).ThenInclude(q => q.Category).Include(x => x.Customer).ThenInclude(c => c!.Plan).FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();
        var questions = r.Survey!.Questions.OrderBy(q => q.Position).ToList();
        var answers = Surveys.Answers(r);
        return Ok(new
        {
            response = new { r.Id, r.CreatedAt, source = Rodeo.ResponseSources.GetValueOrDefault(r.Source ?? "", "unknown"), score = Surveys.Score(r, questions) },
            survey = new { r.Survey.Id, r.Survey.Title },
            customer = r.Customer == null ? null : new { r.Customer.Id, r.Customer.Account, r.Customer.Name, r.Customer.Status, r.Customer.Type, plan = r.Customer.Plan?.Name },
            answered = questions.Count(q => answers.ContainsKey(q.Id)), questions = questions.Count,
            groups = questions.GroupBy(q => q.SurveyCategoryId ?? 0).Select(g => new
            {
                category = g.First().Category is { } c ? new { c.Name, c.Color } : null,
                questions = g.Select(q => new
                {
                    q.Question, q.Type, typeLabel = Rodeo.QuestionTypes.GetValueOrDefault(q.Type, q.Type), q.Required,
                    answer = answers.TryGetValue(q.Id, out var a) ? (a.ValueKind == JsonValueKind.Array ? (object)Surveys.Values(a) : a.ToString()) : null,
                    position = answers.TryGetValue(q.Id, out var b) && q.Type == "scale" ? q.Choices().IndexOf(b.ToString()) + 1 : 0, of = q.Type == "scale" ? q.Choices().Count : 0,
                }),
            }),
            prev = await db.SurveyResponses.Where(x => x.Id < id).MaxAsync(x => (int?)x.Id), next = await db.SurveyResponses.Where(x => x.Id > id).MinAsync(x => (int?)x.Id),
        });
    }

    // ---- Question bank ------------------------------------------------------------------------------------------

    [HttpGet("surveys/bank")]
    public async Task<object> Bank()
    {
        var uses = await db.SurveyQuestions.Where(q => q.SurveyBankQuestionId != null).GroupBy(q => q.SurveyBankQuestionId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var bank = await db.SurveyBankQuestions.Include(b => b.AnswerSet).OrderBy(b => b.Question).ToListAsync();
        object Row(SurveyBankQuestion b) => new
        {
            b.Id, b.Question, b.Type, typeLabel = Rodeo.QuestionTypes.GetValueOrDefault(b.Type, b.Type), b.Help, uses = uses.GetValueOrDefault(b.Id),
            set = b.AnswerSet == null ? null : new { b.AnswerSet.Name, options = Opts(b.AnswerSet.Options) },
        };
        return new
        {
            categories = (await db.SurveyCategories.OrderBy(c => c.Position).ThenBy(c => c.Name).ToListAsync())
                .Select(c => new { c.Id, c.Name, c.Description, c.Color, items = bank.Where(b => b.SurveyCategoryId == c.Id).Select(Row) }),
            uncategorized = bank.Where(b => b.SurveyCategoryId == null).Select(Row),
        };
    }

    [HttpGet("surveys/bank/form")]
    public async Task<IActionResult> BankForm(int? id, int? category)
    {
        var b = id == null ? new SurveyBankQuestion { Type = "scale", SurveyCategoryId = category } : await db.SurveyBankQuestions.FindAsync(id);
        if (b == null) return NotFound();
        return Ok(new
        {
            question = new { id, b.Question, b.Type, b.SurveyCategoryId, b.SurveyAnswerSetId, b.Help }, types = Rodeo.QuestionTypes,
            sets = await db.SurveyAnswerSets.OrderBy(s => s.Group).ThenBy(s => s.Name).Select(s => new { s.Id, s.Name, s.Group }).ToListAsync(),
            categories = await db.SurveyCategories.OrderBy(c => c.Position).ThenBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(),
        });
    }

    public record BankInput(int? SurveyCategoryId, string? Question, string? Type, int? SurveyAnswerSetId, string? Help);

    [HttpPost("surveys/bank")]
    public Task<IActionResult> AddBank(BankInput i) => SaveBank(null, i);

    [HttpPut("surveys/bank/{id:int}")]
    public Task<IActionResult> UpdateBank(int id, BankInput i) => SaveBank(id, i);

    private async Task<IActionResult> SaveBank(int? id, BankInput i)
    {
        var b = id == null ? new SurveyBankQuestion() : await db.SurveyBankQuestions.FindAsync(id);
        if (b == null) return NotFound();
        if (string.IsNullOrWhiteSpace(i.Question) || i.Question.Length > 250) return BadRequest(new { error = "Write the question (up to 250 characters)." });
        if (i.Type == null || !Rodeo.QuestionTypes.ContainsKey(i.Type)) return BadRequest(new { error = "Pick an answer type." });
        var listed = Rodeo.Listed.Contains(i.Type);
        if (listed && (i.SurveyAnswerSetId == null || !await db.SurveyAnswerSets.AnyAsync(s => s.Id == i.SurveyAnswerSetId))) return BadRequest(new { error = "Scale, Pick one and Pick any questions need an answer set." });
        (b.SurveyCategoryId, b.Question, b.Type, b.SurveyAnswerSetId, b.Help) = (i.SurveyCategoryId, i.Question.Trim(), i.Type, listed ? i.SurveyAnswerSetId : null, string.IsNullOrWhiteSpace(i.Help) ? null : i.Help.Trim());
        if (id == null) db.SurveyBankQuestions.Add(b);
        await db.SaveChangesAsync();
        return Ok(new { message = "Question saved to the bank" });
    }

    [HttpDelete("surveys/bank/{id:int}")]
    public async Task<IActionResult> DeleteBank(int id)
    {
        if (await db.SurveyBankQuestions.FindAsync(id) is not { } b) return NotFound();
        await db.SurveyQuestions.Where(q => q.SurveyBankQuestionId == id).ExecuteUpdateAsync(s => s.SetProperty(q => q.SurveyBankQuestionId, (int?)null)); // surveys keep their own copy
        db.SurveyBankQuestions.Remove(b);
        await db.SaveChangesAsync();
        return Ok(new { message = "Question removed from the bank" });
    }

    // ---- Answer sets --------------------------------------------------------------------------------------------

    [HttpGet("surveys/answer-sets")]
    public async Task<object> AnswerSets()
    {
        var questions = await db.SurveyQuestions.Where(q => q.SurveyAnswerSetId != null).GroupBy(q => q.SurveyAnswerSetId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var bank = await db.SurveyBankQuestions.Where(q => q.SurveyAnswerSetId != null).GroupBy(q => q.SurveyAnswerSetId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        return (await db.SurveyAnswerSets.OrderBy(s => s.Name).ToListAsync()).GroupBy(s => s.Group)
            .OrderBy(g => Array.IndexOf(Rodeo.AnswerSetGroups, g.Key) is var i && i < 0 ? 99 : i)
            .Select(g => new
            {
                group = g.Key,
                sets = g.Select(s => new { s.Id, s.Name, s.Type, typeLabel = Rodeo.AnswerSetTypes.GetValueOrDefault(s.Type, s.Type), options = Opts(s.Options), questions = questions.GetValueOrDefault(s.Id), bank = bank.GetValueOrDefault(s.Id) }),
            });
    }

    [HttpGet("surveys/answer-sets/form")]
    public async Task<IActionResult> AnswerSetForm(int? id)
    {
        var s = id == null ? new SurveyAnswerSet { Type = "scale", Group = "" } : await db.SurveyAnswerSets.FindAsync(id);
        if (s == null) return NotFound();
        return Ok(new { set = new { id, s.Name, s.Group, s.Type, options = id == null ? "" : string.Join("\n", Opts(s.Options)) }, groups = Rodeo.AnswerSetGroups, types = Rodeo.AnswerSetTypes });
    }

    public record AnswerSetInput(string? Name, string? Group, string? Type, string? Options);

    [HttpPost("surveys/answer-sets")]
    public Task<IActionResult> AddAnswerSet(AnswerSetInput i) => SaveAnswerSet(null, i);

    [HttpPut("surveys/answer-sets/{id:int}")]
    public Task<IActionResult> UpdateAnswerSet(int id, AnswerSetInput i) => SaveAnswerSet(id, i);

    private async Task<IActionResult> SaveAnswerSet(int? id, AnswerSetInput i)
    {
        var s = id == null ? new SurveyAnswerSet() : await db.SurveyAnswerSets.FindAsync(id);
        if (s == null) return NotFound();
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 80) return BadRequest(new { error = "Give the answer set a name (up to 80 characters)." });
        if (await db.SurveyAnswerSets.AnyAsync(x => x.Name == i.Name.Trim() && x.Id != s.Id)) return BadRequest(new { error = "Another answer set has that name." });
        if (string.IsNullOrWhiteSpace(i.Group) || i.Group.Length > 40) return BadRequest(new { error = "Say what kind it is." });
        if (i.Type == null || !Rodeo.AnswerSetTypes.ContainsKey(i.Type)) return BadRequest(new { error = "Pick a type." });
        var options = (i.Options ?? "").Split('\n').Select(o => o.Trim()).Where(o => o != "").Distinct().ToList();
        if (options.Count < 2) return BadRequest(new { error = "An answer set needs at least two answers." });
        (s.Name, s.Group, s.Type, s.Options) = (i.Name.Trim(), i.Group.Trim(), i.Type, JsonSerializer.Serialize(options));
        if (id == null) db.SurveyAnswerSets.Add(s);
        await db.SaveChangesAsync();
        // Questions using the set show the new wording; past answers keep theirs
        await db.SurveyQuestions.Where(q => q.SurveyAnswerSetId == s.Id).ExecuteUpdateAsync(x => x.SetProperty(q => q.Options, s.Options));
        return Ok(new { message = "Answer set saved" });
    }

    [HttpDelete("surveys/answer-sets/{id:int}")]
    public async Task<IActionResult> DeleteAnswerSet(int id)
    {
        if (await db.SurveyAnswerSets.FindAsync(id) is not { } s) return NotFound();
        if (await db.SurveyQuestions.AnyAsync(q => q.SurveyAnswerSetId == id) || await db.SurveyBankQuestions.AnyAsync(q => q.SurveyAnswerSetId == id))
            return UnprocessableEntity(new { error = "This answer set is in use." });
        db.SurveyAnswerSets.Remove(s);
        await db.SaveChangesAsync();
        return Ok(new { message = "Answer set deleted" });
    }

    // ---- Categories ---------------------------------------------------------------------------------------------

    [HttpGet("surveys/categories")]
    public async Task<object> Categories()
    {
        var bank = await db.SurveyBankQuestions.Where(q => q.SurveyCategoryId != null).GroupBy(q => q.SurveyCategoryId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        var used = await db.SurveyQuestions.Where(q => q.SurveyCategoryId != null).GroupBy(q => q.SurveyCategoryId!.Value).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        return (await db.SurveyCategories.OrderBy(c => c.Position).ThenBy(c => c.Name).ToListAsync())
            .Select(c => new { c.Id, c.Name, c.Description, c.Color, c.Position, bank = bank.GetValueOrDefault(c.Id), questions = used.GetValueOrDefault(c.Id) });
    }

    [HttpGet("surveys/categories/form")]
    public async Task<IActionResult> CategoryForm(int? id)
    {
        var c = id == null ? new SurveyCategory() : await db.SurveyCategories.FindAsync(id);
        return c == null ? NotFound() : Ok(new { category = new { id, c.Name, c.Description, c.Color, position = id == null ? (int?)null : c.Position } });
    }

    public record CategoryInput(string? Name, string? Description, string? Color, int? Position);

    [HttpPost("surveys/categories")]
    public Task<IActionResult> AddCategory(CategoryInput i) => SaveCategory(null, i);

    [HttpPut("surveys/categories/{id:int}")]
    public Task<IActionResult> UpdateCategory(int id, CategoryInput i) => SaveCategory(id, i);

    private async Task<IActionResult> SaveCategory(int? id, CategoryInput i)
    {
        var c = id == null ? new SurveyCategory() : await db.SurveyCategories.FindAsync(id);
        if (c == null) return NotFound();
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 60) return BadRequest(new { error = "Give the category a name (up to 60 characters)." });
        if (await db.SurveyCategories.AnyAsync(x => x.Name == i.Name.Trim() && x.Id != c.Id)) return BadRequest(new { error = "Another category has that name." });
        if (i.Color == null || !Regex.IsMatch(i.Color, "^#[0-9a-fA-F]{6}$")) return BadRequest(new { error = "Pick a color." });
        if (i.Position is < 0 or > 999) return BadRequest(new { error = "The order is 0 to 999." });
        (c.Name, c.Description, c.Color) = (i.Name.Trim(), string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(), i.Color);
        c.Position = i.Position ?? (await db.SurveyCategories.MaxAsync(x => (int?)x.Position) ?? 0) + 1;
        if (id == null) db.SurveyCategories.Add(c);
        await db.SaveChangesAsync();
        return Ok(new { message = "Category saved" });
    }

    [HttpDelete("surveys/categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        if (await db.SurveyCategories.FindAsync(id) is not { } c) return NotFound();
        await db.SurveyQuestions.Where(q => q.SurveyCategoryId == id).ExecuteUpdateAsync(s => s.SetProperty(q => q.SurveyCategoryId, (int?)null));
        await db.SurveyBankQuestions.Where(q => q.SurveyCategoryId == id).ExecuteUpdateAsync(s => s.SetProperty(q => q.SurveyCategoryId, (int?)null));
        db.SurveyCategories.Remove(c);
        await db.SaveChangesAsync();
        return Ok(new { message = "Category deleted; its questions are now uncategorized" });
    }

    // ---- Survey emails ------------------------------------------------------------------------------------------

    [HttpGet("survey-emails")]
    public async Task<object> SurveyEmails(int page = 1)
    {
        var names = Services.SurveyEmails.Rules.Values.Select(r => r.Template).ToList();
        var stats = new List<object>();
        var since = DateTime.UtcNow.AddDays(-30);
        foreach (var (key, rule) in Services.SurveyEmails.Rules)
        {
            var sent = db.ContactLogs.Where(l => l.Template == rule.Template);
            var survey = await db.Surveys.FirstOrDefaultAsync(s => s.Slug == rule.Slug);
            var total = await sent.CountAsync();
            var answered = survey == null ? 0 : await db.SurveyResponses.Where(r => r.SurveyId == survey.Id && sent.Any(l => l.CustomerId == r.CustomerId)).Select(r => r.CustomerId).Distinct().CountAsync();
            var template = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Name == rule.Template);
            stats.Add(new
            {
                key, rule.Name, templateName = rule.Template, rule.Slug, rule.Who, sent30 = await sent.CountAsync(l => l.CreatedAt >= since), total, due = await Services.SurveyEmails.Due(db, key).CountAsync(),
                answered, rate = total == 0 ? (int?)null : (int)Math.Round(100.0 * answered / total),
                survey = survey == null ? null : new { survey.Id, survey.Title }, template = template == null ? null : new { template.Id, template.Name, template.Subject },
            });
        }
        var q = db.ContactLogs.Include(l => l.Customer).Where(l => names.Contains(l.Template));
        var rows = await q.OrderByDescending(l => l.SentAt).ThenByDescending(l => l.Id).Skip((Math.Max(page, 1) - 1) * 10).Take(10).ToListAsync();
        // Which of these customers answered the survey the email was for
        var slugs = Services.SurveyEmails.Rules.Values.ToDictionary(r => r.Template, r => r.Slug);
        var ids = rows.Select(l => l.CustomerId).ToList();
        var answers = await db.SurveyResponses.Where(r => ids.Contains(r.CustomerId)).Select(r => new { r.CustomerId, r.Survey!.Slug }).ToListAsync();
        return new
        {
            stats, total = await q.CountAsync(), page,
            sent = rows.Select(l => new
            {
                l.Id, at = l.SentAt ?? l.CreatedAt, l.Status, l.OpenedAt, email = l.Template == "Survey - Farewell" ? "Farewell Survey" : "90-Day Check-In",
                answered = answers.Any(a => a.CustomerId == l.CustomerId && a.Slug == slugs[l.Template]),
                customer = l.Customer == null ? null : new { l.Customer.Id, l.Customer.Account, l.Customer.Name, l.Customer.Email, l.Customer.Status },
            }),
        };
    }

    [HttpPost("survey-emails/send")]
    public async Task<object> SendSurveyEmails()
    {
        var me = (await Permissions.CurrentAsync(HttpContext))!;
        var job = Jobs.Scheduler.All.First(j => j.Name == "Survey emails");
        var run = await Jobs.Scheduler.RunAsync(HttpContext.RequestServices, job, me.Name);
        return new { message = run.Message };
    }
}
