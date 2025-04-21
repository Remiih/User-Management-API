var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Add exception handling middleware (FIRST)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        
        await context.Response.WriteAsJsonAsync(new { error = "Internal server error." });
    }
});

// Add token validation middleware (SECOND)
app.Use(async (context, next) =>
{
    var token = context.Request.Headers.Authorization.FirstOrDefault()?.Split(" ").Last();
    
    if (string.IsNullOrEmpty(token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "No token provided." });
        return;
    }

    if (token != "valid-token") // Temporary token validation, replace with proper JWT validation
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Invalid token." });
        return;
    }

    await next();
});

// Add logging middleware (THIRD)
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;

    // Log the HTTP method and path
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");

    await next();

    // Log the status code after the response is generated
    Console.WriteLine($"Response Status: {context.Response.StatusCode} - Took: {(DateTime.UtcNow - start).TotalMilliseconds}ms");
});

app.UseHttpsRedirection();

// In-memory storage for users (for demo purposes)
var users = new List<User>();
var nextId = 1;

// GET - Retrieve all users with pagination
app.MapGet("/users", (int page = 1, int pageSize = 10) =>
{
    var totalUsers = users.Count;
    var totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);
    
    var pagedUsers = users
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToList();
        
    return Results.Ok(new {
        Data = pagedUsers,
        TotalItems = totalUsers,
        TotalPages = totalPages,
        CurrentPage = page,
        PageSize = pageSize
    });
});

// GET - Retrieve user by ID
app.MapGet("/users/{id}", (int id) =>
{
    if (id <= 0) return Results.BadRequest("Invalid ID. ID must be greater than 0.");
    
    var user = users.FirstOrDefault(u => u.Id == id);
    return user is null ? Results.NotFound("User not found.") : Results.Ok(user);
});

// POST - Create new user
app.MapPost("/users", (User user) =>
{
    // Validate user input
    var validationErrors = ValidateUser(user);
    if (validationErrors.Any())
    {
        return Results.BadRequest(new { Errors = validationErrors });
    }

    user.Id = nextId++;
    users.Add(user);
    return Results.Created($"/users/{user.Id}", user);
});

// PUT - Update user
app.MapPut("/users/{id}", (int id, User updatedUser) =>
{
    if (id <= 0) return Results.BadRequest("Invalid ID. ID must be greater than 0.");

    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound("User not found.");

    // Validate user input
    var validationErrors = ValidateUser(updatedUser);
    if (validationErrors.Any())
    {
        return Results.BadRequest(new { Errors = validationErrors });
    }
    
    user.Name = updatedUser.Name;
    user.Email = updatedUser.Email;
    
    return Results.Ok(user);
});

// DELETE - Remove user
app.MapDelete("/users/{id}", (int id) =>
{
    if (id <= 0) return Results.BadRequest("Invalid ID. ID must be greater than 0.");

    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound("User not found.");
    
    users.Remove(user);
    return Results.Ok("User successfully deleted.");
});

// Validation helper method
static List<string> ValidateUser(User user)
{
    var errors = new List<string>();

    // Name validation
    if (string.IsNullOrWhiteSpace(user.Name))
    {
        errors.Add("Name is required.");
    }
    else if (user.Name.Length < 2)
    {
        errors.Add("Name must be at least 2 characters long.");
    }
    else if (user.Name.Length > 100)
    {
        errors.Add("Name cannot exceed 100 characters.");
    }

    // Email validation
    if (string.IsNullOrWhiteSpace(user.Email))
    {
        errors.Add("Email is required.");
    }
    else if (!IsValidEmail(user.Email))
    {
        errors.Add("Invalid email format.");
    }
    else if (user.Email.Length > 255)
    {
        errors.Add("Email cannot exceed 255 characters.");
    }

    return errors;
}

// Email validation helper method
static bool IsValidEmail(string email)
{
    try
    {
        var addr = new System.Net.Mail.MailAddress(email);
        return addr.Address == email;
    }
    catch
    {
        return false;
    }
}

app.Run();

// User model
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

