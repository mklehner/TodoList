using System.Net;
using System.Text;
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
    [Fact]
    public async Task Index_ReturnsViewWithProducts_WhenApiCallIsSuccessful()
    {
        // 1. ARRANGEMENT (Vorbereitung)
        
        // Wir simulieren eine erfolgreiche HTTP-Antwort der REST-API mit einer leeren JSON-Liste "[]"
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
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

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        // IHttpClientFactory mocken, damit sie unseren manipulierten HttpClient ausgibt
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Leere Konfiguration simulieren
        var configMock = new Mock<IConfiguration>();

        // Controller mit den vorgetäuschten Abhängigkeiten instanziieren
        var controller = new ProductController(factoryMock.Object, configMock.Object);

        // 2. ACT (Ausführung der zu testenden Methode)
        var result = await controller.Index();

        // 3. ASSERT (Überprüfung des Ergebnisses)
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<TodoViewModel>>(viewResult.Model);
        Assert.Empty(model); // Der Zustand muss leer sein, da wir "[]" zurückgegeben haben
    }

    [Fact]
    public async Task Delete_SendsDeleteRequestToApi_AndRedirectsToIndex()
    {
        // 1. ARRANGEMENT
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            // Wir simulieren die HTTP-Antwort 204 NoContent, die unsere REST-API beim Löschen sendet
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NoContent })
            .Verifiable(); // Sicherstellen, dass der Aufruf stattfindet

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        var configMock = new Mock<IConfiguration>();

        var controller = new ProductController(factoryMock.Object, configMock.Object);
        int testTodoId = 42;

        // 2. ACT
        var result = await controller.Delete(testTodoId);

        // 3. ASSERT
        // Prüfen, ob das Ergebnis ein Redirect (Weiterleitung) ist
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        // Prüfen, ob zur Methode "Index" weitergeleitet wird
        Assert.Equal("Index", redirectResult.ActionName);

        // Prüfen, ob der HTTP-Aufruf exakt mit der ID 42 an die API ging
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Delete && 
                req.RequestUri!.ToString().EndsWith($"/api/todos/{testTodoId}")),
            ItExpr.IsAny<CancellationToken>()
        );
    }
}

