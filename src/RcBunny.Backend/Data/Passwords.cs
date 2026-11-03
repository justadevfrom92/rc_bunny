using System.Security.Cryptography;

namespace RcBunny.Data;

/// <summary>PBKDF2 password hashes stored as "iterations.salt.hash".</summary>
public static class Passwords
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"100000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string? stored, string password)
    {
        var parts = stored?.Split('.');
        if (parts is not { Length: 3 } || !int.TryParse(parts[0], out var iterations)) return false;
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[1]), iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(parts[2]));
    }
}
