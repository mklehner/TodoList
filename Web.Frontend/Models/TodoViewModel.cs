namespace Web.Frontend.Models;

public class TodoViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; } // NEU
    public string? Notes { get; set; } // NEU
    public string Priority { get; set; } = "Mittel"; // NEU
    public int? ParentId { get; set; } // NEU
    
    // 🔴 NEU: Die laufende Nummer für die Sortierung
    public int BatchId { get; set; }

    // 🔴 Das neue Feld für Description
    public string? Description { get; set; } 

    public DateTime? CreatedDate { get; set; }

    public DateTime? LastChangeDate { get; set; }

    // NEU: Hilfseigenschaft, um die Unteraufgaben im Speicher zu bündeln
    public List<TodoViewModel> SubTodos { get; set; } = new();
}

