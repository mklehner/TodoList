namespace Catalog.API.Models;

public class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    // Neues Feld für das Fälligkeitsdatum (Nullable, falls kein Datum gesetzt ist)
    public DateTime? DueDate { get; set; } 
    // NEU: Ein optionales Feld für zusätzliche Notizen
    public string? Notes { get; set; } 
    // NEU: Priorität als String (z.B. "Hoch", "Mittel", "Niedrig")
    public string Priority { get; set; } = "Mittel";

    // NEU: Selbstverweis für die Baumstruktur (Null = Hauptaufgabe)
    public int? ParentId { get; set; }

    // 🔴 Das neue Feld für PostgreSQL
    public int BatchId { get; set; } 

    // 🔴 Feld für Details / Beschreibungstext
    public string? Description { get; set; } 

    // 🔴 Datum erstellt
    public DateTime? CreatedDate{ get; set; } 

    // 🔴 Datum zuletzt geändert
    public DateTime? LastChangeDate{ get; set; } 
}

