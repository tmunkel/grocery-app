using Bunit;
using GroceryApp.Components.Pages;
using GroceryApp.Domain;
using GroceryApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryApp.Tests;

public sealed class HomePageTests : BunitContext
{
    private readonly SqliteDbFactory _db = new();
    private readonly FakeCatalog _catalog = new();
    private readonly ListService _lists;

    public HomePageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _lists = new ListService(_db, TimeProvider.System);
        Services.AddSingleton<IProductCatalog>(_catalog);
        Services.AddSingleton(_lists);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _db.Dispose();
    }

    private static void Submit(IRenderedComponent<Home> cut, string text)
    {
        cut.Find("#search").Input(text);
        cut.Find("form.search").Submit();
    }

    [Fact]
    public void Scanning_known_ean_adds_product_to_shopping_list()
    {
        _catalog.Products.Add(FakeCatalog.Product("Milk", "4006381333931"));
        var cut = Render<Home>();

        Submit(cut, "4006381333931");

        cut.WaitForAssertion(() => Assert.Contains("Milk", cut.Find(".item-list .name").TextContent));
        Assert.Equal("1", cut.Find(".qty").TextContent);
        Assert.Equal("", cut.Find("#search").GetAttribute("value"));
    }

    [Fact]
    public async Task Inventory_mode_adds_to_inventory()
    {
        _catalog.Products.Add(FakeCatalog.Product("Milk", "4006381333931"));
        var cut = Render<Home>();

        cut.FindAll(".mode-toggle button")[1].Click();
        Submit(cut, "4006381333931");
        Submit(cut, "4006381333931");

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find(".qty").TextContent));
        Assert.Single(await _lists.GetItemsAsync(ListKind.Inventory));
        Assert.Empty(await _lists.GetItemsAsync(ListKind.Shopping));
    }

    [Fact]
    public void Name_search_shows_results_and_tap_adds()
    {
        _catalog.Products.Add(FakeCatalog.Product("Whole milk"));
        _catalog.Products.Add(FakeCatalog.Product("Oat milk"));
        var cut = Render<Home>();

        Submit(cut, "milk");

        var results = cut.FindAll(".results .item");
        Assert.Equal(2, results.Count);
        results[1].Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".results")));
        Assert.Contains("Oat milk", cut.Find(".item-list .name").TextContent);
    }

    [Fact]
    public void Unknown_ean_opens_create_dialog_prefilled_from_open_food_facts()
    {
        _catalog.External["4006381333931"] = new ExternalProductInfo("4006381333931", "Nutella", "Ferrero", "Spreads", "400 g",
            "https://images.openfoodfacts.org/n.jpg", "OpenFoodFacts");
        var cut = Render<Home>();

        Submit(cut, "4006381333931");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("form.sheet")));
        var inputs = cut.FindAll("form.sheet input:not([type=checkbox]):not([type=file])");
        Assert.Equal("Nutella", inputs[0].GetAttribute("value"));
        Assert.Equal("4006381333931", inputs[1].GetAttribute("value"));
        Assert.Equal("Ferrero", inputs[2].GetAttribute("value"));

        cut.Find("form.sheet").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Nutella", cut.Find(".item-list .name").TextContent));
        Assert.Equal("Ferrero", _catalog.Created.Single().Brand);
        Assert.Equal("https://images.openfoodfacts.org/n.jpg", _catalog.Imported.Single().Url);
    }

    [Fact]
    public void Create_from_name_search_prefills_name()
    {
        var cut = Render<Home>();
        Submit(cut, "Sourdough");

        cut.Find(".results .btn.block").Click();

        var name = cut.Find("form.sheet input");
        Assert.Equal("Sourdough", name.GetAttribute("value"));
    }

    [Fact]
    public void Service_unavailable_shows_error()
    {
        _catalog.Unavailable = true;
        var cut = Render<Home>();

        Submit(cut, "milk");

        Assert.Contains("not reachable", cut.Find(".alert.error").TextContent);
    }

    [Fact]
    public void Stepper_changes_quantity_and_removes_at_zero()
    {
        _catalog.Products.Add(FakeCatalog.Product("Milk", "4006381333931"));
        var cut = Render<Home>();
        Submit(cut, "4006381333931");

        cut.FindAll(".stepper button")[1].Click();
        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find(".qty").TextContent));
        cut.FindAll(".stepper button")[0].Click();
        cut.FindAll(".stepper button")[0].Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".stepper")));
    }

    [Fact]
    public void Bought_items_can_be_moved_to_inventory()
    {
        _catalog.Products.Add(FakeCatalog.Product("Milk", "4006381333931"));
        var cut = Render<Home>();
        Submit(cut, "4006381333931");

        cut.Find(".tick").Change(true);
        cut.Find(".actions .btn.primary").Click();

        cut.WaitForAssertion(() => Assert.Contains("moved to inventory", cut.Find(".alert.success").TextContent));
        Assert.Empty(cut.FindAll(".item-list .item"));
    }

    [Fact]
    public async Task Editing_list_item_updates_product_and_list()
    {
        var product = FakeCatalog.Product("Bananas");
        _catalog.Products.Add(product);
        await _lists.AddProductAsync(ListKind.Shopping, product);
        var cut = Render<Home>();

        cut.Find(".item-main").Click();
        cut.WaitForAssertion(() => Assert.Equal("Edit product", cut.Find("form.sheet h2").TextContent));
        var name = cut.Find("form.sheet input");
        Assert.Equal("Bananas", name.GetAttribute("value"));

        name.Input("Organic bananas");
        cut.Find("form.sheet").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Organic bananas", cut.Find(".item-list .name").TextContent));
        Assert.Empty(cut.FindAll("form.sheet"));
        Assert.Equal("Organic bananas", _catalog.Products.Single().Name);
        Assert.Empty(_catalog.Created);
    }

    [Fact]
    public async Task Removing_image_in_edit_dialog_clears_list_thumbnail()
    {
        var product = FakeCatalog.Product("Milk") with { HasImage = true };
        _catalog.Products.Add(product);
        await _lists.AddProductAsync(ListKind.Shopping, product);
        var cut = Render<Home>();
        Assert.NotNull(cut.Find(".item-list img.thumb"));

        cut.Find(".item-main").Click();
        cut.WaitForAssertion(() => cut.Find(".btn.link.danger").Click());
        cut.Find("form.sheet").Submit();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".item-list img.thumb")));
        Assert.False(_catalog.Products.Single().HasImage);
    }

    [Fact]
    public async Task List_refreshes_cached_product_details_from_catalog_on_load()
    {
        var product = FakeCatalog.Product("Bananas");
        _catalog.Products.Add(product);
        await _lists.AddProductAsync(ListKind.Shopping, product);
        // Changed elsewhere (e.g. another device) after the item was added.
        _catalog.Products[0] = product with { Name = "Bananas (bunch)", HasImage = true };

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("Bananas (bunch)", cut.Find(".item-list .name").TextContent));
        Assert.NotNull(cut.Find(".item-list img.thumb"));
    }

    [Fact]
    public void Edit_button_in_search_results_opens_edit_dialog_without_adding()
    {
        _catalog.Products.Add(FakeCatalog.Product("Whole milk"));
        var cut = Render<Home>();
        Submit(cut, "milk");

        cut.Find(".results .icon-btn.edit").Click();

        Assert.Equal("Edit product", cut.Find("form.sheet h2").TextContent);
        Assert.Empty(cut.FindAll(".item-list .item:not(.clickable)"));
    }
}
