namespace Web.Frontend.Models;

public class BewerbungViewModel
{
    public int Id { get; set; }
    
    public string JobTitle { get; set; } = string.Empty; // Pflichtfeld

    public string Unternehmen { get; set; } = string.Empty; // Pflichtfeld

    public bool IsFreelance { get; set; } = false;

    public string? Notes { get; set; }

    public bool IsCompleted { get; set; }

    public string Priority { get; set; } = "Normal"; // z.B. Normal, High-Match, Low Match, Interessant
        
    public int BatchId { get; set; }  // Pflichtfeld, laufende Nummer für die Sortierung

    public string? Description { get; set; } 

    public DateTime? DueDate { get; set; } // Ablaufdatum / Bewerbungsfrist

    public string Status { get; set; } = "Offen"; // z.B. Offen, Eingereicht, Gespräch, Zusage, Absage

    public string? Anschreiben { get; set; } //Anschreiben Text

    public string? Lebenslauf { get; set; } // Lebenslauf (Pfad)

    public DateTime? BewerbungsDatum { get; set; } = default; // Kein Pflichtfeld!

    public DateTime? VorstellungsTermin { get; set; } // kein Pflichfeld!

    public string? Verbleib { get; set; } // kein Pflichfeld!

    public string? Kontakt { get; set; } // Kontakt Person oder Daten

    public DateTime? CreatedDate { get; set; }

    public DateTime? LastChangeDate { get; set; }
}