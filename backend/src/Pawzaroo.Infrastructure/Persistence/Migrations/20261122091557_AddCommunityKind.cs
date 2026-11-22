using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pawzaroo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "communities",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Community");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                table: "communities");
        }
    }
}
