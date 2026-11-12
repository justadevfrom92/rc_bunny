using Microsoft.EntityFrameworkCore;
using RcBunny.Data;
using RcBunny.Walker;

namespace RcBunny.Seed;

public static partial class Seeder
{
    /// <summary>Sample notifications (ebunny's AdminNotificationSeeder) so every admin user's avatar number, menu and Notifications page have something in them.</summary>
    private static async Task SeedNotificationsAsync(AppDbContext db)
    {
        db.AdminNotifications.RemoveRange(db.AdminNotifications); // the report runs above made their own; these replace them
        await db.SaveChangesAsync();
        var random = new Random(77);
        var now = DateTime.UtcNow;
        var agents = await db.RangerAgents.Where(a => a.Active).Select(a => a.UserId).ToListAsync();
        foreach (var u in await db.Users.Include(x => x.Role).Where(x => x.Active).ToListAsync())
        {
            var perms = u.PermissionList();
            var items = new List<(string Kind, string Title, string? Body, string Url, DateTime? At)>();
            if (agents.Contains(u.Id))
            {
                foreach (var t in await db.SupportTickets.OrderByDescending(t => t.Id).Take(6).ToListAsync())
                    items.Add(("ticket_new", $"New ticket {t.Number}: {t.Subject}", $"{t.Name} · {Ranger.Topics.GetValueOrDefault(t.Topic)}", $"/admin/ranger/tickets/{t.Id}", t.CreatedAt));
                foreach (var t in await db.SupportTickets.Where(t => Ranger.Active.Contains(t.Status) && t.AssignedToId == u.Id).Take(3).ToListAsync())
                    items.Add(("ticket_reply", $"{t.Name} wrote back on {t.Number}", "Thanks, one more question about this…", $"/admin/ranger/tickets/{t.Id}", t.UpdatedAt));
            }
            if (perms.Contains("corral"))
                foreach (var m in await db.ContactMessages.OrderByDescending(m => m.Id).Take(3).ToListAsync())
                    items.Add(("email", $"New email from {m.Name}", $"{m.Topic}: {(m.Message.Length > 120 ? m.Message[..120] : m.Message)}", $"/admin/ranger/messages/{m.Id}", m.CreatedAt));
            foreach (var r in await db.ReportRuns.Where(r => r.UserId == u.Id && r.Status == "done").OrderByDescending(r => r.FinishedAt).Take(3).ToListAsync())
                items.Add((r.App == "walker" ? "report" : "file", $"{(r.App == "walker" ? "Report ready: " : "Export ready: ")}{r.Label}", $"{r.Rows:N0} rows · {Reports.FormatLabel(r.Format)}", $"/admin/files/{r.Id}", r.FinishedAt));
            if (perms.Contains("walker"))
                foreach (var up in await db.ReportUploads.OrderByDescending(x => x.Id).Take(2).ToListAsync())
                    items.Add(("file", $"New file in {up.Category}: {up.Title}", $"Added by {(await db.Users.FindAsync(up.UserId))?.Name ?? "someone"}", $"/admin/walker/uploads/{up.Id}", up.CreatedAt));
            if (perms.Contains("rodeo"))
                foreach (var c in await db.Campaigns.Where(c => c.Status == "sent").OrderByDescending(c => c.SentAt).Take(2).ToListAsync())
                    items.Add(("campaign", $"Campaign sent: {c.Name}", $"{c.Recipients:N0} sent, {c.Skipped:N0} skipped", $"/admin/rodeo/campaigns/{c.Id}", c.SentAt));
            for (var i = 0; i < items.Count; i++)
            {
                var (kind, title, body, url, when) = items[i];
                var at = when ?? now.AddHours(-(i + 1));
                db.AdminNotifications.Add(new AdminNotification
                {
                    UserId = u.Id, Kind = kind, Title = title.Length > 200 ? title[..200] : title, Body = body, Url = url, CreatedAt = at,
                    ReadAt = at < now.AddDays(-2) || random.Next(3) == 0 ? at.AddHours(1) : null,
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
