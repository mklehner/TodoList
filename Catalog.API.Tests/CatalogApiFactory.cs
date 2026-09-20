using Catalog.API;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Catalog.API.Tests;

/// <summary>
/// Startet die echte Minimal-API in-process, ersetzt aber Npgsql durch eine
/// eigene InMemory-Datenbank pro Factory-Instanz (Tests beeinflussen sich nicht).
/// Hinweis: InMemory ist nicht Postgres – z. B. ToLower()/Contains() und die
/// Sortierung verhalten sich hier ggf. anders als auf einer echten DB.
/// </summary>
public class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"catalog-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            // Npgsql-Registrierung aus Program.cs entfernen ...
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<CatalogDbContext>)
                         || d.ServiceType == typeof(CatalogDbContext))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            // ... und durch InMemory ersetzen
            services.AddDbContext<CatalogDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>Führt eine Aktion mit einem frischen DbContext aus (Seed / direkte DB-Prüfung).</summary>
    public async Task<T> WithDbAsync<T>(Func<CatalogDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await action(db);
    }

    public Task WithDbAsync(Func<CatalogDbContext, Task> action) =>
        WithDbAsync<object?>(async db => { await action(db); return null; });
}
