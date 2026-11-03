using System.Security.Cryptography;
using System.Text;

namespace RcBunny.Data;

/// <summary>
/// Signed survey links ({{survey:slug}} in emails) so an answer is tied to the account. Both projects share the secret
/// (Survey:LinkSecret in appsettings); the Backend makes the links and the website checks them.
/// </summary>
public static class SurveyLinks
{
    public static string Secret = "rcbunny-dev-survey-secret";

    public static string Sign(string slug, int customerId)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes($"{slug}|{customerId}")))[..20].ToLowerInvariant();
    }

    public static bool Verify(string slug, int customerId, string? sig) =>
        sig != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sign(slug, customerId)), Encoding.UTF8.GetBytes(sig));

    public static string For(string slug, int customerId) => $"/survey/{slug}?c={customerId}&sig={Sign(slug, customerId)}";
}
