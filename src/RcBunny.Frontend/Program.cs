using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.ResponseCompression;
using RcBunny.Data;
using RcBunny.Frontend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRcBunnyDatabase(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Catalog>();
builder.Services.AddSingleton<IslandAssets>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Brand").Get<Brand>() ?? new Brand());

// ---- Customer sign-in (My Account). Separate cookie and scheme from the employee admin. ----------------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "rcb.customer";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.LoginPath = "/myaccount/login";
        o.LogoutPath = "/myaccount/logout";
        o.ExpireTimeSpan = TimeSpan.FromHours(2);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/MyAccount");
    o.Conventions.AllowAnonymousToPage("/MyAccount/Login");
    // ebunny's addresses keep working (/plans.html …)
    o.Conventions.AddPageRoute("/Index", "index.html");
    o.Conventions.AddPageRoute("/Plans/Index", "plans.html");
    o.Conventions.AddPageRoute("/Business", "business.html");
    o.Conventions.AddPageRoute("/BuildYourOwnPlan", "build-your-own-plan.html");
    o.Conventions.AddPageRoute("/ContactUs", "contact-us.html");
});

// ---- Speed: pages are plain server-rendered HTML, cached whole on the server and compressed. -------------
builder.Services.AddOutputCache(o =>
{
    // Public marketing pages: same HTML for every visitor, so one render serves everyone for a minute.
    o.AddPolicy("Public", p => p
        .Expire(TimeSpan.FromSeconds(builder.Configuration.GetValue("Website:PageCacheSeconds", 60)))
        .SetVaryByQuery("zip", "filter", "topic", "plan")
        .Tag("pages"));
});
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("forms", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var req = ctx.Context.Request;
        // Hashed island bundles and ?v= versioned css/images never change: cache for a year. Anything else: a day.
        var immutable = req.Path.StartsWithSegments("/islands/assets") || req.Query.ContainsKey("v");
        ctx.Context.Response.Headers.CacheControl = immutable ? "public, max-age=31536000, immutable" : "public, max-age=86400";
    },
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapRazorPages();
app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

app.Run();
