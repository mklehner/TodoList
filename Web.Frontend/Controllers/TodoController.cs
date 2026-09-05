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

    // GET: / (To-Do-Liste anzeigen)
    public async Task<IActionResult> Index()
    {
        var response = await _httpClient.GetAsync("/api/todos");
        if (!response.IsSuccessStatusCode) return View(new List<TodoViewModel>());

        var content = await response.Content.ReadAsStringAsync();
        var todos = JsonSerializer.Deserialize<List<TodoViewModel>>(content, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return View(todos);
    }

    // POST: /Product/Create (Neues To-Do hinzufügen)
    [HttpPost]
    public async Task<IActionResult> Create(string title, DateTime? dueDate)
    {
        var newTodo = new { Title = title, IsCompleted = false, DueDate = dueDate };
        var content = new StringContent(JsonSerializer.Serialize(newTodo), Encoding.UTF8, "application/json");

        await _httpClient.PostAsync("/api/todos", content);
        return RedirectToAction(nameof(Index));
    }

    // POST: /Product/Toggle (Status ändern)
    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        await _httpClient.PutAsync($"/api/todos/{id}/toggle", null);
        return RedirectToAction(nameof(Index));
    }

    // NEU: POST: /Product/Delete (Aufgabe löschen)
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        await _httpClient.DeleteAsync($"/api/todos/{id}");
        return RedirectToAction(nameof(Index));
    }
}

