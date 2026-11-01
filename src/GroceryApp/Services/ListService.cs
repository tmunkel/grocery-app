using GroceryApp.Data;
using GroceryApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace GroceryApp.Services;

/// <summary>Manages the shopping list and the home inventory.</summary>
public class ListService(IDbContextFactory<GroceryDbContext> dbFactory, TimeProvider clock)
{
    public async Task<List<ListItem>> GetItemsAsync(ListKind list, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var items = await db.ListItems.AsNoTracking().Where(i => i.List == list).ToListAsync(ct);
        return items.OrderBy(i => i.Checked).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Adds a product to a list or increments its quantity when already present.</summary>
    public async Task<ListItem> AddProductAsync(ListKind list, CatalogProduct product, int quantity = 1, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow();
        var item = await db.ListItems.FirstOrDefaultAsync(i => i.List == list && i.ProductId == product.Id, ct);
        if (item is null)
        {
            item = new ListItem
            {
                Id = Guid.CreateVersion7(),
                List = list,
                ProductId = product.Id,
                Name = product.Name,
                Quantity = 0,
                CreatedAt = now,
            };
            db.ListItems.Add(item);
        }

        item.Name = product.Name;
        item.Brand = product.Brand;
        item.Ean = product.Ean;
        item.ImageUrl = product.LocalImageUrl;
        item.Quantity += Math.Max(1, quantity);
        item.Checked = false;
        item.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return item;
    }

    /// <summary>
    /// Updates the cached product details (name, brand, EAN, image) of all list items for the given products.
    /// Returns the number of items that changed.
    /// </summary>
    public async Task<int> SyncProductsAsync(IReadOnlyCollection<CatalogProduct> products, CancellationToken ct = default)
    {
        if (products.Count == 0)
        {
            return 0;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var byId = products.DistinctBy(p => p.Id).ToDictionary(p => p.Id);
        var ids = byId.Keys.ToList();
        var items = await db.ListItems.Where(i => i.ProductId != null && ids.Contains(i.ProductId.Value)).ToListAsync(ct);
        var changed = 0;
        foreach (var item in items)
        {
            var product = byId[item.ProductId!.Value];
            var imageUrl = product.LocalImageUrl;
            if (item.Name == product.Name && item.Brand == product.Brand && item.Ean == product.Ean && item.ImageUrl == imageUrl)
            {
                continue;
            }
            item.Name = product.Name;
            item.Brand = product.Brand;
            item.Ean = product.Ean;
            item.ImageUrl = imageUrl;
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
        }
        return changed;
    }

    /// <summary>Changes the quantity; items reaching zero are removed. Returns the updated item or null when removed.</summary>
    public async Task<ListItem?> ChangeQuantityAsync(Guid itemId, int delta, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var item = await db.ListItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null)
        {
            return null;
        }

        item.Quantity += delta;
        if (item.Quantity <= 0)
        {
            db.ListItems.Remove(item);
            await db.SaveChangesAsync(ct);
            return null;
        }

        item.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task RemoveAsync(Guid itemId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.ListItems.Where(i => i.Id == itemId).ExecuteDeleteAsync(ct);
    }

    public async Task SetCheckedAsync(Guid itemId, bool isChecked, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var item = await db.ListItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is not null)
        {
            item.Checked = isChecked;
            item.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Moves all checked (bought) shopping list items into the inventory.</summary>
    public async Task<int> MoveCheckedToInventoryAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow();
        var bought = await db.ListItems.Where(i => i.List == ListKind.Shopping && i.Checked).ToListAsync(ct);
        var productIds = bought.Where(b => b.ProductId != null).Select(b => b.ProductId).ToList();
        var existing = await db.ListItems
            .Where(i => i.List == ListKind.Inventory && productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId!.Value, ct);

        foreach (var item in bought)
        {
            if (item.ProductId is { } pid && existing.TryGetValue(pid, out var stock))
            {
                stock.Quantity += item.Quantity;
                stock.UpdatedAt = now;
                db.ListItems.Remove(item);
            }
            else
            {
                item.List = ListKind.Inventory;
                item.Checked = false;
                item.UpdatedAt = now;
            }
        }

        await db.SaveChangesAsync(ct);
        return bought.Count;
    }

    public async Task<int> RemoveCheckedAsync(ListKind list, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ListItems.Where(i => i.List == list && i.Checked).ExecuteDeleteAsync(ct);
    }
}
