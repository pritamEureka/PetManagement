using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pawzaroo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ApprovalStatus.Approved (1) — existing users predate the approval flow
            // and must keep working. New self-registrations are explicitly set to
            // Pending by RegisterCommandHandler.
            migrationBuilder.AddColumn<int>(
                name: "approval_status",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "users");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "approved_by_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "users");
        }
    }
}
