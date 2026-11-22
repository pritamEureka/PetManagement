using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pawzaroo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WidenPaymentCallbackLogColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "outcome",
                table: "payment_callback_logs",
                type: "character varying(48)",
                maxLength: 48,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "event_type",
                table: "payment_callback_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "outcome",
                table: "payment_callback_logs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(48)",
                oldMaxLength: 48);

            migrationBuilder.AlterColumn<string>(
                name: "event_type",
                table: "payment_callback_logs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
