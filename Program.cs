using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// In-memory user store
var users = new ConcurrentDictionary<int, User>();
var nextId = 1;

// GET: Retrieve all users
app.MapGet("/users", () => users.Values);

// GET: Retrieve a user by ID
app.MapGet("/users/{id:int}", (int id) =>
    users.TryGetValue(id, out var user) ? Results.Ok(user) : Results.NotFound()
);

// POST: Add a new user
app.MapPost("/users", (UserDto userDto) =>
{
    var id = Interlocked.Increment(ref nextId);
    var user = new User(id, userDto.Name, userDto.Email);
    users[user.Id] = user;
    return Results.Created($"/users/{user.Id}", user);
});

// PUT: Update an existing user
app.MapPut("/users/{id:int}", (int id, UserDto userDto) =>
{
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
