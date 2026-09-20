using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Web.Frontend.Controllers;
using Web.Frontend.Models;

namespace Web.Frontend.Tests;

/// <summary>
/// Charakterisierungstests für den BewerbungController (heutiges Verhalten).
/// Tests mit "Quirk" im Namen dokumentieren bekannte Schwächen (siehe Refactoring-Plan).
/// </summary>
public class BewerbungControllerTests
{
    private readonly RecordingHandler _handler = new();
    private readonly BewerbungController _controller;

    public BewerbungControllerTests()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
               .Returns(new HttpClient(_handler) { BaseAddress = new Uri("http://localhost:5001") });

        _controller = new BewerbungController(factory.Object, new Mock<IConfiguration>().Object);
    }

    // ─── Index ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ReturnsViewWithBewerbungen_AndPassesEditId()
    {
        _handler.Respond(HttpStatusCode.OK,
            """[{"id":1,"jobTitle":"Dev","unternehmen":"ACME","batchId":3,"status":"Offen"}]""");

        var result = await _controller.Index(editId: 5);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<BewerbungViewModel>>(view.Model).ToList();
        var item = Assert.Single(model);
        Assert.Equal("Dev", item.JobTitle);
        Assert.Equal("ACME", item.Unternehmen);
        Assert.Equal(3, item.BatchId);
        Assert.Equal(5, view.ViewData["EditId"]);
        Assert.Equal("/api/bewerbung", _handler.Single.Uri.AbsolutePath);
        Assert.Equal(HttpMethod.Get, _handler.Single.Method);
    }

    [Fact]
    public async Task Index_ApiUnreachable_ReturnsEmptyViewWithModelError()
    {
        _handler.Throw(new HttpRequestException("down"));

        var result = await _controller.Index(editId: 0);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<BewerbungViewModel>>(view.Model));
        Assert.False(_controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Index_ApiReturnsErrorStatus_ReturnsEmptyViewWithModelError()
    {
        _handler.Respond(HttpStatusCode.InternalServerError, "");

        var result = await _controller.Index(editId: 0);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<BewerbungViewModel>>(view.Model));
        Assert.False(_controller.ModelState.IsValid);
    }

    // ─── Create ────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PostsJsonToApi_SetsCreatedDate_AndRedirectsToIndex()
    {
        _handler.Respond(HttpStatusCode.Created, "{}");
        var before = DateTime.UtcNow;

        var result = await CreateSample();

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        var request = _handler.Single;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/bewerbung", request.Uri.AbsolutePath);

        var json = request.Json;
        Assert.Equal("Dev", json.GetProperty("jobTitle").GetString());
        Assert.Equal("ACME", json.GetProperty("unternehmen").GetString());
        Assert.True(json.GetProperty("isFreelance").GetBoolean());
        Assert.Equal("High-Match", json.GetProperty("priority").GetString());
        Assert.Equal(4, json.GetProperty("batchId").GetInt32());
        Assert.Equal("Offen", json.GetProperty("status").GetString());
        Assert.Equal("k@example.org", json.GetProperty("kontakt").GetString());

        // Create serialisiert camelCase (PostAsJsonAsync), Edit dagegen PascalCase (siehe Quirk-Test unten).
        // CreatedDate = deutsche Ortszeit (UTC+1/+2), LastChangeDate wird bei Create nicht gesendet
        Assert.InRange(json.GetProperty("createdDate").GetDateTime(), before.AddMinutes(-1), before.AddHours(3));
        Assert.Equal(JsonValueKind.Null, json.GetProperty("lastChangeDate").ValueKind);
    }

    [Fact]
    public async Task Quirk_CreateSendsCamelCase_EditSendsPascalCase()
    {
        // Harmlos, weil die API case-insensitiv deserialisiert – aber inkonsistent
        _handler.Respond(HttpStatusCode.OK, "{}");

        await CreateSample();
        await EditSample(id: 1);

        Assert.True(_handler.Requests[0].Json.TryGetProperty("jobTitle", out _));
        Assert.True(_handler.Requests[1].Json.TryGetProperty("JobTitle", out _));
    }

    [Fact]
    public async Task Create_Quirk_ApiErrorStatusIsIgnored_StillRedirectsToIndex()
    {
        _handler.Respond(HttpStatusCode.BadRequest, "Fehler");

        var result = await CreateSample();

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    [Fact]
    public async Task Create_Quirk_ApiUnreachable_StillRedirectsToIndex()
    {
        _handler.Throw(new HttpRequestException("down"));

        var result = await CreateSample();

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.False(_controller.ModelState.IsValid);
    }

    // ─── Edit ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_PutsJsonToApi_WithoutCreatedDate_AndWithFreshLastChangeDate()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");
        var before = DateTime.UtcNow;

        var result = await EditSample(id: 12);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        var request = _handler.Single;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/bewerbung/12", request.Uri.AbsolutePath);

        var json = request.Json;
        Assert.Equal(12, json.GetProperty("Id").GetInt32());
        Assert.Equal("Dev", json.GetProperty("JobTitle").GetString());
        Assert.Equal("Gespräch", json.GetProperty("Status").GetString());
        Assert.Equal("cv.pdf", json.GetProperty("Lebenslauf").GetString());
        Assert.False(json.TryGetProperty("CreatedDate", out _)); // wird nur von der API vergeben
        Assert.InRange(json.GetProperty("LastChangeDate").GetDateTime(), before.AddMinutes(-1), before.AddHours(3));
    }

    [Fact]
    public async Task Edit_DoesNotForwardClientSuppliedLastChangeDate()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");

        await EditSample(id: 1, lastChangeDate: new DateTime(2001, 1, 1));

        var sent = _handler.Single.Json.GetProperty("LastChangeDate").GetDateTime();
        Assert.True(sent.Year >= DateTime.UtcNow.Year);
    }

    [Fact]
    public async Task Edit_BewerbungsDatumMinValue_IsSentAsNull()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");

        await EditSample(id: 1, bewerbungsDatum: DateTime.MinValue);

        Assert.Equal(JsonValueKind.Null, _handler.Single.Json.GetProperty("BewerbungsDatum").ValueKind);
    }

    [Fact]
    public async Task Edit_BewerbungsDatumSet_IsForwarded()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");

        await EditSample(id: 1, bewerbungsDatum: new DateTime(2026, 4, 5));

        Assert.Equal(new DateTime(2026, 4, 5), _handler.Single.Json.GetProperty("BewerbungsDatum").GetDateTime());
    }

    [Theory]
    [InlineData("", "ACME", 1)]
    [InlineData(null, "ACME", 1)]
    [InlineData("Dev", "", 1)]
    [InlineData("Dev", null, 1)]
    [InlineData("Dev", "ACME", -1)]
    public async Task Edit_Quirk_InvalidInput_ThrowsExceptionInsteadOfValidationError(string? job, string? firma, int batchId)
    {
        await Assert.ThrowsAsync<Exception>(() =>
            _controller.Edit(1, job!, firma!, false, null, false, "Normal", batchId, null, null,
                "Offen", null, null, null, null, null, null));

        Assert.Empty(_handler.Requests); // es wird gar nichts an die API gesendet
    }

    [Fact]
    public async Task Edit_Quirk_ApiErrorStatusIsIgnored_StillRedirectsToIndex()
    {
        _handler.Respond(HttpStatusCode.NotFound, "");

        var result = await EditSample(id: 999);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
    }

    // ─── Toggle / Delete / UpdateBatchId ───────────────────────────────

    [Fact]
    public async Task Toggle_PutsToToggleEndpoint_AndRedirectsToIndex()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");

        var result = await _controller.Toggle(7);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(HttpMethod.Put, _handler.Single.Method);
        Assert.Equal("/api/bewerbung/7/toggle", _handler.Single.Uri.AbsolutePath);
    }

    [Fact]
    public async Task Delete_SendsDeleteToApi_AndRedirectsToIndex()
    {
        _handler.Respond(HttpStatusCode.NoContent, "");

        var result = await _controller.Delete(7);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(HttpMethod.Delete, _handler.Single.Method);
        Assert.Equal("/api/bewerbung/7", _handler.Single.Uri.AbsolutePath);
    }

    [Fact]
    public async Task UpdateBatchId_PutsToBatchIdEndpoint_AndRedirectsToIndex()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");

        var result = await _controller.UpdateBatchId(7, 42);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(HttpMethod.Put, _handler.Single.Method);
        Assert.Equal("/api/bewerbung/7/batchId/42", _handler.Single.Uri.AbsolutePath);
    }

    [Fact]
    public async Task UpdateBatchId_Quirk_FragmentIsPassedAsRouteValue_NotAsUrlFragment()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");

        var result = await _controller.UpdateBatchId(7, 42);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(redirect.Fragment);                                   // müsste "bewerbung-7" sein
        Assert.Equal("bewerbung-7", redirect.RouteValues!["fragment"]);   // landet als ?fragment=... in der URL
    }

    [Fact]
    public async Task UpdateBatchId_Quirk_ApiErrorStatus_ThrowsException()
    {
        _handler.Respond(HttpStatusCode.NotFound, "");

        var ex = await Assert.ThrowsAsync<Exception>(() => _controller.UpdateBatchId(7, 42));

        Assert.Contains("NotFound", ex.Message);
    }

    // ─── UpdateStatus (wird per fetch aus site.js aufgerufen) ──────────

    [Fact]
    public async Task UpdateStatus_PutsToStatusEndpoint_AndRedirectsToIndex()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");

        var result = await _controller.UpdateStatus(7, "Eingereicht");

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(HttpMethod.Put, _handler.Single.Method);
        Assert.Equal("/api/bewerbung/7/status/Eingereicht", _handler.Single.Uri.AbsolutePath);
        Assert.Null(_handler.Single.Body);
    }

    [Fact]
    public async Task UpdateStatus_UmlautsArePercentEncodedInPath()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");

        await _controller.UpdateStatus(7, "Gespräch");

        Assert.Equal("/api/bewerbung/7/status/Gespr%C3%A4ch", _handler.Single.Uri.AbsolutePath);
    }

    [Fact]
    public async Task UpdateStatus_Quirk_StatusIsNotEscaped_SlashChangesTheRoute()
    {
        // Der Status wird ungeprüft in den Pfad interpoliert (kein Uri.EscapeDataString)
        _handler.Respond(HttpStatusCode.OK, "{}");

        await _controller.UpdateStatus(7, "a/b");

        Assert.Equal("/api/bewerbung/7/status/a/b", _handler.Single.Uri.AbsolutePath);
    }

    [Fact]
    public async Task UpdateStatus_Quirk_RedirectsWithFragmentAsRouteValue()
    {
        // Für den fetch-Aufruf aus site.js folgt der Browser diesem Redirect und lädt die komplette Index-Seite,
        // obwohl nur der Statuscode (response.ok) ausgewertet wird
        _handler.Respond(HttpStatusCode.OK, "{}");

        var result = await _controller.UpdateStatus(7, "Offen");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(redirect.Fragment);
        Assert.Equal("status-7", redirect.RouteValues!["fragment"]);
    }

    [Fact]
    public async Task UpdateStatus_Quirk_ApiErrorStatus_ThrowsException_WithBatchIdInMessage()
    {
        // Copy-Paste aus UpdateBatchId: die Fehlermeldung nennt "batchId" statt "status"
        _handler.Respond(HttpStatusCode.NotFound, "");

        var ex = await Assert.ThrowsAsync<Exception>(() => _controller.UpdateStatus(7, "Offen"));

        Assert.Contains("NotFound", ex.Message);
        Assert.Contains("batchId: Offen", ex.Message);
    }

    // ─── Hilfen ────────────────────────────────────────────────────────

    private Task<IActionResult> CreateSample() =>
        _controller.Create(
            jobTitle: "Dev", dueDate: null, priority: "High-Match", unternehmen: "ACME",
            isFreelance: true, notes: null, isCompleted: false, batchId: 4, description: null,
            anschreiben: null, status: "Offen", lebenslauf: null,
            bewerbungsDatum: new DateTime(2026, 1, 1), kontakt: "k@example.org",
            createdDate: null, lastChangeDate: null);

    private Task<IActionResult> EditSample(int id, DateTime? lastChangeDate = null, DateTime? bewerbungsDatum = null) =>
        _controller.Edit(
            id, jobTitle: "Dev", unternehmen: "ACME", isFreelance: false, notes: null, isCompleted: false,
            priority: "Normal", batchId: 2, description: null, dueDate: null, status: "Gespräch",
            anschreiben: null, lebenslauf: "cv.pdf", bewerbungsDatum: bewerbungsDatum,
            kontakt: null, createdDate: null, lastChangeDate: lastChangeDate);

    /// <summary>Zeichnet Requests samt Body auf (statt async-void-Callbacks an Moq-Mocks).</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "";
        private Exception? _exception;

        public List<RecordedRequest> Requests { get; } = new();

        public RecordedRequest Single => Assert.Single(Requests);

        public void Respond(HttpStatusCode status, string body) { _status = status; _body = body; }

        public void Throw(Exception exception) => _exception = exception;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body));

            if (_exception is not null) throw _exception;

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body)
    {
        public JsonElement Json => JsonDocument.Parse(Body ?? throw new InvalidOperationException("Kein Request-Body")).RootElement;
    }
}
