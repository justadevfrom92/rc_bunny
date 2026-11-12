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
}
