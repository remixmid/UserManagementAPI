using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// JWT Authentication setup (use a secure key in production)
var jwtKey = "SuperSecretKeyForTechHiveSolutions123!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization(); // <-- Add this line

var users = new ConcurrentDictionary<int, User>();
users.TryAdd(1, new User { Id = 1, Name = "Alice", Email = "alice@techhive.com" });
users.TryAdd(2, new User { Id = 2, Name = "Bob", Email = "bob@techhive.com" });

var app = builder.Build();

// Error Handling Middleware (first)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Authentication Middleware (next)
app.UseAuthentication();
app.UseMiddleware<TokenValidationMiddleware>();
app.UseAuthorization();

// Logging Middleware (last)
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// GET: Retrieve all users with optional paging
app.MapGet("/users", ([FromQuery] int? page, [FromQuery] int? pageSize) =>
{
    var userList = users.Values.OrderBy(u => u.Id).ToList();
    if (page.HasValue && pageSize.HasValue && page > 0 && pageSize > 0)
    {
        userList = userList.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToList();
    }
    return Results.Ok(userList);
}).RequireAuthorization();

// GET: Retrieve a user by ID
app.MapGet("/users/{id:int}", (int id) =>
{
    if (users.TryGetValue(id, out var user))
        return Results.Ok(user);
    return Results.NotFound(new { Message = $"User with ID {id} not found." });
}).RequireAuthorization();

// POST: Add a new user
app.MapPost("/users", ([FromBody] User newUser) =>
{
    var validation = ValidateUser(newUser);
    if (!validation.IsValid)
        return Results.BadRequest(new { Message = validation.Error });

    if (users.Values.Any(u => u.Email.Equals(newUser.Email, StringComparison.OrdinalIgnoreCase)))
        return Results.BadRequest(new { Message = "Email already exists." });

    int newId = users.Keys.Any() ? users.Keys.Max() + 1 : 1;
    newUser.Id = newId;
    if (!users.TryAdd(newId, newUser))
        return Results.Problem("Failed to add user due to a concurrency issue.");
    return Results.Created($"/users/{newUser.Id}", newUser);
}).RequireAuthorization();

// PUT: Update an existing user's details
app.MapPut("/users/{id:int}", (int id, [FromBody] User updatedUser) =>
{
    var validation = ValidateUser(updatedUser);
    if (!validation.IsValid)
        return Results.BadRequest(new { Message = validation.Error });

    if (!users.TryGetValue(id, out var user))
        return Results.NotFound(new { Message = $"User with ID {id} not found." });

    if (users.Values.Any(u => u.Email.Equals(updatedUser.Email, StringComparison.OrdinalIgnoreCase) && u.Id != id))
        return Results.BadRequest(new { Message = "Email already exists." });

    user.Name = updatedUser.Name;
    user.Email = updatedUser.Email;
    users[id] = user;
    return Results.Ok(user);
}).RequireAuthorization();

// DELETE: Remove a user by ID
app.MapDelete("/users/{id:int}", (int id) =>
{
    if (!users.TryRemove(id, out var _))
        return Results.NotFound(new { Message = $"User with ID {id} not found." });
    return Results.NoContent();
}).RequireAuthorization();

app.Run();

// Helper for email validation and required fields
static (bool IsValid, string Error) ValidateUser(User user)
{
    if (user == null)
        return (false, "User data is required.");
    if (string.IsNullOrWhiteSpace(user.Name))
        return (false, "Name is required.");
    if (string.IsNullOrWhiteSpace(user.Email))
        return (false, "Email is required.");
    if (!Regex.IsMatch(user.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        return (false, "Valid email is required.");
    return (true, "");
}

// User model
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}