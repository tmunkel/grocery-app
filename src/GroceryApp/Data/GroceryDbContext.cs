using GroceryApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace GroceryApp.Data;

public class GroceryDbContext(DbContextOptions<GroceryDbContext> options) : DbContext(options)
{
    public DbSet<ListItem> ListItems => Set<ListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var item = modelBuilder.Entity<ListItem>();
        item.ToTable("list_items");
        item.HasKey(i => i.Id);
        item.Property(i => i.List).HasConversion<string>().HasMaxLength(20);
        item.Property(i => i.Name).HasMaxLength(200).IsRequired();
        item.Property(i => i.Brand).HasMaxLength(200);
        item.Property(i => i.Ean).HasMaxLength(14);
        item.Property(i => i.ImageUrl).HasMaxLength(300);
        item.HasIndex(i => new { i.List, i.ProductId });
    }
}
