using System.Security.Cryptography;

namespace RcBunny.Backend.Infrastructure;

/// <summary>
/// Weak ETags on GET /api responses: when the data hasn't changed the browser gets a 304 with no body,
/// so revisiting a screen (or a background refetch) costs almost nothing on the wire.
/// User-specific data is marked private so shared proxies never store it.
/// </summary>
public sealed class ApiETagMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!HttpMethods.IsGet(ctx.Request.Method) || !ctx.Request.Path.StartsWithSegments("/api"))
        {
            await next(ctx);
            return;
        }

        var original = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        try
        {
            await next(ctx);

            if (ctx.Response.StatusCode == StatusCodes.Status200OK && buffer.Length > 0)
            {
                var etag = "W/\"" + Convert.ToBase64String(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))[..22] + "\"";
                ctx.Response.Headers.ETag = etag;
                if (string.IsNullOrEmpty(ctx.Response.Headers.CacheControl))
                    ctx.Response.Headers.CacheControl = "private, no-cache";   // revalidate each time, 304 when unchanged
                if (ctx.Request.Headers.IfNoneMatch == etag)
                {
                    ctx.Response.StatusCode = StatusCodes.Status304NotModified;
                    ctx.Response.ContentLength = 0;
                    return;
                }
            }
            buffer.Position = 0;
            await buffer.CopyToAsync(original);
        }
        finally
        {
            ctx.Response.Body = original;
        }
    }
}

/// <summary>
/// CSRF protection for a cookie-authenticated JSON API: every state-changing /api request must carry
/// X-RCB: 1. Browsers won't let another site add a custom header without a CORS preflight (which we never allow),
/// and the cookie is SameSite=Strict as a second line.
/// </summary>
public sealed class RequireApiHeaderMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx)
    {
        var m = ctx.Request.Method;
        if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(m) && !HttpMethods.IsHead(m) && !HttpMethods.IsOptions(m)
            && ctx.Request.Headers["X-RCB"] != "1")
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return ctx.Response.WriteAsJsonAsync(new { code = "missing_header", message = "Requests must come from the admin app." });
        }
        return next(ctx);
    }
}
