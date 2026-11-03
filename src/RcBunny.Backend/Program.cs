using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using RcBunny.Backend.Access;
using RcBunny.Backend.Infrastructure;
using RcBunny.Data;
using RcBunny.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRcBunnyDatabase(builder.Configuration);
builder.Services.AddScoped<DbSeeder>();

// ---- Caching -------------------------------------------------------------------------------------------
// HybridCache = in-memory (L1) + optional distributed (L2) with tag invalidation and stampede protection.
// For more than one server, add a distributed cache (e.g. AddStackExchangeRedisCache) and it becomes the L2
// automatically, so invalidations reach every node.
builder.Services.AddHybridCache(o =>
{
    o.DefaultEntryOptions = new() { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromMinutes(1) };
    o.MaximumPayloadBytes = 4 * 1024 * 1024;
}).AddSerializerFactory<WebJsonCacheSerializerFactory>();
builder.Services.AddResponseCaching();

// ---- Employee sign-in and permissions ------------------------------------------------------------------
builder.Services.AddScoped<AccessService>();
builder.Services.AddScoped<CookieEvents>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "rcb.admin";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(10);   // a working day; sliding while they use it
        o.SlidingExpiration = true;
        o.EventsType = typeof(CookieEvents);
    });
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermPolicy>();
builder.Services.AddScoped<IAuthorizationHandler, PermHandler>();
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().InitializeAsync();
// `dotnet run -- --seed-only`: create/migrate the database and load the sample data, then exit.
if (args.Contains("--seed-only")) return;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

// The React app: hashed asset files are cached for a year, index.html is always revalidated.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        ctx.Context.Response.Headers.CacheControl = path.StartsWith("/assets/")
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    },
});

app.UseRateLimiter();
app.UseMiddleware<RequireApiHeaderMiddleware>();
app.UseMiddleware<ApiETagMiddleware>();
app.UseResponseCaching();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { ok = true })).AllowAnonymous();
// Any other /api path is a 404 (not the SPA); everything else is a client route.
app.Map("/api/{**rest}", () => Results.NotFound(new { code = "not_found" }));
app.MapFallbackToFile("index.html");

app.Run();
