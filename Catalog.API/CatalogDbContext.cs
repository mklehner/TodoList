using Catalog.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }
    
    // Die Tabelle heißt nun 'Todos'
    public DbSet<TodoItem> Todos => Set<TodoItem>();
}

