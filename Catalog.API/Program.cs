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

    // DIESE ZEILE HINZUFÜGEN: Gibt das komplette SQL-Skript im Docker-Log aus!
    Console.WriteLine(db.Database.GenerateCreateScript());

    db.Database.Migrate(); // <-- Geändert von EnsureCreated() zu Migrate()
}

// REST-Endpunkte für die To-Do-Liste
// REST-Endpunkt erweitert um optionale Filter-Parameter
app.MapGet("/api/todos", async (string? search, string? priority, int? batchId, CatalogDbContext db) => 
{
    var query = db.Todos.AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(t => t.Title.ToLower().Contains(search.ToLower()));
    }

    if (!string.IsNullOrWhiteSpace(priority) && priority != "Alle")
    {
        query = query.Where(t => t.Priority == priority);
    }

    if (batchId.HasValue)
    {
        query = query.Where(t => t.BatchId == batchId.Value);
    }

    // Wir holen alle To-Dos. Die Baum-Strukturierung machen wir gleich im Frontend.
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

    // das anhaken wirkt wie eine Änderung
    // Holt die aktuelle deutsche Uhrzeit (inkl. automatischer Sommer-/Winterzeit)
    var zoneDe = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zoneDe); //DateTime.Now;
    DateTime? lastChangeDate = localNow;

    todo.LastChangeDate = lastChangeDate;

    await db.SaveChangesAsync();
    return Results.Ok(todo);
});

// NEU: Endpunkt für das Bearbeiten / Aktualisieren einer Aufgabe
// übrigens: MapPut ist "MinimalAPI" Minimal APIs erlauben es, 
// den CatalogDbContext db direkt als Parameter in die Methode zu injizieren.
app.MapPut("/api/todos/{id}", async (int id, TodoItem updatedTodo, CatalogDbContext db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    todo.Title = updatedTodo.Title;
    todo.Notes = updatedTodo.Notes;
    todo.Priority = updatedTodo.Priority;
    todo.DueDate = updatedTodo.DueDate;
    todo.BatchId = updatedTodo.BatchId;
    todo.Description = updatedTodo.Description;
    
    if (!todo.CreatedDate.HasValue)
        todo.CreatedDate = updatedTodo.LastChangeDate;

    todo.LastChangeDate = updatedTodo.LastChangeDate;
    
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

