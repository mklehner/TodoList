var builder = WebApplication.CreateBuilder(args);

// MVC und HTTP Client hinzufügen
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Standardroute auf unseren TodoController setzen
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Todo}/{action=Index}/{id?}");

app.Run();

