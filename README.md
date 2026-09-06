# :whale: Todo List (.NET 8 / PostgreSQL / Docker)

Ein kleines Todo-Verwaltungssystem.
mit moderner, verteilter **cloud-native Microservice-Architektur**. 
Praxisnahes Pilotprojekt demonstriert Best Practices in den Bereichen **C# (ASP.NET Core)**, **Docker-Containerisierung**, **asynchrone REST-Kommunikation** und **CI/CD**.

---

## 🚀 Quickstart (Docker erleben)

* Docker einrichten (Voraussetzung)

```text
Richten Sie sich hierbei nach der exakten Version Ihres Betriebssystems, 
z.B. für Linux Mint 21.2 Victoria die offizielle Docker Community Edition (Docker CE) 
aus den Repositories von Docker (Details siehe 💻 unten).
```

* Ansonsten muss nichts installiert werden. Im Terminal einfach mit folgenden Dreizeiler die vollständige Microservice-Architektur inkl. SQL-Datenbank starten:

```bash
git clone https://github.com/mklehner/TodoList.git
cd TodoList
sudo docker compose up
```

**Jetzt im Browser öffnen:**
* 🖥️ **Frontend (Todo UI):** [http://localhost:5000](http://localhost:5000) – *mit augenschonenden Dark Mode.*
* ![Swagger](https://shields.io) 🔍 **Backend (Swagger-Dokumentation):** [http://localhost:5001/swagger](http://localhost:5001/swagger) – *Testen Sie die REST-API live mit Swagger.*

---

## 🕸️ Architektur-Überblick

Das System ist in lose gekoppelte, funktionale Einheiten unterteilt und läuft vollständig isoliert innerhalb eines Docker-Netzwerks:

```text
┌─────────────────────────┐
│  Web.Frontend (MVC)     │  <-- Port 5000 (Dark Mode Theme)
└───────────┬─────────────┘
            │
            │ HTTP REST (JSON)
            ▼
┌─────────────────────────┐
│   Catalog.API (Backend) │  <-- Port 5001 (Minimal APIs / Swagger)
└───────────┬─────────────┘
            │
            │ EF Core (PostgreSQL Driver)
            ▼
┌─────────────────────────┐
│   PostgreSQL (Database) │  <-- Port 5432 (Isoliertes Volume)
└─────────────────────────┘
```

1. **`Web.Frontend` (ASP.NET Core MVC):** Die Benutzeroberfläche rendert serverseitig und kommuniziert asynchron via `HttpClient` mit dem API-Backend. Es verfügt über Inline-Editierung und Echtzeit-Validierung überfälliger Aufgaben.
2. **`Catalog.API` (ASP.NET Core Web API):** Ein schlanker, hochperformanter Daten-Service, der auf modernsten **Minimal APIs** basiert. Er verarbeitet die Geschäftslogik und stellt die Endpunkte bereit.
3. **`PostgreSQL` (Relational SQL DB):** Ein robuster Datenbank-Container. Die Tabellenstrukturen werden beim App-Start über Entity Framework Core automatisch und sicher initialisiert (`EnsureCreated`).

---

## ✨ Key Features & Implementierungen

*   **Inline-Bearbeitung & Priorisierung:** Aufgaben können direkt in der Listenansicht editiert werden (Titel, Notiz, Ablaufdatum). Ein dreistufiges Priorisierungssystem (`Hoch`, `Mittel`, `Niedrig`) sortiert wichtige Aufgaben automatisch nach oben.
*   **Visuelle Dringlichkeits-Warnung:** Aufgaben, deren Fälligkeitsdatum in der Vergangenheit liegt, werden im Frontend über dynamische CSS-Klassen automatisch rot hervorgehoben.
*   **Dynamische Server-seitige Filterung:** Suchbegriffe und Prioritätsfilter werden von der UI über Query-Parameter bis in die PostgreSQL-Datenbank durchgereicht, um maximale Performance bei großen Datenmengen zu garantieren.
*   **Interaktive API-Dokumentation:** Vollständige Integration von **Swagger / OpenAPI** im Backend zur Live-Einsicht und zum direkten Testen aller REST-Endpunkte unter `http://localhost:5001/swagger`.

---

## ⚙️ Tech Stack & Werkzeuge

*   **Frameworks:** .NET 8.0 (LTS) – ASP.NET Core MVC & Web API
*   **ORM / Datenbank:** Entity Framework Core mit PostgreSQL-Provider (`Npgsql`)
*   **Containerisierung:** Docker & Docker Compose
*   **Testing:** xUnit & Moq (Umlaut- und asynchronitätsoptimierte Test-Suiten)
*   **CI/CD:** GitHub Actions (Automatisierte Linux-Builds und Test-Validierung bei jedem Commit)

---

## 💻 Lokale Inbetriebnahme (Getting Started)

Dank vollständiger Docker-Kapselung müssen keine SDKs oder Datenbanken auf Ihrem Host-System installiert sein.

### Voraussetzungen
*   Installiertes **Docker** und **Docker Compose**
*   Ein Terminal mit Root- oder Docker-Rechten
*   Richten Sie sich beim einrichten von Docker nach der exakten Version Ihres Betriebssystems, 
    (z.B. für Linux Mint 21.2 Victoria die offizielle Docker Community Edition (Docker CE) aus den Docker Repositories)
*   Anschließend Neuanmeldung (oder System Neustart)
*   Prüfen Sie nach der Neuanmeldung mit folgendem Befehl, ob Docker funktioniert: 
```bash
'docker run hello-world'
```

### 1. Repository klonen & Verzeichnis wechseln
```bash
git clone https://github.com/mklehner/TodoList.git
cd TodoList
```

### 2. System via Docker Compose starten
Führen Sie im Hauptverzeichnis folgenden Befehl aus, um die Infrastruktur hochzufahren:
```bash
sudo docker compose up
```

### 3. Applikation aufrufen
Sobald die Container einsatzbereit sind, können die Dienste über folgende URLs auf dem lokalen Rechner erreicht werden:
*   **Frontend (To-Do UI):** [http://localhost:5000](http://localhost:5000)
*   **Backend (REST-API):** [http://localhost:5001](http://localhost:5001)
*   **API-Dokumentation (Swagger):** [http://localhost:5001/swagger](http://localhost:5001/swagger)

---

## 🧪 Qualitätssicherung (Unit-Testing)

Das Projekt enthält automatisierte Unit-Tests für den MVC-Controller, um den HTTP-Verkehr asynchron und isoliert zu validieren.

Wechseln Sie in das Testverzeichnis, um die Tests lokal auszuführen:
```bash
cd Web.Frontend.Tests
dotnet test
```

### CI/CD Pipeline
Bei jedem `git push` auf den Haupt-Branch triggert **GitHub Actions** automatisch den Workflow `.github/workflows/dotnet-ci.yml`. Dieser führt auf einem frischen Ubuntu-Server folgende Schritte aus:
1. NuGet-Abhängigkeiten wiederherstellen (`dotnet restore`)
2. Kompilierung im Release-Modus (`dotnet build`)
3. Ausführung der vollständigen Test-Suite (`dotnet test`)

---

## 📂 Projektstruktur

```text
├── .github/workflows/      # CI/CD Pipeline-Konfiguration (GitHub Actions)
├── Catalog.API/            # C# REST-API Backend (Geschäftslogik & DB-Kontext)
├── Web.Frontend/           # C# ASP.NET Core MVC Frontend (Views, Controller, Styles)
├── Web.Frontend.Tests/     # xUnit & Moq Testprojekt
└── docker-compose.yml      # Zentrale Multi-Container-Orchestrierung
```

