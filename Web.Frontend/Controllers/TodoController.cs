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

        // Dynamischen API-Pfad mit Query-Parametern für das Backend zusammenbauen
        var url = $"/api/todos?search={Uri.EscapeDataString(search ?? "")}&priority={Uri.EscapeDataString(priorityFilter ?? "")}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return View(new List<TodoViewModel>());

        var content = await response.Content.ReadAsStringAsync();
        var todos = JsonSerializer.Deserialize<List<TodoViewModel>>(content, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return View(todos);
    }

    // POST: /Product/Create (Neues To-Do hinzufügen)
    [HttpPost]
    public async Task<IActionResult> Create(string title, DateTime? dueDate, string? notes, string priority)
    {
        var newTodo = new { Title = title, IsCompleted = false, DueDate = dueDate, Notes = notes, Priority = priority };
        var content = new StringContent(JsonSerializer.Serialize(newTodo), Encoding.UTF8, "application/json");

        await _httpClient.PostAsync("/api/todos", content);
        return RedirectToAction(nameof(Index), new { search = cookiesOrParam(null), priorityFilter = cookiesOrParam(null) });
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

