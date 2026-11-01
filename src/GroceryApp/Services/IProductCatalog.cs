namespace GroceryApp.Services;

public record CatalogProduct(
    Guid Id,
    string? Ean,
    string Name,
    string? Brand,
    string? Category,
    string? Quantity,
    string? Description,
    bool HasImage,
    string? ImageUrl,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Image URL served by this app (proxied from the inventory service).</summary>
    public string? LocalImageUrl => HasImage ? $"/product-images/{Id}?v={UpdatedAt.ToUnixTimeSeconds()}" : null;
}

public record ProductInput(
    string? Ean,
    string Name,
    string? Brand,
    string? Category,
    string? Quantity,
    string? Description);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record ExternalProductInfo(
    string Ean,
    string? Name,
    string? Brand,
    string? Category,
    string? Quantity,
    string? ImageUrl,
    string Source);

public record ProductImage(Stream Content, string ContentType);

public class CatalogException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Client for the product inventory REST service.</summary>
public interface IProductCatalog
{
    Task<IReadOnlyList<CatalogProduct>> SearchAsync(string query, int pageSize = 20, CancellationToken ct = default);
    Task<CatalogProduct?> GetByEanAsync(string ean, CancellationToken ct = default);
    /// <summary>Loads the given products; unknown ids are omitted.</summary>
    Task<IReadOnlyList<CatalogProduct>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<CatalogProduct> CreateAsync(ProductInput input, CancellationToken ct = default);
    Task<CatalogProduct> UpdateAsync(Guid productId, ProductInput input, CancellationToken ct = default);
    Task<CatalogProduct> UploadImageAsync(Guid productId, Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task<CatalogProduct> ImportImageAsync(Guid productId, string url, CancellationToken ct = default);
    Task DeleteImageAsync(Guid productId, CancellationToken ct = default);
    Task<ExternalProductInfo?> LookupExternalAsync(string ean, CancellationToken ct = default);
    Task<ProductImage?> GetImageAsync(Guid productId, CancellationToken ct = default);
}
