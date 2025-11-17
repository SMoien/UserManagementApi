using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add CORS (allow all for demo; restrict in production)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// Logging helper
ILogger logger = app.Logger;

// Logging middleware for HTTP method, path, and response status code
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var path = context.Request.Path;

    await next();

    var statusCode = context.Response.StatusCode;
    logger.LogInformation("HTTP {Method} {Path} responded {StatusCode}", method, path, statusCode);
});

// Custom exception handling middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled exception occurred.");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var errorResponse = new { error = "Internal server error." };
        await context.Response.WriteAsJsonAsync(errorResponse);
    }
});

// Token validation middleware (place this after app.UseCors() and before other middlewares)
app.Use(async (context, next) =>
{
    // Example: Expect token in Authorization header as "Bearer {token}"
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (authHeader is null || !authHeader.StartsWith("Bearer "))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Unauthorized: Missing or invalid token." });
        return;
    }

    var token = authHeader.Substring("Bearer ".Length).Trim();

    // Replace this with your real token validation logic
    var validTokens = new[] { "mysecrettoken123", "another-valid-token" };
    if (!validTokens.Contains(token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Unauthorized: Invalid token." });
        return;
    }

    await next();
});

// In-memory user store (for demo only)
var users = new ConcurrentDictionary<int, User>();
var nextId = 0; // So first user gets ID 1

// GET: Retrieve all users
app.MapGet("/users", () =>
{
    try
    {
        logger.LogInformation("Retrieving all users.");
        return Results.Ok(users.Values);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving all users.");
        return Results.Problem("An unexpected error occurred.");
    }
})
.WithName("GetAllUsers")
.WithTags("Users");

// GET: Retrieve a user by ID
app.MapGet("/users/{id:int}", (int id) =>
{
    try
    {
        if (users.TryGetValue(id, out var user))
        {
            return Results.Ok(user);
        }
        else
        {
            return Results.NotFound(new { Message = $"User with ID {id} not found." });
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving user by ID.");
        return Results.Problem("An unexpected error occurred.");
    }
})
.WithName("GetUserById")
.WithTags("Users");

// POST: Add a new user
app.MapPost("/users", (UserDto userDto) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(userDto.Name) || string.IsNullOrWhiteSpace(userDto.Email))
        {
            return Results.BadRequest(new { Message = "Name and Email are required." });
        }

        if (users.Values.Any(u => u.Email.Equals(userDto.Email, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.BadRequest(new { Message = "Email already exists." });
        }

        var id = Interlocked.Increment(ref nextId);
        var user = new User(id, userDto.Name, userDto.Email);
        users[user.Id] = user;
        logger.LogInformation("User created: {User}", user);
        return Results.Created($"/users/{user.Id}", user);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating user.");
        return Results.Problem("An unexpected error occurred.");
    }
})
.WithName("CreateUser")
.WithTags("Users");

// PUT: Update an existing user
app.MapPut("/users/{id:int}", (int id, UserDto userDto) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(userDto.Name) || string.IsNullOrWhiteSpace(userDto.Email))
        {
            return Results.BadRequest(new { Message = "Name and Email are required." });
        }

        if (!users.ContainsKey(id))
            return Results.NotFound(new { Message = $"User with ID {id} not found." });

        if (users.Values.Any(u => u.Email.Equals(userDto.Email, StringComparison.OrdinalIgnoreCase) && u.Id != id))
        {
            return Results.BadRequest(new { Message = "Email already exists." });
        }

        var updatedUser = new User(id, userDto.Name, userDto.Email);
        users[id] = updatedUser;
        logger.LogInformation("User updated: {User}", updatedUser);
        return Results.Ok(updatedUser);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating user.");
        return Results.Problem("An unexpected error occurred.");
    }
})
.WithName("UpdateUser")
.WithTags("Users");

// DELETE: Remove a user by ID
app.MapDelete("/users/{id:int}", (int id) =>
{
    try
    {
        if (users.TryRemove(id, out var removed))
        {
            logger.LogInformation("User deleted: {User}", removed);
            return Results.NoContent();
        }
        else
        {
            return Results.NotFound(new { Message = $"User with ID {id} not found." });
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deleting user.");
        return Results.Problem("An unexpected error occurred.");
    }
})
.WithName("DeleteUser")
.WithTags("Users");

// Placeholder for authentication/authorization
// app.UseAuthentication();
// app.UseAuthorization();

// Placeholder for rate limiting
// builder.Services.AddRateLimiter(...);

app.Run();

// DTO for creating/updating users
public record UserDto(
    [property: Required] string Name,
    [property: Required] string Email
);

// User model
public record User(int Id, string Name, string Email);

// Unit test placeholder: Create a separate test project and add tests for all endpoints and validation logic.
