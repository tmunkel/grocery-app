using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GroceryApp.Services;

public class ProductInventoryOptions
{
    public const string SectionName = "ProductInventory";
    public string BaseUrl { get; set; } = "http://localhost:5100/";
}

public class HttpProductCatalog(HttpClient http, ILogger<HttpProductCatalog> logger) : IProductCatalog
{
    public async Task<IReadOnlyList<CatalogProduct>> SearchAsync(string query, int pageSize = 20, CancellationToken ct = default)
    {
        var url = $"api/products?query={Uri.EscapeDataString(query.Trim())}&pageSize={pageSize}";
        var result = await SendAsync<PagedResult<CatalogProduct>>(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
        return result?.Items ?? [];
    }

    public Task<CatalogProduct?> GetByEanAsync(string ean, CancellationToken ct = default) =>
        SendAsync<CatalogProduct>(() => new HttpRequestMessage(HttpMethod.Get, $"api/products/ean/{Uri.EscapeDataString(ean.Trim())}"), ct);

    public async Task<IReadOnlyList<CatalogProduct>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        const int batchSize = 100; // service page size limit
        var products = new List<CatalogProduct>(ids.Count);
        foreach (var batch in ids.Distinct().Chunk(batchSize))
        {
            var url = $"api/products?pageSize={batchSize}&" + string.Join("&", batch.Select(id => $"ids={id}"));
            var result = await SendAsync<PagedResult<CatalogProduct>>(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
            products.AddRange(result?.Items ?? []);
        }
        return products;
    }

    public async Task<CatalogProduct> CreateAsync(ProductInput input, CancellationToken ct = default) =>
        await SendAsync<CatalogProduct>(() => new HttpRequestMessage(HttpMethod.Post, "api/products") { Content = JsonContent.Create(input) }, ct)
        ?? throw new CatalogException("The product could not be created.");

    public async Task<CatalogProduct> UpdateAsync(Guid productId, ProductInput input, CancellationToken ct = default) =>
        await SendAsync<CatalogProduct>(() => new HttpRequestMessage(HttpMethod.Put, $"api/products/{productId}") { Content = JsonContent.Create(input) }, ct)
        ?? throw new CatalogException("The product no longer exists.");

    public async Task DeleteImageAsync(Guid productId, CancellationToken ct = default) =>
        await SendAsync<object>(() => new HttpRequestMessage(HttpMethod.Delete, $"api/products/{productId}/image"), ct);

    public async Task DeleteAsync(Guid productId, CancellationToken ct = default) =>
        await SendAsync<object>(() => new HttpRequestMessage(HttpMethod.Delete, $"api/products/{productId}"), ct);

    public async Task<CatalogProduct> UploadImageAsync(Guid productId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        return await SendAsync<CatalogProduct>(() => new HttpRequestMessage(HttpMethod.Post, $"api/products/{productId}/image") { Content = form }, ct)
            ?? throw new CatalogException("The image could not be uploaded.");
    }

    public async Task<CatalogProduct> ImportImageAsync(Guid productId, string url, CancellationToken ct = default) =>
        await SendAsync<CatalogProduct>(() => new HttpRequestMessage(HttpMethod.Post, $"api/products/{productId}/image/from-url")
            { Content = JsonContent.Create(new { url }) }, ct)
        ?? throw new CatalogException("The image could not be imported.");

    public Task<ExternalProductInfo?> LookupExternalAsync(string ean, CancellationToken ct = default) =>
        SendAsync<ExternalProductInfo>(() => new HttpRequestMessage(HttpMethod.Get, $"api/lookup/{Uri.EscapeDataString(ean.Trim())}"), ct);

    public async Task<ProductImage?> GetImageAsync(Guid productId, CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync($"api/products/{productId}/image", HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                response.Dispose();
                return null;
            }
            return new ProductImage(await response.Content.ReadAsStreamAsync(ct),
                response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Could not load image for product {ProductId}", productId);
            return null;
        }
    }

    /// <summary>Sends a request; returns null for 404, throws <see cref="CatalogException"/> for other failures.</summary>
    private async Task<T?> SendAsync<T>(Func<HttpRequestMessage> createRequest, CancellationToken ct) where T : class
    {
        HttpResponseMessage response;
        try
        {
            using var request = createRequest();
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogError(ex, "Product inventory service is unreachable");
            throw new CatalogException("The product inventory service is not reachable.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new CatalogException(await ReadErrorAsync(response, ct));
            }
            if (response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object))
            {
                return null;
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                return string.Join(" ", errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray().Select(v => v.GetString())));
            }
            if (root.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } d)
            {
                return d;
            }
            if (root.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } t)
            {
                return t;
            }
        }
        catch (JsonException)
        {
        }
        return $"The product inventory service returned {(int)response.StatusCode}.";
    }
}
