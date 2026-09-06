using Catalog.API;
using Catalog.API.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 1. DIESE ZEILE HINZUFÜGEN: Sagt PostgreSQL, dass es normale Datumsformate ohne Zeitzonenzwang akzeptieren soll
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));

// ─── 1. SWAGGER-DIENSTE REGISTRIEREN ───────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ─── 2. SWAGGER MIDDLEWARE AKTIVIEREN ─────────────────────────────────
// In Docker-Containern erzwingen wir Swagger unabhängig von der Environment
app.UseSwagger();
app.UseSwaggerUI(c => 
{
    // Nutzt den Standard-Pfad für die OpenAPI-Schnittstellenbeschreibung
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1");
});

// Erstellt die Todo-Tabelle in der DB beim Start
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    db.Database.EnsureCreated();
}

// REST-Endpunkte für die To-Do-Liste
// REST-Endpunkt erweitert um optionale Filter-Parameter
app.MapGet("/api/todos", async (string? search, string? priority, CatalogDbContext db) => 
{
    // Wir starten mit der Grundabfrage auf die Tabelle
    var query = db.Todos.AsQueryable();

    // 1. Filter: Suchtext (Groß-/Kleinschreibung ignorieren)
    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(t => t.Title.ToLower().Contains(search.ToLower()));
    }

    // 2. Filter: Spezifische Priorität
    if (!string.IsNullOrWhiteSpace(priority) && priority != "Alle")
    {
        query = query.Where(t => t.Priority == priority);
    }

    // Sortierung anwenden und Liste zurückgeben
    return await query
        .OrderByDescending(t => t.Priority == "Hoch")
        .ThenByDescending(t => t.Priority == "Mittel")
        .ToListAsync();
});

app.MapPost("/api/todos", async (TodoItem todo, CatalogDbContext db) =>
{
    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return Results.Created($"/api/todos/{todo.Id}", todo);
});

app.MapPut("/api/todos/{id}/toggle", async (int id, CatalogDbContext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();
    
    todo.IsCompleted = !todo.IsCompleted;
    await db.SaveChangesAsync();
    return Results.Ok(todo);
});

// NEU: Endpunkt für das Bearbeiten / Aktualisieren einer Aufgabe
app.MapPut("/api/todos/{id}", async (int id, TodoItem updatedTodo, CatalogDbContext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    todo.Title = updatedTodo.Title;
    todo.Notes = updatedTodo.Notes;
    todo.Priority = updatedTodo.Priority;
    todo.DueDate = updatedTodo.DueDate;

    await db.SaveChangesAsync();
    return Results.Ok(todo);
});

app.MapDelete("/api/todos/{id}", async (int id, CatalogDbContext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    db.Todos.Remove(todo);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// NEU: Automatische Weiterleitung von der Startseite direkt zu Swagger
app.MapGet("/", (HttpContext context) => {
    context.Response.Redirect("/swagger");
    return Task.CompletedTask;
});

app.Run();

