namespace GroceryApp.Domain;

public enum ListKind
{
    Shopping = 0,
    Inventory = 1,
}

public class ListItem
{
    public Guid Id { get; set; }
    public ListKind List { get; set; }
    public Guid? ProductId { get; set; }
    public required string Name { get; set; }
    public string? Brand { get; set; }
    public string? Ean { get; set; }
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; } = 1;
    public bool Checked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
