using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Web.Frontend.Models;

namespace Web.Frontend.Controllers;

public class ProductController : Controller
{
    private readonly HttpClient _httpClient;

    public ProductController(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(config["BackendUrl"] ?? "http://localhost:5001");
    }

    // GANZ WICHTIG: Nur DIESE EINE Index-Methode darf im Controller existieren!
    // GET: / (To-Do-Liste anzeigen mit optionalen Such- und Edit-Parametern)
    public async Task<IActionResult> Index(int? editId, string? search, string? priorityFilter)
    {
        ViewData["EditId"] = editId;
        ViewData["CurrentSearch"] = search;
        ViewData["CurrentPriorityFilter"] = priorityFilter;

        var url = $"/api/todos?search={Uri.EscapeDataString(search ?? "")}&priority={Uri.EscapeDataString(priorityFilter ?? "")}";
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return View(new List<TodoViewModel>());

        var content = await response.Content.ReadAsStringAsync();
        var allTodos = JsonSerializer.Deserialize<List<TodoViewModel>>(content, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<TodoViewModel>();

        // ─── BAUM-STRUKTUR IM CODE AUFBAUEN ─────────────────────────────────────
        var lookup = allTodos.ToDictionary(t => t.Id);
        var rootNodes = new List<TodoViewModel>();

        foreach (var todo in allTodos)
        {
            if (todo.ParentId.HasValue && lookup.ContainsKey(todo.ParentId.Value))
            {
                // Wenn es eine ParentId hat, fügen wir es der übergeordneten Aufgabe hinzu
                lookup[todo.ParentId.Value].SubTodos.Add(todo);
            }
            else
            {
                // Ansonsten ist es eine Hauptaufgabe
                rootNodes.Add(todo);
            }
        }

        return View(rootNodes); // Wir übergeben nur die Hauptaufgaben an die View
    }

    // NEU: Erlaubt das Erstellen von Unteraufgaben via GET-Link (behebt den 405-Fehler)
    [HttpGet]
    public async Task<IActionResult> Create(string title, DateTime? dueDate, string? notes, string priority, int? parentId)
    {
        // Wir rufen intern einfach die bestehende POST-Logik auf, um Code-Duplikate zu vermeiden
        return await CreatePostInternal(title, dueDate, notes, priority, parentId);
    }

    // Damit wir keinen doppelten Code haben, benennen wir die alte POST-Methode intern um:
    [HttpPost]
    public async Task<IActionResult> Create(string title, DateTime? dueDate, string? notes, string priority)
    {
        return await CreatePostInternal(title, dueDate, notes, priority, null);
    }

    // Hilfsmethode, die von beiden Endpunkten genutzt wird
    private async Task<IActionResult> CreatePostInternal(string title, DateTime? dueDate, string? notes, string priority, int? parentId)
    {
        var newTodo = new { Title = title, IsCompleted = false, DueDate = dueDate, Notes = notes, Priority = priority, ParentId = parentId };
        var content = new StringContent(JsonSerializer.Serialize(newTodo), Encoding.UTF8, "application/json");

        await _httpClient.PostAsync("/api/todos", content);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Product/Edit (Aufgabe aktualisieren)
    [HttpPost]
    public async Task<IActionResult> Edit(int id, string title, DateTime? dueDate, string? notes, string priority, bool isCompleted)
    {
        var updatedTodo = new { Id = id, Title = title, IsCompleted = isCompleted, DueDate = dueDate, Notes = notes, Priority = priority };
        var content = new StringContent(JsonSerializer.Serialize(updatedTodo), Encoding.UTF8, "application/json");

        await _httpClient.PutAsync($"/api/todos/{id}", content);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Product/Toggle (Status Erledigt/Offen umschalten)
    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        await _httpClient.PutAsync($"/api/todos/{id}/toggle", null);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Product/Delete (Aufgabe löschen)
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await _httpClient.DeleteAsync($"/api/todos/{id}");
        return RedirectToAction(nameof(Index));
    }

    private string? cookiesOrParam(string? val) => val; // Hilfsfunktion für sauberes Routing
}

