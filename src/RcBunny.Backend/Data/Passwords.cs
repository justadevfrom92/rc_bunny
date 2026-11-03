using Microsoft.AspNetCore.Identity;

namespace RcBunny.Data;

/// <summary>PBKDF2 password hashes (ASP.NET Core Identity's format), for employees and customers.</summary>
public static class Passwords
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Subject = new();

    public static string Hash(string password) => Hasher.HashPassword(Subject, password);

    public static bool Verify(string? hash, string password)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        try
        {
            return Hasher.VerifyHashedPassword(Subject, hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
