using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RcBunny.Auth;
using RcBunny.Data;
using RcBunny.Walker;

namespace RcBunny.Controllers;

/// <summary>
/// Your Account area (the user menu, top right), like ebunny's: Notifications (the number on your avatar), My Files (every
/// report you ran, from any app, with its file) and Settings (which notifications you get, and switching test users).
/// </summary>
[ApiController]
[Route("api")]
[RequirePermission]
public class AccountController(AppDbContext db, IServiceScopeFactory scopes, IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    private string Storage => Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:Root"] ?? "../../storage"));
    private async Task<Permissions.SignedIn> Me() => (await Permissions.CurrentAsync(HttpContext))!;

    private static object Note(AdminNotification n) => new { n.Id, n.Kind, n.Title, n.Body, n.Url, n.ReadAt, n.CreatedAt, n.Icon, kindLabel = AdminNotification.Kinds.GetValueOrDefault(n.Kind).Label ?? n.Kind };

    // ---- the user menu ----------------------------------------------------------------------------------------

    /// <summary>For the user menu: unread notifications (the number on your avatar), your latest 5, and how many files you have.</summary>
    [HttpGet("account/menu")]
    public async Task<object> Menu()
    {
        var me = await Me();
        var mine = db.AdminNotifications.Where(n => n.UserId == me.Id);
        return new
        {
            unread = await mine.CountAsync(n => n.ReadAt == null),
            latest = (await mine.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(5).ToListAsync()).Select(Note),
            files = await db.ReportRuns.CountAsync(r => r.UserId == me.Id),
        };
    }

    /// <summary>For the popup and the number on your avatar: finished runs you haven't seen, newest first.</summary>
    [HttpGet("notifications")]
    public async Task<object> Notify()
    {
        var me = await Me();
        var cut = DateTime.UtcNow.AddMinutes(-ReportRun.TimeoutMinutes);
        var items = await Unseen(db.ReportRuns.Where(r => r.UserId == me.Id)).OrderByDescending(r => r.FinishedAt).Take(10).ToListAsync();
        return new
        {
            count = await db.AdminNotifications.CountAsync(n => n.UserId == me.Id && n.ReadAt == null), // every unread notification
            running = await db.ReportRuns.CountAsync(r => r.UserId == me.Id && (r.Status == "queued" || r.Status == "running") && (r.StartedAt ?? r.CreatedAt) >= cut),
            items = items.Select(r => new { r.Id, title = r.Label, ok = r.Status == "done", r.Rows, format = Reports.FormatLabel(r.Format), url = $"/files/{r.Id}",
                download = r.File != null ? $"/api/files/{r.Id}/download" : null }),
        };
    }

    // ---- Notifications ----------------------------------------------------------------------------------------

    [HttpGet("my/notifications")]
    public async Task<object> Notifications(string? kind, bool unread = false, int page = 1)
    {
        var me = await Me();
        var mine = db.AdminNotifications.Where(n => n.UserId == me.Id);
        var q = mine.Where(n => (kind == null || n.Kind == kind) && (!unread || n.ReadAt == null));
        return new
        {
            total = await q.CountAsync(), page, unread = await mine.CountAsync(n => n.ReadAt == null),
            kinds = AdminNotification.Kinds.ToDictionary(k => k.Key, k => k.Value.Label),
            items = (await q.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Skip((Math.Max(page, 1) - 1) * 25).Take(25).ToListAsync()).Select(Note),
        };
    }

    /// <summary>Open one: marks it read and says where to go.</summary>
    [HttpPost("my/notifications/{id:int}/open")]
    public async Task<IActionResult> Open(int id)
    {
        var me = await Me();
        var n = await db.AdminNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == me.Id);
        if (n == null) return NotFound();
        n.ReadAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { url = n.Url ?? "/admin/my/notifications" });
    }

    [HttpPost("my/notifications/read-all")]
    public async Task<object> ReadAll()
    {
        var me = await Me();
        await db.AdminNotifications.Where(n => n.UserId == me.Id && n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
        return new { message = "All notifications marked read" };
    }

    // ---- Settings ---------------------------------------------------------------------------------------------

    [HttpGet("my/settings")]
    public async Task<object> Settings()
    {
        var me = await Me();
        var user = await db.Users.FindAsync(me.Id);
        // Switch User is for testing only: never in production
        var others = !env.IsDevelopment() ? [] : await db.Users.Include(u => u.Role).Where(u => u.Active && u.Id != me.Id).OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Email, role = u.Role!.Name }).ToListAsync();
        return new
        {
            me.Name, me.Email,
            kinds = AdminNotification.Kinds.Select(k => new { key = k.Key, k.Value.Label, k.Value.When, k.Value.Icon, on = AdminNotification.Wants(user!, k.Key) }),
            testUsers = others,
        };
    }

    public record SettingsInput(List<string>? Kinds);

    [HttpPut("my/settings")]
    public async Task<object> SaveSettings(SettingsInput input)
    {
        var user = await db.Users.FindAsync((await Me()).Id);
        user!.NotificationPrefs = JsonSerializer.Serialize(AdminNotification.Kinds.Keys.ToDictionary(k => k, k => (input.Kinds ?? []).Contains(k)));
        await db.SaveChangesAsync();
        return new { message = "Notification settings saved" };
    }

    // ---- My Files ---------------------------------------------------------------------------------------------

    private static object Row(ReportRun r) => new
    {
        r.Id, r.Report, label = r.Label, r.App, r.CreatedAt, r.FinishedAt, r.DurationMs, r.Rows, r.Status, state = r.State, r.Error, r.Summary,
        file = r.File != null, fileName = r.File == null ? null : Path.GetFileName(r.File), size = r.SizeLabel, format = Reports.FormatLabel(r.Format),
        @params = r.Params == null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(r.Params),
    };

    private static IQueryable<ReportRun> Unseen(IQueryable<ReportRun> q) => q.Where(r => r.SeenAt == null && (r.Status == "done" || r.Status == "failed"));

    [HttpGet("files")]
    public async Task<object> Files(string? status, int page = 1)
    {
        var me = await Me();
        var mine = db.ReportRuns.Where(r => r.UserId == me.Id);
        var q = status switch
        {
            "running" => mine.Where(r => r.Status == "queued" || r.Status == "running"),
            "done" or "failed" => mine.Where(r => r.Status == status),
            _ => mine,
        };
        var unseen = await Unseen(mine).Select(r => r.Id).ToListAsync();
        var result = new
        {
            total = await q.CountAsync(), page, unseen,
            counts = new { all = await mine.CountAsync(), running = await mine.CountAsync(r => r.Status == "queued" || r.Status == "running"),
                done = await mine.CountAsync(r => r.Status == "done"), failed = await mine.CountAsync(r => r.Status == "failed") },
            runs = (await q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip((Math.Max(page, 1) - 1) * 25).Take(25).ToListAsync()).Select(Row),
        };
        // Opening the list counts as seeing them (and reading their notifications)
        await Unseen(mine).ExecuteUpdateAsync(s => s.SetProperty(r => r.SeenAt, DateTime.UtcNow));
        await db.AdminNotifications.Where(n => n.UserId == me.Id && n.ReadAt == null && (n.Kind == "report" || n.Kind == "file") && n.Url!.StartsWith("/admin/files/"))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
        return result;
    }

    /// <summary>Yours, or anyone's for people with Walker.</summary>
    private async Task<ReportRun?> Own(int id)
    {
        var me = await Me();
        var run = await db.ReportRuns.FindAsync(id);
        return run != null && (run.UserId == me.Id || me.Permissions.Contains("walker")) ? run : null;
    }

    [HttpGet("files/{id:int}")]
    public async Task<IActionResult> File(int id)
    {
        if (await Own(id) is not { } run) return NotFound();
        var me = await Me();
        if (run.Status is "done" or "failed" && run.SeenAt == null && run.UserId == me.Id) run.SeenAt = DateTime.UtcNow;
        await db.AdminNotifications.Where(n => n.UserId == me.Id && n.ReadAt == null && n.Url == $"/admin/files/{id}").ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var def = Reports.Find(run.Report);
        // The sidebar of the app the report came from, if you can open it
        return Ok(new { run = Row(run), model = def?.Model, menuApp = me.Permissions.Contains(run.App) ? run.App : null,
            preview = run.Preview == null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(run.Preview) });
    }

    [HttpGet("files/{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var run = await Own(id);
        var path = run?.File == null ? null : Path.Combine(Storage, run.File);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        run!.SeenAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return PhysicalFile(path, Reports.Formats.GetValueOrDefault(run.Format).Type ?? "application/octet-stream", Path.GetFileName(path));
    }

    /// <summary>Run it again with the same settings and format.</summary>
    [HttpPost("files/{id:int}/rerun")]
    public async Task<IActionResult> Rerun(int id)
    {
        if (await Own(id) is not { } run) return NotFound();
        if (Reports.Find(run.Report) == null) return NotFound();
        var again = new ReportRun
        {
            UserId = run.UserId, App = run.App, Report = run.Report, Title = run.Title, Model = run.Model, Params = run.Params,
            Format = run.Format, Summary = run.Summary, RerunOf = run.Id, SavedReportId = run.SavedReportId,
        };
        db.ReportRuns.Add(again);
        await db.SaveChangesAsync();
        Reports.Start(scopes, again.Id, Storage);
        return Ok(new { again.Id, message = $"{again.Label} is running again. You'll get a notice when it's ready." });
    }

    [HttpPost("files/seen")]
    public async Task<object> Seen()
    {
        var me = await Me();
        await Unseen(db.ReportRuns.Where(r => r.UserId == me.Id)).ExecuteUpdateAsync(s => s.SetProperty(r => r.SeenAt, DateTime.UtcNow));
        await db.AdminNotifications.Where(n => n.UserId == me.Id && n.ReadAt == null && (n.Kind == "report" || n.Kind == "file"))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
        return new { message = "Marked seen" };
    }
}
