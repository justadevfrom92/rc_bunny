using Microsoft.AspNetCore.Authentication.Cookies;
using RcBunny.Data;
using RcBunny.Seed;

// Show money and dates the US way, whatever language the server is set to.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo("en-US");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDatabase(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddSingleton<RcBunny.Services.Mailer>(); // email (SMTP, or storage/mail-log until it is set up)
builder.Services.AddHostedService<RcBunny.Jobs.Scheduler>(); // Sheriff → Crons

// Employees sign in with a cookie. The API answers 401/403 instead of redirecting.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "rcb.admin";
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.ExpireTimeSpan = TimeSpan.FromHours(10);
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });

var app = builder.Build();
var storage = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, app.Configuration["Storage:Root"] ?? "../../storage"));

// Database commands: dotnet run -- migrate | migrate:fresh | db:show (see Seed/Commands.cs)
if (args.FirstOrDefault(a => Commands.Names.Contains(a)) is { } command)
{
    using var scope = app.Services.CreateScope();
    return await Commands.RunAsync(command, scope.ServiceProvider.GetRequiredService<AppDbContext>(), storage);
}

// Starting normally creates the database and sample data the first time
using (var scope = app.Services.CreateScope())
    await Seeder.RunAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), storage);

// The admin's pages are under /admin like ebunny's; its logos and tokens.css are at /shared like ebunny's
var admin = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "admin");
// Only /admin and /shared are served (anything else left in wwwroot by an older build is never a page)
if (Directory.Exists(admin))
{
    app.UseStaticFiles(new StaticFileOptions { RequestPath = "/admin", FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(admin) });
    if (Directory.Exists(Path.Combine(admin, "shared")))
        app.UseStaticFiles(new StaticFileOptions { RequestPath = "/shared", FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(admin, "shared")) });
}
app.UseAuthentication();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/admin"));
app.MapFallbackToFile("/admin/{**path:nonfile}", "admin/index.html"); // the React admin handles its own pages

app.Run();
return 0;
