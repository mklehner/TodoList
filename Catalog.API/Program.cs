using Catalog.API;
using Catalog.API.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 1. DIESE ZEILE HINZUFÜGEN: Sagt PostgreSQL, dass es normale Datumsformate ohne Zeitzonenzwang akzeptieren soll
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

// Erstellt die Todo-Tabelle in der DB beim Start
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    db.Database.EnsureCreated();
}

// REST-Endpunkte für die To-Do-Liste
app.MapGet("/api/todos", async (CatalogDbContext db) => 
    await db.Todos.OrderByDescending(t => t.Priority == "Hoch")
                  .ThenByDescending(t => t.Priority == "Mittel")
                  .ToListAsync());

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

app.Run();

