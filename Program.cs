using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// Add global exception handler middleware
builder.Services.AddExceptionHandler(options =>
{
    options.ExceptionHandler = async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { Message = "An unexpected error occurred. Please try again later." });
    };
});

var app = builder.Build();

// Use the exception handler middleware
app.UseExceptionHandler();

// In-memory user store
var users = new ConcurrentDictionary<int, User>();
var nextId = 1;

// GET: Retrieve all users
app.MapGet("/users", () => users.Values);

// GET: Retrieve a user by ID
app.MapGet("/users/{id:int}", (int id) =>
{
    if (users.TryGetValue(id, out var user))
    {
        return Results.Ok(user);
    }
    else
    {
        return Results.NotFound(new { Message = $"User with ID {id} not found." });
    }
});

// POST: Add a new user
app.MapPost("/users", (UserDto userDto) =>
{
    if (string.IsNullOrWhiteSpace(userDto.Name) || string.IsNullOrWhiteSpace(userDto.Email))
    {
        return Results.BadRequest("Name and Email are required.");
    }

    var id = Interlocked.Increment(ref nextId);
    var user = new User(id, userDto.Name, userDto.Email);
    users[user.Id] = user;
    return Results.Created($"/users/{user.Id}", user);
});

// PUT: Update an existing user
app.MapPut("/users/{id:int}", (int id, UserDto userDto) =>
{
    if (string.IsNullOrWhiteSpace(userDto.Name) || string.IsNullOrWhiteSpace(userDto.Email))
    {
        return Results.BadRequest("Name and Email are required.");
    }

    if (!users.ContainsKey(id))
        return Results.NotFound();

    var updatedUser = new User(id, userDto.Name, userDto.Email);
    users[id] = updatedUser;
    return Results.Ok(updatedUser);
});

// DELETE: Remove a user by ID
app.MapDelete("/users/{id:int}", (int id) =>
{
    return users.TryRemove(id, out _) ? Results.NoContent() : Results.NotFound();
});

app.Run();

// DTO for creating/updating users
record UserDto(string Name, string Email);

// User model
record User(int Id, string Name, string Email);
