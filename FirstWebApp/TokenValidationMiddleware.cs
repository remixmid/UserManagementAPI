using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;

public class TokenValidationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        // Check for Authorization header
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized: Missing or invalid token." });
            return;
        }

        // Token extraction
        var token = authHeader.Substring("Bearer ".Length).Trim();

        // Validate token using built-in authentication
        var result = await context.AuthenticateAsync();
        if (!result.Succeeded)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized: Invalid token." });
            return;
        }

        await _next(context);
    }
}