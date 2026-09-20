using System.ComponentModel.DataAnnotations;

namespace Catalog.API.Models
{
    public class Bewerbung
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string JobTitle { get; set; } = string.Empty; // Pflichtfeld

        [Required]
        [StringLength(100)]
        public string Unternehmen { get; set; } = string.Empty; // Pflichtfeld

        public bool IsFreelance { get; set; }

        public string? Notes { get; set; } // Notizen und Anmerkungen

        public bool IsCompleted { get; set; } // wird beim Klicken auf den blauen Haken getoggelt

        public string Priority { get; set; } = "Normal"; // z.B. Normal, High-Match, Low Match, Interessant, Exotic
        
        [Required]
        public int BatchId { get; set; } // Pflichtfeld, laufende Nummer für die Sortierung

        public string? Description { get; set; } //Stellenbeschreibung

        public DateTime? DueDate { get; set; } // Ablaufdatum / Bewerbungsfrist

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Offen"; // z.B. Offen, Eingereicht, Gespräch, Zusage, Absage

        public string? Anschreiben { get; set; } //Anschreiben Text

        public string? Lebenslauf { get; set; } //Name der CV-Datei

        public DateTime? BewerbungsDatum { get; set; } // kein Pflichfeld!

        public string? Kontakt { get; set; }  // Kontakt Person oder Daten

        public DateTime? CreatedDate { get; set; }

        public DateTime? LastChangeDate { get; set; }
    }
}

    
