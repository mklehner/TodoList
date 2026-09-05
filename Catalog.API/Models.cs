namespace Catalog.API.Models;

public class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    // Neues Feld für das Fälligkeitsdatum (Nullable, falls kein Datum gesetzt ist)
    public DateTime? DueDate { get; set; } 
}

