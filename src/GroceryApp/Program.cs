using GroceryApp.Components;
using GroceryApp.Data;
using GroceryApp.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContextFactory<GroceryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Grocery")
        ?? throw new InvalidOperationException("Connection string 'Grocery' is missing.")));
builder.Services.AddScoped<ListService>();

var inventory = builder.Configuration.GetSection(ProductInventoryOptions.SectionName).Get<ProductInventoryOptions>() ?? new();
builder.Services.AddHttpClient<IProductCatalog, HttpProductCatalog>(client =>
{
    client.BaseAddress = new Uri(inventory.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var db = await app.Services.GetRequiredService<IDbContextFactory<GroceryDbContext>>().CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// Product images are proxied so phones only need to reach this app, not the inventory service.
app.MapGet("/product-images/{id:guid}", async (Guid id, IProductCatalog catalog, HttpContext http, CancellationToken ct) =>
{
    var image = await catalog.GetImageAsync(id, ct);
    if (image is null)
    {
        return Results.NotFound();
    }
    http.Response.Headers.CacheControl = "public, max-age=86400";
    return Results.Stream(image.Content, image.ContentType);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
