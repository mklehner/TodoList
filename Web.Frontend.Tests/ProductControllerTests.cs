using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Web.Frontend.Controllers;
using Web.Frontend.Models;
using Xunit;

namespace Web.Frontend.Tests;

public class ProductControllerTests
{
    private readonly Mock<IHttpClientFactory> _factoryMock;
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly Mock<IConfiguration> _configMock;

    public ProductControllerTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _factoryMock = new Mock<IHttpClientFactory>();
        _configMock = new Mock<IConfiguration>();

        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };
        _factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    [Fact]
    public async Task Index_ReturnsViewWithProducts_WhenApiCallIsSuccessful()
    {
        // ARRANGEMENT
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });

        var controller = new ProductController(_factoryMock.Object, _configMock.Object);

        // ACT
        var result = await controller.Index(editId: null, search: null, priorityFilter: null);

        // ASSERT
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<TodoViewModel>>(viewResult.Model);
        Assert.Empty(model);
    }

    [Fact]
    public async Task Index_CorrectlyBuildsTreeStructure_AndRetainsBatchId()
    {
        // ARRANGEMENT: Flache Liste aus der API mit echten, gespeicherten BatchIds (z. B. 42 und 99)
        var flatJson = @"[
            {""Id"": 1, ""Title"": ""Hauptaufgabe"", ""ParentId"": null, ""Priority"": ""Mittel"", ""BatchId"": 42},
            {""Id"": 2, ""Title"": ""Unteraufgabe"", ""ParentId"": 1, ""Priority"": ""Mittel"", ""BatchId"": 99}
        ]";

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(flatJson, Encoding.UTF8, "application/json")
            });

        var controller = new ProductController(_factoryMock.Object, _configMock.Object);

        // ACT
        var result = await controller.Index(editId: null, search: null, priorityFilter: null);

        // ASSERT
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<TodoViewModel>>(viewResult.Model).ToList();

        // Es darf nur die Hauptaufgabe (Root) auf oberster Ebene zurückgegeben werden
        Assert.Single(model); 
        Assert.Equal(1, model.First().Id);
        Assert.Equal(42, model.First().BatchId); // 🔴 Prüft, ob der Wert aus DB erhalten bleibt (nicht mehr überschrieben wird!)

        // Die Unteraufgabe muss hierarchisch im Speicher in die 'SubTodos'-Liste einsortiert worden sein
        Assert.Single(model.First().SubTodos);
        Assert.Equal(2, model.First().SubTodos.First().Id);
        Assert.Equal(99, model.First().SubTodos.First().BatchId);
    }

    [Fact]
    public async Task Index_SendsCorrectQueryParameters_ToBackendApi()
    {
        // ARRANGEMENT
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });

        var controller = new ProductController(_factoryMock.Object, _configMock.Object);
        string searchWord = "Bewerbung";
        string filterPrio = "Hoch";

        // ACT
        await controller.Index(editId: null, search: searchWord, priorityFilter: filterPrio);

        // ASSERT
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Get && 
                req.RequestUri!.ToString().Contains($"search={searchWord}") && 
                req.RequestUri!.ToString().Contains($"priority={filterPrio}")),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task Create_SendsPostRequestToApi_WithAllParameters()
    {
        // ARRANGEMENT
        string capturedJson = string.Empty;

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>(async (req, token) =>
            {
                if (req.Content != null)
                {
                    capturedJson = await req.Content.ReadAsStringAsync(token);
                }
            })
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.Created });

        var controller = new ProductController(_factoryMock.Object, _configMock.Object);

        // ACT
        var result = await controller.Create("Bewerbung abschicken", DateTime.Today, "C# Projekt zeigen", "Hoch");

        // ASSERT
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);

        Assert.Contains("Bewerbung abschicken", capturedJson);
        Assert.Contains("Hoch", capturedJson);

        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task Edit_SendsPutRequestToApi_WithBatchId_AndRedirectsToIndex()
    {
        // ARRANGEMENT
        string capturedJson = string.Empty;
        int testId = 1;
        int testBatchId = 7; // 🔴 Testwert für die BatchId

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>(async (req, token) =>
            {
                if (req.Content != null)
                {
                    capturedJson = await req.Content.ReadAsStringAsync(token);
                }
            })
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var controller = new ProductController(_factoryMock.Object, _configMock.Object);

        // ACT - 🔴 Aufruf angepasst: Parameter 'batchId: testBatchId' am Ende übergeben
        var result = await controller.Edit(testId, "Titel geändert", DateTime.Today, "Neue Notiz", "Niedrig", isCompleted: false, batchId: testBatchId);

        // ASSERT
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);

        var deserializedTodo = JsonSerializer.Deserialize<Dictionary<string, object>>(capturedJson, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(deserializedTodo);
        Assert.Equal("Titel geändert", deserializedTodo["Title"].ToString());
        
        // 🔴 Verifiziert, dass die BatchId korrekt im JSON serialisiert und an die API geschickt wird
        Assert.Equal(testBatchId.ToString(), deserializedTodo["BatchId"].ToString());

        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Put && 
                req.RequestUri!.ToString().EndsWith($"/api/todos/{testId}")),
            ItExpr.IsAny<CancellationToken>()
        );
    }
}

