using Microsoft.AspNetCore.Authentication.Cookies;
using RcBunny.Data;
using RcBunny.Frontend.Services;

// Show money and dates the US way, whatever language the server is set to.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo("en-US");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDatabase(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/MyAccount").AllowAnonymousToPage("/MyAccount/Login");
    o.Conventions.AddPageRoute("/Index", "index.html"); // main.js links home as index.html
});


// Customers sign in to My Account with their own cookie (separate from the employee admin).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "rcb.customer";
        o.LoginPath = "/myaccount/login";
        o.ExpireTimeSpan = TimeSpan.FromHours(2);
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
app.UseStaticFiles();
app.UseMiddleware<Maintenance>(); // Sheriff can switch the website off
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapSite();       // what main.js asks the server for (Services/SiteEndpoints.cs)
app.MapRazorPages(); // the website's pages and My Account

app.Run();
