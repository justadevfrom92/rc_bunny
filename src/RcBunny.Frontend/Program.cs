using Microsoft.AspNetCore.Authentication.Cookies;
using RcBunny.Data;
using RcBunny.Frontend.Services;

// Show money and dates the US way, whatever language the server is set to.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo("en-US");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDatabase(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddScoped<Catalog>();
builder.Services.AddRazorPages(o => o.Conventions.AuthorizeFolder("/MyAccount").AllowAnonymousToPage("/MyAccount/Login"));

// Public pages are the same for everyone, so the finished HTML is kept for a minute and served straight from memory.
builder.Services.AddOutputCache(o => o.AddPolicy("Public", p => p.Expire(TimeSpan.FromSeconds(60)).SetVaryByQuery("zip", "type")));

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
app.UseOutputCache();
app.MapRazorPages();

app.Run();
