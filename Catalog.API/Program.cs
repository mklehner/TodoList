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
    await db.Todos.ToListAsync());

app.MapPost("/api/todos", async (TodoItem todo, CatalogDbContext db) =>
{
    // Der UTC-Zwang-Code wurde entfernt, da Npgsql jetzt das normale Datum akzeptiert!
    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return Results.Created($"/api/todos/{todo.Id}", todo);
});

// Endpunkt um den Status (Erledigt/Offen) zu toggeln
app.MapPut("/api/todos/{id}/toggle", async (int id, CatalogDbContext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();
    
    todo.IsCompleted = !todo.IsCompleted;
    await db.SaveChangesAsync();
    return Results.Ok(todo);
});

// NEU: DELETE-Endpunkt zum Löschen einer Aufgabe
app.MapDelete("/api/todos/{id}", async (int id, CatalogDbContext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    db.Todos.Remove(todo);
    await db.SaveChangesAsync();
    return Results.NoContent(); // HTTP 204
});

app.Run();

