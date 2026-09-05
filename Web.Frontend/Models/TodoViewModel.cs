namespace Web.Frontend.Models;

public class TodoViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; } // NEU
    public string? Notes { get; set; } // NEU
    public string Priority { get; set; } = "Mittel"; // NEU
}

