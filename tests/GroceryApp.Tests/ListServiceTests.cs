using GroceryApp.Domain;
using GroceryApp.Services;

namespace GroceryApp.Tests;

public sealed class ListServiceTests : IDisposable
{
    private readonly SqliteDbFactory _db = new();
    private readonly ListService _lists;

    public ListServiceTests() => _lists = new ListService(_db, TimeProvider.System);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Adding_same_product_twice_increments_quantity()
    {
        var milk = FakeCatalog.Product("Milk", "4006381333931");

        await _lists.AddProductAsync(ListKind.Shopping, milk);
        var item = await _lists.AddProductAsync(ListKind.Shopping, milk);

        Assert.Equal(2, item.Quantity);
        Assert.Single(await _lists.GetItemsAsync(ListKind.Shopping));
    }

    [Fact]
    public async Task Sync_updates_cached_details_on_all_lists_and_reports_changes()
    {
        var milk = FakeCatalog.Product("Milk");
        var bread = FakeCatalog.Product("Bread");
        await _lists.AddProductAsync(ListKind.Shopping, milk);
        await _lists.AddProductAsync(ListKind.Inventory, milk);
        await _lists.AddProductAsync(ListKind.Shopping, bread);

        var renamed = milk with { Name = "Whole milk", Brand = "Dairy", HasImage = true };
        var changed = await _lists.SyncProductsAsync([renamed, bread]);

        Assert.Equal(2, changed);
        foreach (var list in new[] { ListKind.Shopping, ListKind.Inventory })
        {
            var item = (await _lists.GetItemsAsync(list)).Single(i => i.ProductId == milk.Id);
            Assert.Equal("Whole milk", item.Name);
            Assert.Equal("Dairy", item.Brand);
            Assert.Equal(renamed.LocalImageUrl, item.ImageUrl);
        }
        Assert.Equal(0, await _lists.SyncProductsAsync([renamed, bread]));
    }

    [Fact]
    public async Task Removing_products_deletes_their_items_from_all_lists()
    {
        var milk = FakeCatalog.Product("Milk");
        var bread = FakeCatalog.Product("Bread");
        await _lists.AddProductAsync(ListKind.Shopping, milk);
        await _lists.AddProductAsync(ListKind.Inventory, milk);
        await _lists.AddProductAsync(ListKind.Shopping, bread);

        Assert.Equal(2, await _lists.RemoveProductsAsync([milk.Id]));

        Assert.Equal("Bread", Assert.Single(await _lists.GetItemsAsync(ListKind.Shopping)).Name);
        Assert.Empty(await _lists.GetItemsAsync(ListKind.Inventory));
        Assert.Equal(0, await _lists.RemoveProductsAsync([]));
    }

    [Fact]
    public async Task Lists_are_separate()
    {
        var milk = FakeCatalog.Product("Milk");

        await _lists.AddProductAsync(ListKind.Shopping, milk);
        await _lists.AddProductAsync(ListKind.Inventory, milk);
        await _lists.AddProductAsync(ListKind.Inventory, milk);

        Assert.Equal(1, (await _lists.GetItemsAsync(ListKind.Shopping)).Single().Quantity);
        Assert.Equal(2, (await _lists.GetItemsAsync(ListKind.Inventory)).Single().Quantity);
    }

    [Fact]
    public async Task Decreasing_to_zero_removes_item()
    {
        var item = await _lists.AddProductAsync(ListKind.Inventory, FakeCatalog.Product("Eggs"));

        var updated = await _lists.ChangeQuantityAsync(item.Id, -1);

        Assert.Null(updated);
        Assert.Empty(await _lists.GetItemsAsync(ListKind.Inventory));
    }

    [Fact]
    public async Task Moving_checked_items_merges_into_inventory()
    {
        var milk = FakeCatalog.Product("Milk");
        var bread = FakeCatalog.Product("Bread");
        await _lists.AddProductAsync(ListKind.Inventory, milk);
        var shopMilk = await _lists.AddProductAsync(ListKind.Shopping, milk, 2);
        var shopBread = await _lists.AddProductAsync(ListKind.Shopping, bread);
        await _lists.AddProductAsync(ListKind.Shopping, FakeCatalog.Product("Butter"));
        await _lists.SetCheckedAsync(shopMilk.Id, true);
        await _lists.SetCheckedAsync(shopBread.Id, true);

        var moved = await _lists.MoveCheckedToInventoryAsync();

        var inventory = await _lists.GetItemsAsync(ListKind.Inventory);
        var shopping = await _lists.GetItemsAsync(ListKind.Shopping);
        Assert.Equal(2, moved);
        Assert.Equal(3, inventory.Single(i => i.Name == "Milk").Quantity);
        Assert.Contains(inventory, i => i.Name == "Bread");
        Assert.Equal("Butter", shopping.Single().Name);
    }

    [Fact]
    public async Task Checked_items_sort_last_and_can_be_removed()
    {
        var a = await _lists.AddProductAsync(ListKind.Shopping, FakeCatalog.Product("Apples"));
        await _lists.AddProductAsync(ListKind.Shopping, FakeCatalog.Product("Zucchini"));
        await _lists.SetCheckedAsync(a.Id, true);

        var items = await _lists.GetItemsAsync(ListKind.Shopping);
        var removed = await _lists.RemoveCheckedAsync(ListKind.Shopping);

        Assert.Equal("Zucchini", items[0].Name);
        Assert.Equal(1, removed);
        Assert.Single(await _lists.GetItemsAsync(ListKind.Shopping));
    }
}
