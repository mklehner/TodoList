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

    // Nur bei relationalen Providern (Npgsql); Tests laufen mit InMemory und kennen keine Migrationen
    if (db.Database.IsRelational())
    {
        // DIESE ZEILE HINZUFÜGEN: Gibt das komplette SQL-Skript im Docker-Log aus!
        Console.WriteLine(db.Database.GenerateCreateScript());

        db.Database.Migrate(); // <-- Geändert von EnsureCreated() zu Migrate()
    }
}

// REST-Endpunkte für die Todo-Liste
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

// -------------------------------------------------------------------------------------------------------------
// --- NEUE MINIMAL API ENDPUNKTE FÜR BEWERBUNG ---

app.MapGet("/api/bewerbung", async (CatalogDbContext context) =>
{
    return await context.Bewerbungen.ToListAsync();
});

// NEU: Create-Endpunkt für das Neuanlegen einer Bewerbung
app.MapPost("/api/bewerbung", async (Bewerbung neueBewerbung, CatalogDbContext context) =>
{
    // last chance Validierung auf tiefer Ebene (wird in der Oberfläche required oder min verhindert)
    // Entweder man validiert es im Controller oder hier -- beim Edit ist die Validierung im Controller
    // ich habs mal so und mal so gemacht um die Codierungstechniken zu eroieren 
    // hier hat man den Vorteil, dass man einen BadRequest reusltieren kann!
    if (string.IsNullOrWhiteSpace(neueBewerbung.JobTitle) || string.IsNullOrWhiteSpace(neueBewerbung.Unternehmen))    
        return Results.BadRequest("JobTitle und Unternehmen dürfen nicht leer sein.");

    else if (neueBewerbung.BatchId < 0)            
        return Results.BadRequest("Fehler: Das Feld 'batchId' ist im Controller < 0 !");

    // alle anderen Pflichtfelder (Freelance, IsCompleted, Priority und Status)
    // beinhalten über das Control automatisch Werte, auch bei Nicht-Auswahl

    // Fehler: Das wäre ein ungewollter Automatismus, der Wert darf uns soll unbelegt bleiben
    //##-- if (neueBewerbung.BewerbungsDatum == default)
    //##--    neueBewerbung.BewerbungsDatum = DateTime.UtcNow;

    context.Bewerbungen.Add(neueBewerbung);
    await context.SaveChangesAsync();

    return Results.Created($"/api/bewerbung/{neueBewerbung.Id}", neueBewerbung);
});

// NEU: Edit-Endpunkt für das Bearbeiten / Aktualisieren einer Bewerbung
// übrigens: MapPut ist "MinimalAPI" Minimal APIs erlauben es, 
// den CatalogDbContext db direkt als Parameter in die Methode zu injizieren.
app.MapPut("/api/bewerbung/{id}", async (int id, Bewerbung updBewerbung, CatalogDbContext db) =>
{
    var bewerbung = await db.Bewerbungen.FindAsync(id);
    if (bewerbung == null) return Results.NotFound();

    bewerbung.JobTitle = updBewerbung.JobTitle;
    bewerbung.Unternehmen = updBewerbung.Unternehmen;
    bewerbung.IsFreelance = updBewerbung.IsFreelance;
    bewerbung.Notes = updBewerbung.Notes;
    bewerbung.Priority = updBewerbung.Priority;
    bewerbung.DueDate = updBewerbung.DueDate;
    bewerbung.BatchId = updBewerbung.BatchId;
    bewerbung.Description = updBewerbung.Description;
    bewerbung.Status = updBewerbung.Status;
    bewerbung.Anschreiben = updBewerbung.Anschreiben;
    bewerbung.Lebenslauf = updBewerbung.Lebenslauf;
    bewerbung.BewerbungsDatum = updBewerbung.BewerbungsDatum;
    bewerbung.VorstellungsTermin = updBewerbung.VorstellungsTermin;
    bewerbung.Verbleib = updBewerbung.Verbleib;
    bewerbung.Kontakt = updBewerbung.Kontakt;

    // CreatedDate soll nicht mehr geändert werden, 
    // es sei denn es handelt sich um alte Datensätze bei welchen das Datum noch nicht gespeichert wurde
    // Der Vergleich darf nur hier mit dem wert vom Context vorgenommen werden, nicht im Controller!!
    // Siehe ausf. Kommentar im Controller
    if (!bewerbung.CreatedDate.HasValue)
        bewerbung.CreatedDate = updBewerbung.LastChangeDate;

    bewerbung.LastChangeDate = updBewerbung.LastChangeDate;
    
    await db.SaveChangesAsync();
    return Results.Ok(bewerbung);
});

app.MapPut("/api/bewerbung/{id}/toggle", async (int id, CatalogDbContext db) =>
{
    var bewerbung = await db.Bewerbungen.FindAsync(id);
    if (bewerbung == null) 
        return Results.NotFound();
    
    // das anhaken wirkt wie eine Änderung
    // Holt die aktuelle deutsche Uhrzeit (inkl. automatischer Sommer-/Winterzeit)
    var zoneDe = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zoneDe); //DateTime.Now;
    DateTime? lastChangeDate = localNow;

    bewerbung.LastChangeDate = lastChangeDate;

    bewerbung.IsCompleted = !bewerbung.IsCompleted;

    await db.SaveChangesAsync();
    return Results.Ok(bewerbung);
});

app.MapDelete("/api/bewerbung/{id}", async (int id, CatalogDbContext db) =>
{
    var bewerbung = await db.Bewerbungen.FindAsync(id);
    if (bewerbung == null) return Results.NotFound();

    db.Bewerbungen.Remove(bewerbung);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPut("/api/bewerbung/{id}/batchId/{batchId}", async (int id, int batchId, CatalogDbContext db) =>
{
    var bewerbung = await db.Bewerbungen.FindAsync(id);
    if (bewerbung == null) 
        return Results.NotFound();
    
    //Results.BadRequest($"new BatchId: {batchId}");

    // // das anhaken wirkt wie eine Änderung
    // // Holt die aktuelle deutsche Uhrzeit (inkl. automatischer Sommer-/Winterzeit)
    // var zoneDe = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    // var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zoneDe); //DateTime.Now;
    // DateTime? lastChangeDate = localNow;
    // bewerbung.LastChangeDate = lastChangeDate;

    bewerbung.BatchId = batchId;

    await db.SaveChangesAsync();
    return Results.Ok(bewerbung);
});

app.MapPut("/api/bewerbung/{id}/status/{status}", async (int id, string status, CatalogDbContext db) =>
{
    var bewerbung = await db.Bewerbungen.FindAsync(id);
    if (bewerbung == null) 
        return Results.NotFound();
    
    //Results.BadRequest($"new BatchId: {batchId}");

    // // das anhaken wirkt wie eine Änderung
    // // Holt die aktuelle deutsche Uhrzeit (inkl. automatischer Sommer-/Winterzeit)
    // var zoneDe = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    // var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zoneDe); //DateTime.Now;
    // DateTime? lastChangeDate = localNow;
    // bewerbung.LastChangeDate = lastChangeDate;

    bewerbung.Status = status;

    await db.SaveChangesAsync();
    return Results.Ok(bewerbung);
});

// -------------------------------------------------------------------------------------------------------------

// NEU: Automatische Weiterleitung von der Startseite direkt zu Swagger
app.MapGet("/", (HttpContext context) => {
    context.Response.Redirect("/swagger");
    return Task.CompletedTask;
});

app.Run();

// Macht die Top-Level-Program-Klasse für WebApplicationFactory<Program> in den Tests sichtbar
public partial class Program { }

