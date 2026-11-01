using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "list_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    List = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Ean = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Checked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_list_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_list_items_List_ProductId",
                table: "list_items",
                columns: new[] { "List", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "list_items");
        }
    }
}
