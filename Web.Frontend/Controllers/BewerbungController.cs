using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Web.Frontend.Models;

namespace Web.Frontend.Controllers
{
    public class BewerbungController : Controller
    {
        private readonly HttpClient _httpClient;

        // Per Dependency Injection übergeben wir den HttpClient
        // public BewerbungydController(IHttpClientFactory httpClientFactory)
        // {
        //     // Erstellt den Client (Konfiguration erfolgt gleich in Program.cs)
        //     _httpClient = httpClientFactory.CreateClient("CatalogAPI");
        // }
        
        public BewerbungController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri(config["BackendUrl"] ?? "http://localhost:5001");
        }

        // GET: /Bewerbungs
        public async Task<IActionResult> Index(int editId)
        {
            ViewData["Title"] = "Bewerbungen Übersicht";
            ViewData["EditId"] = editId;

            try
            {
                // Ruft den Minimal-API-Endpunkt ab und mappt das JSON automatisch in die Liste
                var bewerbungen = await _httpClient.GetFromJsonAsync<List<BewerbungViewModel>>("api/bewerbung");
                
                return View(bewerbungen ?? new List<BewerbungViewModel>());
            }
            catch (HttpRequestException)
            {
                // Fehlerbehandlung falls die API offline ist
                ModelState.AddModelError(string.Empty, "Die API ist derzeit nicht erreichbar.");
                return View(new List<BewerbungViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
                string jobTitle, 
                DateTime? dueDate, 
                string priority, 
                string unternehmen,
                bool isFreelance,
                string? notes,
                bool isCompleted,
                int batchId,
                string? description,
                string? anschreiben,
                string status,
                string? lebenslauf,
                DateTime bewerbungsDatum,
                string? kontakt, 
                DateTime? createdDate,
                DateTime? lastChangeDate
            )
        {   

            // Holt die aktuelle deutsche Uhrzeit (inkl. automatischer Sommer-/Winterzeit)
            var zoneDe = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zoneDe);

            // Achtung - createdDate wird hier einmal angelegt und später nicht mehr geändert
            // createdDate ist eine unsichtbare Variable die beim Update des Datensatzes mitschwimmt
            // Daten die man nicht aus dem Input der HTML-Form bezieht, kommen hier u.U. als Null-Werte herein,
            // wenn sie im ASP nicht explizit mit <hidden> als Teil des Dokumentes mitgeführt werden.
            // man kann entweder hier bei der Neuanlage den Wert der Variablen einspeisen
            // oder man erledigt es in Program.cs

            createdDate = localNow; //DateTime.Now;

            //throw new Exception($"Create: Das Feld 'createdDate' im Controller ist: {createdDate.HasValue ? createdDate : ""}!");

            // Mappt die Formulardaten in dein API-Datenobjekt bzw. ViewModel
            // bzgl. der MappingVariablen camelCase beachten
            // P.= Pflichtfelder 
            var neueBewerbung = new BewerbungViewModel
            {
                JobTitle = jobTitle,        // P. Der 'jobTitle'-Wert aus dem Input
                Unternehmen = unternehmen,  // P. muss eingegeben werden
                IsFreelance = isFreelance,  // P. trägt automatisch einen Wert
                Notes = notes,      
                IsCompleted = isCompleted,  // P. trägt automatisch einen Wert
                Priority = priority,        // P. trägt automatisch einen Wert
                BatchId = batchId,          // P. muss eingegeben werden
                Description = description,
                DueDate = dueDate,  
                Status = status,            // P. trägt automatisch einen Wert
                Anschreiben = anschreiben,
                Lebenslauf = lebenslauf,
                BewerbungsDatum = bewerbungsDatum,
                Kontakt = kontakt,
                CreatedDate = createdDate
                //LastChangeDate = lastChangeDate   // wird hier nicht angelegt
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/bewerbung", neueBewerbung);
                //if (response.IsSuccessStatusCode)
                    //return RedirectToAction(nameof(Index));
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "Verbindungsfehler zur API.");
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Product/Edit (Aufgabe aktualisieren)
        // public async Task<IActionResult> Edit(int id, BewerbungViewModel bwModel)
        // statt alle Parameter einzeln in die Edit-Methode zu schreiben (int id, string jobTitle, ...), 
        // kannst du auch direkt dein BewerbungViewModel als Parameter nutzen. ASP.NET Core mappt das automatisch, 
        // wenn die Namen übereinstimmen:
        [HttpPost] 
        public async Task<IActionResult> Edit(
            int id, 
            string jobTitle,    //P.
            string unternehmen, //P.
            bool isFreelance,   //P.
            string? notes, 
            bool isCompleted,   //P.
            string priority,    //P.
            int batchId,        //P.
            string? description, 
            DateTime? dueDate, 
            string status,      //P.
            string? anschreiben, 
            string? lebenslauf, 
            DateTime? bewerbungsDatum, 
            string? kontakt, 
            DateTime? createdDate, 
            DateTime? lastChangeDate)
        {
            // Automatische Anpassungen/Korrekturen: entweder hier oder in Program.cs

            // Holt die aktuelle deutsche Uhrzeit (inkl. automatischer Sommer-/Winterzeit)
            var zoneDe = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zoneDe);

            lastChangeDate = localNow; //DateTime.Now;

            // Achtung - createdDate wird einmal beim Create angelegt und später nicht mehr geändert
            // createdDate ist eine unsichtbare Variable die beim Update des Datensatzes mitschwimmt
            // Daten die man nicht aus dem Input der HTML-Form bezieht, kommen hier u.U. als Null-Werte herein,
            // wenn sie im ASP nicht explizit mit <hidden> als Teil des Dokumentes mitgeführt werden.
            // Solche Daten sollen nicht unbeabsichtigt überschreiben werden!
            // man erledigt es daher am besten in Program.cs weil der Dbcontext den gespeicherten Wert beinhaltet
            
            // Fehler: createdDate ist hier immer Null wenn es im ASP nicht ausgetauscht wird
            //##-- if (!createdDate.HasValue)
            //##--     createdDate = localNow;

            // wenn man das Datum zurückssetzen will, damit danach nicht "01/01/0001" erscheint
            if (bewerbungsDatum == DateTime.MinValue)
                bewerbungsDatum = default;   //null;

            // last chance Validierung auf tiefer Ebene (wird in der Oberfläche required oder min verhindert)
            if (string.IsNullOrEmpty(jobTitle))             
                 throw new Exception("Fehler: Das Feld 'jobTitle' ist im Controller null oder leer!");
            
            else if (string.IsNullOrEmpty(unternehmen))            
                throw new Exception("Fehler: Das Feld 'unternehmen' ist im Controller null oder leer!");
            
            else if (batchId < 0)            
                throw new Exception("Fehler: Das Feld 'batchId' ist im Controller < 0 !");

            // alle anderen Pflichtfelder (Freelance, IsCompleted, Priority und Status)
            // beinhalten über das Control automatisch Werte, auch bei Nicht-Auswahl

            // Debug/Test:
            //##--throw new Exception($"Fehler: Das Feld 'anschreiben' im Controller ist: {anschreiben}!");

            // die Exchange-Variablen sollten exakt gleich geschrieben werden wie in der HTML Form
            // also camelCase (kleiner Anfangs-Buchstabe)
            var updBewerbung = new {
                Id = id,
                JobTitle = jobTitle,
                Unternehmen = unternehmen,
                IsFreelance = isFreelance,
                Notes = notes,
                IsCompleted = isCompleted,
                Priority = priority,
                BatchId = batchId,
                Description = description,
                DueDate = dueDate,
                Status = status,
                Anschreiben = anschreiben,
                Lebenslauf = lebenslauf,
                BewerbungsDatum = bewerbungsDatum,
                Kontakt = kontakt,
                //CreatedDate = createdDate,    // kann nicht mehr geändert werden
                LastChangeDate = lastChangeDate
            };

            var content = new StringContent(JsonSerializer.Serialize(updBewerbung), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PutAsync($"/api/bewerbung/{id}", content);
                //if (response.IsSuccessStatusCode)
                //    return RedirectToAction(nameof(Index));
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "Verbindungsfehler zur API (HttpPost Edit).");
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Bewebung/Toggle (IsCompleted umschalten)
        [HttpPost]
        public async Task<IActionResult> Toggle(int id)
        {
            //debug: komm ich hier vorbei?            
            //throw new Exception($"Der Button funktioniert! ID ist: {id}");

            var response = await _httpClient.PutAsync($"/api/bewerbung/{id}/toggle", null);

            // Sicherstellen, dass die API fertig geantwortet hat
            if (response.IsSuccessStatusCode)
            {
                // Optional: Kurzes Auslesen des Contents zwingt zum Warten auf den Stream
                var content = await response.Content.ReadAsStringAsync(); 
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Bewerbung/Delete (löschen)
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _httpClient.DeleteAsync($"/api/bewerbung/{id}");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBatchId(int id, int batchId)
        {
            // 1. Bewerbung aus der Datenbank laden anhand der 'id'
            // 2. bewerbung.BatchId = batchId;
            // 3. Änderungen speichern (SaveChangesAsync)
            
            //debug: komm ich hier vorbei?            
            //throw new Exception($"Es funktioniert! Id: {id}, batchId: {batchId}");

            var response = await _httpClient.PutAsync($"/api/bewerbung/{id}/batchId/{batchId}", null);

            // Sicherstellen, dass die API fertig geantwortet hat
            if (response.IsSuccessStatusCode)
            {
                // Optional: Kurzes Auslesen des Contents zwingt zum Warten auf den Stream
                var content = await response.Content.ReadAsStringAsync(); 
            }
            else
                throw new Exception($"Failure Id: {id}, batchId: {batchId}. Status: {response.StatusCode}");

            // Zurück zur Liste springen (inklusive Scroll-Anker zum bearbeiteten Element)
            return RedirectToAction(nameof(Index), new { fragment = $"bewerbung-{id}" });
        }
    }
}
