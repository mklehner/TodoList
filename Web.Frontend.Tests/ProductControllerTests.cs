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

        // ACT - Aufruf an die neue Index-Signatur mit 3 Parametern angepasst
        var result = await controller.Index(editId: null, search: null, priorityFilter: null);

        // ASSERT
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<TodoViewModel>>(viewResult.Model);
        Assert.Empty(model);
    }

    [Fact]
    public async Task Index_CorrectlyBuildsTreeStructure_FromFlatList()
    {
        // ARRANGEMENT: Wir simulieren eine flache Liste aus der API (1 Hauptaufgabe, 1 Unteraufgabe)
        var flatJson = @"[
            {""Id"": 1, ""Title"": ""Hauptaufgabe"", ""ParentId"": null, ""Priority"": ""Mittel""},
            {""Id"": 2, ""Title"": ""Unteraufgabe"", ""ParentId"": 1, ""Priority"": ""Mittel""}
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

        // Die Unteraufgabe muss hierarchisch im Speicher in die 'SubTodos'-Liste einsortiert worden sein
        Assert.Single(model.First().SubTodos);
        Assert.Equal(2, model.First().SubTodos.First().Id);
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

        // ACT - Aufruf an die neue Create-Überladung angepasst
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
    public async Task Edit_SendsPutRequestToApi_AndRedirectsToIndex()
    {
        // ARRANGEMENT
        string capturedJson = string.Empty;
        int testId = 1;

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

        // ACT
        var result = await controller.Edit(testId, "Titel geändert", DateTime.Today, "Neue Notiz", "Niedrig", isCompleted: false);

        // ASSERT
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);

        var deserializedTodo = JsonSerializer.Deserialize<Dictionary<string, object>>(capturedJson, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(deserializedTodo);
        Assert.Equal("Titel geändert", deserializedTodo["Title"].ToString());

        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Put && 
                req.RequestUri!.ToString().EndsWith($"/api/todos/{testId}")),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task Delete_SendsDeleteRequestToApi_AndRedirectsToIndex()
    {
        // ARRANGEMENT
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NoContent });

        var controller = new ProductController(_factoryMock.Object, _configMock.Object);
        int testTodoId = 42;

        // ACT
        var result = await controller.Delete(testTodoId);

        // ASSERT
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);

        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Delete && 
                req.RequestUri!.ToString().EndsWith($"/api/todos/{testTodoId}")),
            ItExpr.IsAny<CancellationToken>()
        );
    }
}

