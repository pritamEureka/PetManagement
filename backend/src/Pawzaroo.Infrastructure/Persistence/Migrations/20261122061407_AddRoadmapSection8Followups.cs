using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pawzaroo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoadmapSection8Followups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attachments_json",
                table: "assistant_messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "assistant_review_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    urgency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    checklist_json = table.Column<string>(type: "jsonb", nullable: true),
                    image_urls_json = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    vet_notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assistant_review_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "passkey_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credential_id = table.Column<string>(type: "character varying(1400)", maxLength: 1400, nullable: false),
                    public_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    sign_count = table.Column<long>(type: "bigint", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_passkey_credentials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    dataset = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    recipients = table.Column<List<string>>(type: "text[]", nullable: false),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scheduled_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ran_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    recipient_count = table.Column<int>(type: "integer", nullable: false),
                    emails_sent = table.Column<int>(type: "integer", nullable: false),
                    csv_text = table.Column<string>(type: "text", nullable: true),
                    csv_truncated = table.Column<bool>(type: "boolean", nullable: false),
                    manual = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_runs_scheduled_report_scheduled_report_id",
                        column: x => x.scheduled_report_id,
                        principalTable: "scheduled_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assistant_review_requests_status_created_at",
                table: "assistant_review_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_assistant_review_requests_user_id",
                table: "assistant_review_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_passkey_credentials_credential_id",
                table: "passkey_credentials",
                column: "credential_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_passkey_credentials_user_id",
                table: "passkey_credentials",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_runs_scheduled_report_id_ran_at",
                table: "report_runs",
                columns: new[] { "scheduled_report_id", "ran_at" });

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_reports_is_active_next_run_at",
                table: "scheduled_reports",
                columns: new[] { "is_active", "next_run_at" });

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_reports_owner_user_id",
                table: "scheduled_reports",
                column: "owner_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assistant_review_requests");

            migrationBuilder.DropTable(
                name: "passkey_credentials");

            migrationBuilder.DropTable(
                name: "report_runs");

            migrationBuilder.DropTable(
                name: "scheduled_reports");

            migrationBuilder.DropColumn(
                name: "attachments_json",
                table: "assistant_messages");
        }
    }
}
