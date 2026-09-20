using System.Net;
using System.Net.Http.Json;
using Catalog.API.Models;

namespace Catalog.API.Tests;

/// <summary>
/// Charakterisierungstests für /api/bewerbung und die Startseite.
/// Tests mit "Quirk" im Namen dokumentieren bekannte Schwächen (siehe Refactoring-Plan).
/// </summary>
public class BewerbungEndpointsTests : IDisposable
{
    private readonly CatalogApiFactory _factory = new();
    private readonly HttpClient _client;

    public BewerbungEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task SeedAsync(params Bewerbung[] items) =>
        _factory.WithDbAsync(async db =>
        {
            db.Bewerbungen.AddRange(items);
            await db.SaveChangesAsync();
        });

    private Task<Bewerbung?> LoadAsync(int id) =>
        _factory.WithDbAsync(db => db.Bewerbungen.FindAsync(id).AsTask());

    private static Bewerbung Sample(string job = "Entwickler", string firma = "ACME") =>
        new() { JobTitle = job, Unternehmen = firma, BatchId = 1 };

    // ─── GET ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ReturnsAllBewerbungen()
    {
        await SeedAsync(Sample("A"), Sample("B"));

        var items = await _client.GetFromJsonAsync<List<Bewerbung>>("/api/bewerbung");

        Assert.Equal(2, items!.Count);
    }

    // ─── POST ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_CreatesBewerbung_Returns201WithLocation_AndDefaults()
    {
        var response = await _client.PostAsJsonAsync("/api/bewerbung",
            new { JobTitle = "Dev", Unternehmen = "ACME", BatchId = 3 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Bewerbung>();
        Assert.Equal($"/api/bewerbung/{created!.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal("Offen", created.Status);
        Assert.Equal("Normal", created.Priority);
        Assert.False(created.IsCompleted);
        Assert.False(created.IsFreelance);
        Assert.Null(created.BewerbungsDatum);
    }

    [Theory]
    [InlineData("", "ACME")]
    [InlineData("  ", "ACME")]
    [InlineData("Dev", "")]
    [InlineData("Dev", "   ")]
    public async Task Post_EmptyJobTitleOrUnternehmen_ReturnsBadRequest(string job, string firma)
    {
        var response = await _client.PostAsJsonAsync("/api/bewerbung",
            new { JobTitle = job, Unternehmen = firma, BatchId = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await _factory.WithDbAsync(db => Task.FromResult(db.Bewerbungen.ToList())));
    }

    [Fact]
    public async Task Post_NegativeBatchId_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/bewerbung",
            new { JobTitle = "Dev", Unternehmen = "ACME", BatchId = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Quirk_DataAnnotationsAreNotEnforced_EmptyStatusIsAccepted()
    {
        // [Required] auf Status wird von Minimal APIs nicht automatisch geprüft
        var response = await _client.PostAsJsonAsync("/api/bewerbung",
            new { JobTitle = "Dev", Unternehmen = "ACME", BatchId = 1, Status = "" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_Quirk_ClientControlsCreatedDate()
    {
        var date = new DateTime(2020, 1, 2, 3, 4, 5);

        var response = await _client.PostAsJsonAsync("/api/bewerbung",
            new { JobTitle = "Dev", Unternehmen = "ACME", BatchId = 1, CreatedDate = date });
        var created = await response.Content.ReadFromJsonAsync<Bewerbung>();

        Assert.Equal(date, created!.CreatedDate);
    }

    // ─── PUT ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Put_UpdatesAllEditableFields()
    {
        var item = Sample();
        await SeedAsync(item);
        var due = new DateTime(2030, 1, 1);
        var applied = new DateTime(2030, 2, 2);

        var response = await _client.PutAsJsonAsync($"/api/bewerbung/{item.Id}", new
        {
            JobTitle = "Senior Dev",
            Unternehmen = "Neu GmbH",
            IsFreelance = true,
            Notes = "n",
            Priority = "High-Match",
            DueDate = due,
            BatchId = 9,
            Description = "d",
            Status = "Gespräch",
            Anschreiben = "a",
            Lebenslauf = "cv.pdf",
            BewerbungsDatum = applied,
            Kontakt = "k",
            LastChangeDate = new DateTime(2026, 3, 3)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var s = (await LoadAsync(item.Id))!;
        Assert.Equal("Senior Dev", s.JobTitle);
        Assert.Equal("Neu GmbH", s.Unternehmen);
        Assert.True(s.IsFreelance);
        Assert.Equal("n", s.Notes);
        Assert.Equal("High-Match", s.Priority);
        Assert.Equal(due, s.DueDate);
        Assert.Equal(9, s.BatchId);
        Assert.Equal("d", s.Description);
        Assert.Equal("Gespräch", s.Status);
        Assert.Equal("a", s.Anschreiben);
        Assert.Equal("cv.pdf", s.Lebenslauf);
        Assert.Equal(applied, s.BewerbungsDatum);
        Assert.Equal("k", s.Kontakt);
        Assert.Equal(new DateTime(2026, 3, 3), s.LastChangeDate);
    }

    [Fact]
    public async Task Put_KeepsExistingCreatedDate()
    {
        var created = new DateTime(2025, 1, 1);
        var item = Sample();
        item.CreatedDate = created;
        await SeedAsync(item);

        await _client.PutAsJsonAsync($"/api/bewerbung/{item.Id}", new
        {
            JobTitle = "x", Unternehmen = "y", Status = "Offen", LastChangeDate = new DateTime(2026, 2, 2)
        });

        Assert.Equal(created, (await LoadAsync(item.Id))!.CreatedDate);
    }

    [Fact]
    public async Task Put_Quirk_MissingCreatedDate_IsSetFromLastChangeDate()
    {
        var item = Sample();
        await SeedAsync(item);
        var change = new DateTime(2026, 2, 2);

        await _client.PutAsJsonAsync($"/api/bewerbung/{item.Id}", new
        {
            JobTitle = "x", Unternehmen = "y", Status = "Offen", LastChangeDate = change
        });

        Assert.Equal(change, (await LoadAsync(item.Id))!.CreatedDate);
    }

    [Fact]
    public async Task Put_Quirk_NoValidation_EmptyJobTitleOverwritesData()
    {
        // Anders als POST prüft PUT keine Pflichtfelder (das macht nur der MVC-Controller per Exception)
        var item = Sample("Original");
        await SeedAsync(item);

        var response = await _client.PutAsJsonAsync($"/api/bewerbung/{item.Id}", new
        {
            JobTitle = "", Unternehmen = "", Status = "Offen"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", (await LoadAsync(item.Id))!.JobTitle);
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/bewerbung/9999",
            new { JobTitle = "x", Unternehmen = "y" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── Toggle / BatchId / Delete ─────────────────────────────────────

    [Fact]
    public async Task Toggle_FlipsIsCompleted_AndSetsLastChangeDate()
    {
        var item = Sample();
        await SeedAsync(item);

        var response = await _client.PutAsync($"/api/bewerbung/{item.Id}/toggle", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = (await LoadAsync(item.Id))!;
        Assert.True(stored.IsCompleted);
        TodoEndpointsTests.AssertIsRecentGermanLocalTime(stored.LastChangeDate);
    }

    [Fact]
    public async Task Toggle_UnknownId_Returns404()
    {
        var response = await _client.PutAsync("/api/bewerbung/9999/toggle", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBatchId_SetsBatchId_ButNotLastChangeDate()
    {
        var item = Sample();
        await SeedAsync(item);

        var response = await _client.PutAsync($"/api/bewerbung/{item.Id}/batchId/42", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = (await LoadAsync(item.Id))!;
        Assert.Equal(42, stored.BatchId);
        Assert.Null(stored.LastChangeDate);
    }

    [Fact]
    public async Task UpdateBatchId_UnknownId_Returns404()
    {
        var response = await _client.PutAsync("/api/bewerbung/9999/batchId/1", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesBewerbung_Returns204()
    {
        var item = Sample();
        await SeedAsync(item);

        var response = await _client.DeleteAsync($"/api/bewerbung/{item.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await LoadAsync(item.Id));
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/bewerbung/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── Startseite ────────────────────────────────────────────────────

    [Fact]
    public async Task Root_RedirectsToSwagger()
    {
        using var client = _factory.CreateClient(new()
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/swagger", response.Headers.Location!.OriginalString);
    }
}
