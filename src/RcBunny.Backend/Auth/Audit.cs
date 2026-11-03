using RcBunny.Data;

namespace RcBunny.Auth;

/// <summary>Adds a line to Sheriff → Timeline. Saved with the caller's next SaveChanges.</summary>
public static class Audit
{
    public static void Add(AppDbContext db, string who, string category, string what) =>
        db.AuditEntries.Add(new AuditEntry { Who = who, Category = category, What = what });
}
