using GroceryApp.Data;
using GroceryApp.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GroceryApp.Tests;

public sealed class SqliteDbFactory : IDbContextFactory<GroceryDbContext>, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<GroceryDbContext> _options;

    public SqliteDbFactory()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<GroceryDbContext>().UseSqlite(_connection).Options;
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public GroceryDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}

public sealed class FakeCatalog : IProductCatalog
{
    public List<CatalogProduct> Products { get; } = [];
    public Dictionary<string, ExternalProductInfo> External { get; } = new();
    public List<ProductInput> Created { get; } = [];
    public List<(Guid Id, string Url)> Imported { get; } = [];
    public bool Unavailable { get; set; }

    public static CatalogProduct Product(string name, string? ean = null, string? brand = null) =>
        new(Guid.NewGuid(), ean, name, brand, null, null, null, false, null, DateTimeOffset.UtcNow);

    private void ThrowIfUnavailable()
    {
        if (Unavailable) throw new CatalogException("The product inventory service is not reachable.");
    }

    public Task<IReadOnlyList<CatalogProduct>> SearchAsync(string query, int pageSize = 20, CancellationToken ct = default)
    {
        ThrowIfUnavailable();
        return Task.FromResult<IReadOnlyList<CatalogProduct>>(
            Products.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    public Task<CatalogProduct?> GetByEanAsync(string ean, CancellationToken ct = default)
    {
        ThrowIfUnavailable();
        return Task.FromResult(Products.FirstOrDefault(p => p.Ean == ean));
    }

    public Task<CatalogProduct> CreateAsync(ProductInput input, CancellationToken ct = default)
    {
        ThrowIfUnavailable();
        Created.Add(input);
        var product = new CatalogProduct(Guid.NewGuid(), input.Ean, input.Name, input.Brand, input.Category, input.Quantity,
            input.Description, false, null, DateTimeOffset.UtcNow);
        Products.Add(product);
        return Task.FromResult(product);
    }

    public Task<CatalogProduct> UploadImageAsync(Guid productId, Stream content, string fileName, string contentType, CancellationToken ct = default) =>
        Task.FromResult(Products.Single(p => p.Id == productId) with { HasImage = true });

    public Task<CatalogProduct> ImportImageAsync(Guid productId, string url, CancellationToken ct = default)
    {
        Imported.Add((productId, url));
        return Task.FromResult(Products.Single(p => p.Id == productId) with { HasImage = true });
    }

    public Task<ExternalProductInfo?> LookupExternalAsync(string ean, CancellationToken ct = default) =>
        Task.FromResult(External.GetValueOrDefault(ean));

    public Task<ProductImage?> GetImageAsync(Guid productId, CancellationToken ct = default) =>
        Task.FromResult<ProductImage?>(null);
}
