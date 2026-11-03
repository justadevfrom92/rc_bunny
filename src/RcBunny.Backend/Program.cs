using Microsoft.AspNetCore.Authentication.Cookies;
using RcBunny.Data;
using RcBunny.Seed;

// Show money and dates the US way, whatever language the server is set to.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo("en-US");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDatabase(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
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

using (var scope = app.Services.CreateScope())
    await Seeder.RunAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.MapControllers();
app.MapFallbackToFile("index.html"); // the React admin handles its own pages

app.Run();
