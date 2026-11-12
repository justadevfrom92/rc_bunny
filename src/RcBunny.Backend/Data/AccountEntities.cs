using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RcBunny.Data;

// ---- Account: your notifications (ebunny's admin_notifications table) ---------------------------------------------

/// <summary>
/// Something an admin user should know about: a report or export finished, a new support ticket, a customer writing back
/// on a ticket, a new website email, a file added, a campaign sent. It adds to the number on their avatar until they open it.
/// Each person picks the kinds they want in Settings.
/// </summary>
public class AdminNotification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    /// <summary>One of Kinds' keys.</summary>
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    /// <summary>The admin page it's about, as a path (/admin/ranger/tickets/12).</summary>
    public string? Url { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>kind → (label in Settings, who gets it, icon).</summary>
    public static readonly Dictionary<string, (string Label, string When, string Icon)> Kinds = new()
    {
        ["ticket_new"] = ("New support tickets", "Ranger team: a customer opened a ticket", "🎫"),
        ["ticket_reply"] = ("Support ticket chats", "Your tickets: the customer wrote back (My Account or email)", "💬"),
        ["email"] = ("Emails", "New Web Messages from the website contact form", "✉"),
        ["report"] = ("Reports done", "Your Walker reports finished running", "📊"),
        ["file"] = ("Files finished", "Your exports are ready, and new files in Walker folders", "📁"),
        ["campaign"] = ("Campaigns finished", "Rodeo campaigns finished sending", "📣"),
        ["health"] = ("System health", "Sheriff: a heartbeat check goes bad, and when it's healthy again", "❤"),
    };

    public string Icon => Kinds.TryGetValue(Kind, out var k) ? k.Icon : "🔔";

    /// <summary>Is this kind on for this person (on unless they turned it off in Settings)?</summary>
    public static bool Wants(User u, string kind) =>
        u.NotificationPrefs == null || JsonSerializer.Deserialize<Dictionary<string, bool>>(u.NotificationPrefs)!.GetValueOrDefault(kind, true);

    /// <summary>Send to each person who wants this kind (added to db; the caller saves).</summary>
    public static int Notify(AppDbContext db, IEnumerable<User?> users, string kind, string title, string? body = null, string? url = null)
    {
        var n = 0;
        foreach (var u in users)
        {
            if (u is not { Active: true } || !Wants(u, kind)) continue;
            db.AdminNotifications.Add(new AdminNotification
            {
                UserId = u.Id, Kind = kind, Title = title.Length > 200 ? title[..200] : title,
                Body = body is { Length: > 500 } ? body[..500] : body, Url = url,
            });
            n++;
        }
        return n;
    }

    /// <summary>Everyone whose role (or own rights) opens an app.</summary>
    public static async Task<List<User>> AppUsersAsync(AppDbContext db, string permission) =>
        (await db.Users.Include(u => u.Role).Where(u => u.Active).ToListAsync()).Where(u => u.PermissionList().Contains(permission)).ToList();
}
