using System.Net;
using System.Net.Http.Json;
using Catalog.API.Models;

namespace Catalog.API.Tests;

/// <summary>
/// Charakterisierungstests für /api/todos: halten das heutige Verhalten fest.
/// Tests mit "Quirk" im Namen dokumentieren bekannte Schwächen (siehe Refactoring-Plan) –
/// sie sollen beim jeweiligen Refactoring bewusst angepasst werden.
/// </summary>
public class TodoEndpointsTests : IDisposable
{
    private readonly CatalogApiFactory _factory = new();
    private readonly HttpClient _client;

    public TodoEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task SeedAsync(params TodoItem[] todos) =>
        _factory.WithDbAsync(async db =>
        {
            db.Todos.AddRange(todos);
            await db.SaveChangesAsync();
        });

    private Task<TodoItem?> LoadAsync(int id) =>
        _factory.WithDbAsync(db => db.Todos.FindAsync(id).AsTask());

    // ─── GET /api/todos ────────────────────────────────────────────────

    [Fact]
    public async Task Get_ReturnsEmptyList_WhenNoTodosExist()
    {
        var todos = await _client.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task Get_SearchFiltersByTitle_CaseInsensitive()
    {
        await SeedAsync(
            new TodoItem { Title = "Bewerbung abschicken" },
            new TodoItem { Title = "Einkaufen" });

        var todos = await _client.GetFromJsonAsync<List<TodoItem>>("/api/todos?search=BEWERBUNG");

        var single = Assert.Single(todos!);
        Assert.Equal("Bewerbung abschicken", single.Title);
    }

    [Fact]
    public async Task Get_PriorityFilter_ReturnsOnlyMatching()
    {
        await SeedAsync(
            new TodoItem { Title = "A", Priority = "Hoch" },
            new TodoItem { Title = "B", Priority = "Niedrig" });

        var todos = await _client.GetFromJsonAsync<List<TodoItem>>("/api/todos?priority=Hoch");

        Assert.Equal("A", Assert.Single(todos!).Title);
    }

    [Fact]
    public async Task Get_PriorityAlle_IsTreatedAsNoFilter()
    {
        await SeedAsync(
            new TodoItem { Title = "A", Priority = "Hoch" },
            new TodoItem { Title = "B", Priority = "Niedrig" });

        var todos = await _client.GetFromJsonAsync<List<TodoItem>>("/api/todos?priority=Alle");

        Assert.Equal(2, todos!.Count);
    }

    [Fact]
    public async Task Get_EmptyPriorityAndSearch_AreTreatedAsNoFilter()
    {
        // So ruft das Frontend die API auf, wenn nichts gefiltert wird (search=&priority=)
        await SeedAsync(new TodoItem { Title = "A" }, new TodoItem { Title = "B" });

        var todos = await _client.GetFromJsonAsync<List<TodoItem>>("/api/todos?search=&priority=");

        Assert.Equal(2, todos!.Count);
    }

    [Fact]
    public async Task Get_BatchIdFilter_ReturnsOnlyMatching()
    {
        await SeedAsync(
            new TodoItem { Title = "A", BatchId = 1 },
            new TodoItem { Title = "B", BatchId = 2 });

        var todos = await _client.GetFromJsonAsync<List<TodoItem>>("/api/todos?batchId=2");

        Assert.Equal("B", Assert.Single(todos!).Title);
    }

    [Fact]
    public async Task Get_SortsHochBeforeMittelBeforeOthers()
    {
        await SeedAsync(
            new TodoItem { Title = "niedrig", Priority = "Niedrig" },
            new TodoItem { Title = "mittel", Priority = "Mittel" },
            new TodoItem { Title = "hoch", Priority = "Hoch" });

        var todos = await _client.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        Assert.Equal(new[] { "hoch", "mittel", "niedrig" }, todos!.Select(t => t.Title));
    }

    // ─── POST /api/todos ───────────────────────────────────────────────

    [Fact]
    public async Task Post_CreatesTodo_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/todos",
            new { Title = "Neu", Priority = "Hoch", Notes = "n", ParentId = (int?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal($"/api/todos/{created.Id}", response.Headers.Location!.OriginalString);

        var stored = await LoadAsync(created.Id);
        Assert.Equal("Neu", stored!.Title);
        Assert.Equal("Hoch", stored.Priority);
    }

    [Fact]
    public async Task Post_WithoutBody_Values_UsesDefaults()
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new { Title = "Nur Titel" });
        var created = await response.Content.ReadFromJsonAsync<TodoItem>();

        Assert.Equal("Mittel", created!.Priority);
        Assert.False(created.IsCompleted);
        Assert.Null(created.ParentId);
    }

    [Fact]
    public async Task Post_Quirk_NoValidation_EmptyTitleIsAccepted()
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new { Title = "" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_Quirk_ClientControlsCreatedDate()
    {
        // Overposting: Die API übernimmt CreatedDate/LastChangeDate unverändert vom Client
        var date = new DateTime(2020, 1, 2, 3, 4, 5);

        var response = await _client.PostAsJsonAsync("/api/todos",
            new { Title = "x", CreatedDate = date, LastChangeDate = date });
        var created = await response.Content.ReadFromJsonAsync<TodoItem>();

        Assert.Equal(date, created!.CreatedDate);
        Assert.Equal(date, created.LastChangeDate);
    }

    // ─── PUT /api/todos/{id}/toggle ────────────────────────────────────

    [Fact]
    public async Task Toggle_FlipsIsCompleted_AndSetsLastChangeDate()
    {
        var todo = new TodoItem { Title = "T", IsCompleted = false };
        await SeedAsync(todo);

        var response = await _client.PutAsync($"/api/todos/{todo.Id}/toggle", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await LoadAsync(todo.Id);
        Assert.True(stored!.IsCompleted);
        AssertIsRecentGermanLocalTime(stored.LastChangeDate);

        await _client.PutAsync($"/api/todos/{todo.Id}/toggle", null);
        Assert.False((await LoadAsync(todo.Id))!.IsCompleted);
    }

    [Fact]
    public async Task Toggle_UnknownId_Returns404()
    {
        var response = await _client.PutAsync("/api/todos/9999/toggle", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PUT /api/todos/{id} ───────────────────────────────────────────

    [Fact]
    public async Task Put_UpdatesEditableFields()
    {
        var todo = new TodoItem { Title = "alt", Priority = "Mittel", BatchId = 1 };
        await SeedAsync(todo);
        var due = new DateTime(2030, 5, 6);

        var response = await _client.PutAsJsonAsync($"/api/todos/{todo.Id}", new
        {
            Title = "neu",
            Notes = "notiz",
            Priority = "Hoch",
            DueDate = due,
            BatchId = 7,
            Description = "beschreibung",
            LastChangeDate = new DateTime(2026, 1, 1)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await LoadAsync(todo.Id);
        Assert.Equal("neu", stored!.Title);
        Assert.Equal("notiz", stored.Notes);
        Assert.Equal("Hoch", stored.Priority);
        Assert.Equal(due, stored.DueDate);
        Assert.Equal(7, stored.BatchId);
        Assert.Equal("beschreibung", stored.Description);
        Assert.Equal(new DateTime(2026, 1, 1), stored.LastChangeDate);
    }

    [Fact]
    public async Task Put_KeepsExistingCreatedDate()
    {
        var created = new DateTime(2025, 1, 1);
        var todo = new TodoItem { Title = "t", CreatedDate = created };
        await SeedAsync(todo);

        await _client.PutAsJsonAsync($"/api/todos/{todo.Id}",
            new { Title = "t2", LastChangeDate = new DateTime(2026, 2, 2) });

        Assert.Equal(created, (await LoadAsync(todo.Id))!.CreatedDate);
    }

    [Fact]
    public async Task Put_Quirk_MissingCreatedDate_IsSetFromLastChangeDate()
    {
        var todo = new TodoItem { Title = "alt-datensatz", CreatedDate = null };
        await SeedAsync(todo);
        var change = new DateTime(2026, 2, 2);

        await _client.PutAsJsonAsync($"/api/todos/{todo.Id}", new { Title = "t", LastChangeDate = change });

        Assert.Equal(change, (await LoadAsync(todo.Id))!.CreatedDate);
    }

    [Fact]
    public async Task Put_Quirk_ParentIdIsNotUpdated()
    {
        var todo = new TodoItem { Title = "kind", ParentId = 5 };
        await SeedAsync(todo);

        await _client.PutAsJsonAsync($"/api/todos/{todo.Id}", new { Title = "kind", ParentId = 99 });

        Assert.Equal(5, (await LoadAsync(todo.Id))!.ParentId);
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/todos/9999", new { Title = "x" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── DELETE /api/todos/{id} ────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesTodo_Returns204()
    {
        var todo = new TodoItem { Title = "weg" };
        await SeedAsync(todo);

        var response = await _client.DeleteAsync($"/api/todos/{todo.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await LoadAsync(todo.Id));
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/todos/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Quirk_ChildrenOfDeletedParentStayOrphaned()
    {
        var parent = new TodoItem { Title = "eltern" };
        await SeedAsync(parent);
        var child = new TodoItem { Title = "kind", ParentId = parent.Id };
        await SeedAsync(child);

        await _client.DeleteAsync($"/api/todos/{parent.Id}");

        var orphan = await LoadAsync(child.Id);
        Assert.NotNull(orphan);
        Assert.Equal(parent.Id, orphan.ParentId);
    }

    // ─── Hilfen ────────────────────────────────────────────────────────

    /// <summary>
    /// Die API schreibt deutsche Ortszeit (UTC+1/+2) ohne Zeitzone in die DB.
    /// Toleranzfenster statt exaktem Wert, damit der Test nicht flackert.
    /// </summary>
    internal static void AssertIsRecentGermanLocalTime(DateTime? value)
    {
        Assert.NotNull(value);
        var utcNow = DateTime.UtcNow;
        Assert.InRange(value.Value, utcNow.AddMinutes(-1), utcNow.AddHours(3));
    }
}
