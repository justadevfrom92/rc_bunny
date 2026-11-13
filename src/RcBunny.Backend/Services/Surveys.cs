using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RcBunny.Data;

namespace RcBunny.Services;

/// <summary>Survey answers and scores (ebunny's SurveyStats).</summary>
public static class Surveys
{
    /// <summary>A response's answers: question id → a string, or a list for Pick any.</summary>
    public static Dictionary<int, JsonElement> Answers(SurveyResponse r) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(r.Answers)!
            .Where(kv => int.TryParse(kv.Key, out _) && !(kv.Value.ValueKind == JsonValueKind.String && kv.Value.GetString() == "") && !(kv.Value.ValueKind == JsonValueKind.Array && kv.Value.GetArrayLength() == 0))
            .ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);

    /// <summary>One answer as a list of strings (empty when not answered).</summary>
    public static List<string> Values(JsonElement? a) => a is not { } v ? [] : v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.ToString()).ToList() : [v.ToString()];

    /// <summary>NPS from 0–10 answers: % promoters (9–10) minus % detractors (0–6).</summary>
    public static int? Nps(IReadOnlyCollection<int> scores) =>
        scores.Count == 0 ? null : (int)Math.Round(100.0 * (scores.Count(v => v >= 9) - scores.Count(v => v <= 6)) / scores.Count);

    /// <summary>The response's 0–10 recommend score, if the survey asks one.</summary>
    public static int? Score(SurveyResponse r, IEnumerable<SurveyQuestion> questions)
    {
        var q = questions.OrderBy(x => x.Position).FirstOrDefault(x => x.Type == "rating");
        return q != null && Answers(r).TryGetValue(q.Id, out var a) && int.TryParse(a.ToString(), out var n) ? n : null;
    }

    public static int? SurveyNps(Survey s, IEnumerable<SurveyResponse> responses) =>
        Nps(responses.Select(r => Score(r, s.Questions)).Where(n => n != null).Select(n => n!.Value).ToList());

    public record Summary(int Id, string Title, string State, string StateLabel, int Recent, int Responses, int? Score, DateTime? LastAt);

    /// <summary>Each survey's responses (last 30 days and all), its score and its last response.</summary>
    public static async Task<List<Summary>> SummariesAsync(AppDbContext db)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var surveys = await db.Surveys.Include(s => s.Questions).ToListAsync();
        var responses = (await db.SurveyResponses.ToListAsync()).ToLookup(r => r.SurveyId);
        return surveys.Select(s => new Summary(s.Id, s.Title, s.State, s.StateLabel, responses[s.Id].Count(r => r.CreatedAt >= since), responses[s.Id].Count(),
            SurveyNps(s, responses[s.Id]), responses[s.Id].Select(r => (DateTime?)r.CreatedAt).Max())).ToList();
    }

    /// <summary>The latest responses: survey, customer and score.</summary>
    public static async Task<List<object>> LatestAsync(AppDbContext db, int take)
    {
        var latest = await db.SurveyResponses.Include(r => r.Survey).ThenInclude(s => s!.Questions).Include(r => r.Customer)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(take).ToListAsync();
        return latest.Select(r => (object)new
        {
            r.Id, r.CreatedAt, survey = r.Survey?.Title, customer = r.Customer == null ? null : new { r.Customer.Id, r.Customer.Account, r.Customer.Name },
            score = r.Survey == null ? null : Score(r, r.Survey.Questions),
        }).ToList();
    }

    /// <summary>[{label, count, pct}] for each choice (and any other answers given), pct of the people who answered.</summary>
    private static List<object> Rows(List<string> choices, List<string> answers, int basis)
    {
        var counts = answers.GroupBy(a => a).ToDictionary(g => g.Key, g => g.Count());
        return choices.Concat(counts.Keys).Distinct()
            .Select(l => (object)new { label = l, count = counts.GetValueOrDefault(l), pct = basis == 0 ? 0 : (int)Math.Round(100.0 * counts.GetValueOrDefault(l) / basis) }).ToList();
    }

    /// <summary>One question's results (ebunny's SurveyStats::question).</summary>
    public static object Question(SurveyQuestion q, List<SurveyResponse> responses)
    {
        var answered = responses.Select(r => (r, a: Answers(r).TryGetValue(q.Id, out var v) ? v : (JsonElement?)null)).Where(x => x.a != null).ToList();
        var n = answered.Count;
        var basics = new { q.Id, q.Question, q.Type, typeLabel = Rodeo.QuestionTypes.GetValueOrDefault(q.Type, q.Type), count = n, skipped = responses.Count - n };
        switch (q.Type)
        {
            case "rating":
                var nums = answered.Select(x => int.TryParse(x.a!.Value.ToString(), out var i) ? i : 0).ToList();
                return new
                {
                    basics, avg = n > 0 ? Math.Round(nums.Average(), 1) : (double?)null, nps = Nps(nums),
                    bands = new[] { new { label = "Detractors (0–6)", n = nums.Count(v => v <= 6) }, new { label = "Passives (7–8)", n = nums.Count(v => v is >= 7 and <= 8) }, new { label = "Promoters (9–10)", n = nums.Count(v => v >= 9) } },
                    rows = Rows(q.Choices(), nums.Select(v => v.ToString()).ToList(), n),
                };
            case "scale":
                var choices = q.Choices();
                var scores = answered.Select(x => choices.IndexOf(x.a!.Value.ToString())).Where(i => i >= 0).Select(i => i + 1).ToList();
                return new
                {
                    basics, avg = scores.Count > 0 ? Math.Round(scores.Average(), 2) : (double?)null, of = choices.Count,
                    top = scores.Count > 0 ? (int)Math.Round(100.0 * scores.Count(x => x >= choices.Count - 1) / scores.Count) : (int?)null,
                    rows = Rows(choices, answered.Select(x => x.a!.Value.ToString()).ToList(), n),
                };
            case "multi":
                // % of people who answered, so it can add up past 100
                return new { basics, rows = Rows(q.Choices(), answered.SelectMany(x => Values(x.a)).ToList(), n) };
            case "text":
                return new
                {
                    basics, total = n,
                    texts = answered.OrderByDescending(x => x.r.CreatedAt).Take(4).Select(x => new { x.r.Id, text = x.a!.Value.ToString(), name = x.r.Customer?.Name, at = x.r.CreatedAt }),
                };
            default: // single, yes_no
                return new { basics, rows = Rows(q.Choices(), answered.Select(x => x.a!.Value.ToString()).ToList(), n) };
        }
    }

    /// <summary>The first written answer in a response.</summary>
    public static string? Comment(SurveyResponse r, IEnumerable<SurveyQuestion> questions)
    {
        var answers = Answers(r);
        foreach (var q in questions.Where(q => q.Type == "text").OrderBy(q => q.Position))
            if (answers.TryGetValue(q.Id, out var a) && a.ToString() is { Length: > 0 } t) return t;
        return null;
    }
}
