using System.Globalization;
using System.Text.RegularExpressions;
using RcBunny.Data.Entities;

namespace RcBunny.Data;

/// <summary>Fills {{placeholders}} in email, SMS and PDF templates for one customer (ebunny's template variables).</summary>
public static partial class TemplateText
{
    [GeneratedRegex(@"\{\{\s*([a-z_]+)(?::([a-z0-9-]+))?(?:\|([^}]*))?\s*\}\}")]
    private static partial Regex Placeholder();

    /// <param name="surveyLink">Builds the signed link for {{survey:slug}} (null = leave a plain link).</param>
    public static string Render(string text, Customer? c, string? planName = null, Func<string, string>? surveyLink = null, IDictionary<string, string>? extra = null)
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        return Placeholder().Replace(text ?? "", m =>
        {
            var key = m.Groups[1].Value;
            var fallback = m.Groups[3].Success ? m.Groups[3].Value : "";
            if (extra != null && extra.TryGetValue(key, out var x)) return x;
            string? v = key switch
            {
                "first_name" => c?.FirstName ?? c?.Name.Split(' ')[0],
                "last_name" => c?.LastName,
                "name" => c?.Name,
                "account" => c?.Account,
                "email" => c?.Email,
                "phone" => c?.Phone,
                "address" => c is null ? null : $"{c.Address}, {c.City} {c.Zip}",
                "plan" => planName,
                "balance" => c?.Balance.ToString("C", us),
                "due_date" => c?.DueDate?.ToString("MMMM d, yyyy", us),
                "start_date" => (c?.ActualStart ?? c?.StartDate)?.ToString("MMMM d, yyyy", us),
                "deposit_due" => c?.DepositDue.ToString("C", us),
                "stars" => c?.Stars.ToString("N0", us),
                "referral_code" => c?.ReferralCode,
                "survey" => m.Groups[2].Success ? (surveyLink?.Invoke(m.Groups[2].Value) ?? "/survey/" + m.Groups[2].Value) : null,
                "today" => DateTime.Today.ToString("MMMM d, yyyy", us),
                _ => null,
            };
            return string.IsNullOrEmpty(v) ? fallback : v;
        });
    }
}
