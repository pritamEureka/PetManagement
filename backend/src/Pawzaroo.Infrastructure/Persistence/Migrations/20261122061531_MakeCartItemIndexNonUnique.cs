using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pawzaroo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeCartItemIndexNonUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cart_items_user_id_product_id",
                table: "cart_items");

            migrationBuilder.CreateIndex(
                name: "ix_cart_items_user_id_product_id",
                table: "cart_items",
                columns: new[] { "user_id", "product_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cart_items_user_id_product_id",
                table: "cart_items");

            migrationBuilder.CreateIndex(
                name: "ix_cart_items_user_id_product_id",
                table: "cart_items",
                columns: new[] { "user_id", "product_id" },
                unique: true);
        }
    }
}
