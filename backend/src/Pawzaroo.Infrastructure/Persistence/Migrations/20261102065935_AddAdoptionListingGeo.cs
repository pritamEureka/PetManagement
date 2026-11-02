using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pawzaroo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdoptionListingGeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "adoption_listings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "adoption_listings",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_adoption_listings_status_latitude_longitude",
                table: "adoption_listings",
                columns: new[] { "status", "latitude", "longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_adoption_listings_status_latitude_longitude",
                table: "adoption_listings");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "adoption_listings");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "adoption_listings");
        }
    }
}
