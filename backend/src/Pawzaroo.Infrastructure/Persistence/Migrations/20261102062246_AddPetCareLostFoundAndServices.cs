using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pawzaroo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPetCareLostFoundAndServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "certificate_url",
                table: "vaccination_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "last_reminder_stage",
                table: "vaccination_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "admin_notes",
                table: "service_providers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "animal_types",
                table: "service_providers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "service_providers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "default_duration_minutes",
                table: "service_providers",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<string>(
                name: "logo_url",
                table: "service_providers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "service_bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "service_bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "confirmed_at",
                table: "service_bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duration_minutes",
                table: "service_bookings",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<Guid>(
                name: "dam_id",
                table: "pets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "pets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_lost",
                table: "pets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "microchip_number",
                table: "pets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "public_code",
                table: "pets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "public_profile_enabled",
                table: "pets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "sire_id",
                table: "pets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "size",
                table: "pets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attachment_url",
                table: "medical_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vet_name",
                table: "medical_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cost",
                table: "grooming_records",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "service_booking_id",
                table: "grooming_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "breeding_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sire_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sire_description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    mated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expected_due_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    outcome_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    offspring_count = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_breeding_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_breeding_records_pets_dam_id",
                        column: x => x.dam_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_breeding_records_pets_sire_id",
                        column: x => x.sire_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "lost_found_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    animal_type = table.Column<int>(type: "integer", nullable: false),
                    breed = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    color = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    photo_url = table.Column<string>(type: "text", nullable: true),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    area = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    contact_preference = table.Column<int>(type: "integer", nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    feed_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lost_found_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_lost_found_reports_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lost_found_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_care_routines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_type = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    time_of_day = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_pet_care_routines", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_care_routines_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_caretakers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    invited_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_caretakers", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_caretakers_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pet_caretakers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    spent_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_expenses", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_expenses_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    animal_type = table.Column<int>(type: "integer", nullable: false),
                    purpose = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_groups_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_health_share_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_health_share_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_health_share_links_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_insurance_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    policy_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    coverage_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    coverage_limit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    premium = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    document_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_insurance_policies", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_insurance_policies_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_medications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    drug_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    dose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    dose_times = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_pet_medications", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_medications_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_ownership_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adoption_listing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transferred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    includes_health_history = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_ownership_transfers", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_ownership_transfers_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_weight_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_weight_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_weight_logs_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_care_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    routine_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activity_type = table.Column<int>(type: "integer", nullable: false),
                    performed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_care_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_care_logs_pet_care_routines_routine_id",
                        column: x => x.routine_id,
                        principalTable: "pet_care_routines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_pet_care_logs_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pet_care_logs_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "production_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recorded_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_production_logs_pet_groups_pet_group_id",
                        column: x => x.pet_group_id,
                        principalTable: "pet_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_production_logs_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "medication_dose_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    medication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_for = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    logged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    logged_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skipped = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medication_dose_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_medication_dose_logs_pet_medications_medication_id",
                        column: x => x.medication_id,
                        principalTable: "pet_medications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pets_dam_id",
                table: "pets",
                column: "dam_id");

            migrationBuilder.CreateIndex(
                name: "ix_pets_group_id",
                table: "pets",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_pets_microchip_number",
                table: "pets",
                column: "microchip_number");

            migrationBuilder.CreateIndex(
                name: "ix_pets_public_code",
                table: "pets",
                column: "public_code",
                unique: true,
                filter: "public_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pets_sire_id",
                table: "pets",
                column: "sire_id");

            migrationBuilder.CreateIndex(
                name: "ix_breeding_records_dam_id",
                table: "breeding_records",
                column: "dam_id");

            migrationBuilder.CreateIndex(
                name: "ix_breeding_records_owner_id",
                table: "breeding_records",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_breeding_records_sire_id",
                table: "breeding_records",
                column: "sire_id");

            migrationBuilder.CreateIndex(
                name: "ix_lost_found_reports_created_at",
                table: "lost_found_reports",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_lost_found_reports_pet_id",
                table: "lost_found_reports",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_lost_found_reports_reporter_id",
                table: "lost_found_reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "ix_lost_found_reports_status_kind_city",
                table: "lost_found_reports",
                columns: new[] { "status", "kind", "city" });

            migrationBuilder.CreateIndex(
                name: "ix_medication_dose_logs_medication_id_scheduled_for",
                table: "medication_dose_logs",
                columns: new[] { "medication_id", "scheduled_for" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pet_care_logs_performed_by_user_id",
                table: "pet_care_logs",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_care_logs_pet_id_performed_at",
                table: "pet_care_logs",
                columns: new[] { "pet_id", "performed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_pet_care_logs_routine_id",
                table: "pet_care_logs",
                column: "routine_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_care_routines_pet_id",
                table: "pet_care_routines",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_caretakers_pet_id_user_id",
                table: "pet_caretakers",
                columns: new[] { "pet_id", "user_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_pet_caretakers_user_id",
                table: "pet_caretakers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_expenses_pet_id_spent_on",
                table: "pet_expenses",
                columns: new[] { "pet_id", "spent_on" });

            migrationBuilder.CreateIndex(
                name: "ix_pet_groups_owner_id",
                table: "pet_groups",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_health_share_links_pet_id",
                table: "pet_health_share_links",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_health_share_links_token",
                table: "pet_health_share_links",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pet_insurance_policies_pet_id",
                table: "pet_insurance_policies",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_medications_pet_id_is_active",
                table: "pet_medications",
                columns: new[] { "pet_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_pet_ownership_transfers_pet_id",
                table: "pet_ownership_transfers",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_weight_logs_pet_id_recorded_on",
                table: "pet_weight_logs",
                columns: new[] { "pet_id", "recorded_on" });

            migrationBuilder.CreateIndex(
                name: "ix_production_logs_owner_id_recorded_on",
                table: "production_logs",
                columns: new[] { "owner_id", "recorded_on" });

            migrationBuilder.CreateIndex(
                name: "ix_production_logs_pet_group_id",
                table: "production_logs",
                column: "pet_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_logs_pet_id",
                table: "production_logs",
                column: "pet_id");

            migrationBuilder.AddForeignKey(
                name: "fk_pets_pet_groups_group_id",
                table: "pets",
                column: "group_id",
                principalTable: "pet_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_pets_pets_dam_id",
                table: "pets",
                column: "dam_id",
                principalTable: "pets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_pets_pets_sire_id",
                table: "pets",
                column: "sire_id",
                principalTable: "pets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pets_pet_groups_group_id",
                table: "pets");

            migrationBuilder.DropForeignKey(
                name: "fk_pets_pets_dam_id",
                table: "pets");

            migrationBuilder.DropForeignKey(
                name: "fk_pets_pets_sire_id",
                table: "pets");

            migrationBuilder.DropTable(
                name: "breeding_records");

            migrationBuilder.DropTable(
                name: "lost_found_reports");

            migrationBuilder.DropTable(
                name: "medication_dose_logs");

            migrationBuilder.DropTable(
                name: "pet_care_logs");

            migrationBuilder.DropTable(
                name: "pet_caretakers");

            migrationBuilder.DropTable(
                name: "pet_expenses");

            migrationBuilder.DropTable(
                name: "pet_health_share_links");

            migrationBuilder.DropTable(
                name: "pet_insurance_policies");

            migrationBuilder.DropTable(
                name: "pet_ownership_transfers");

            migrationBuilder.DropTable(
                name: "pet_weight_logs");

            migrationBuilder.DropTable(
                name: "production_logs");

            migrationBuilder.DropTable(
                name: "pet_medications");

            migrationBuilder.DropTable(
                name: "pet_care_routines");

            migrationBuilder.DropTable(
                name: "pet_groups");

            migrationBuilder.DropIndex(
                name: "ix_pets_dam_id",
                table: "pets");

            migrationBuilder.DropIndex(
                name: "ix_pets_group_id",
                table: "pets");

            migrationBuilder.DropIndex(
                name: "ix_pets_microchip_number",
                table: "pets");

            migrationBuilder.DropIndex(
                name: "ix_pets_public_code",
                table: "pets");

            migrationBuilder.DropIndex(
                name: "ix_pets_sire_id",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "certificate_url",
                table: "vaccination_records");

            migrationBuilder.DropColumn(
                name: "last_reminder_stage",
                table: "vaccination_records");

            migrationBuilder.DropColumn(
                name: "admin_notes",
                table: "service_providers");

            migrationBuilder.DropColumn(
                name: "animal_types",
                table: "service_providers");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "service_providers");

            migrationBuilder.DropColumn(
                name: "default_duration_minutes",
                table: "service_providers");

            migrationBuilder.DropColumn(
                name: "logo_url",
                table: "service_providers");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "service_bookings");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "service_bookings");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                table: "service_bookings");

            migrationBuilder.DropColumn(
                name: "duration_minutes",
                table: "service_bookings");

            migrationBuilder.DropColumn(
                name: "dam_id",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "is_lost",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "microchip_number",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "public_code",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "public_profile_enabled",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "sire_id",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "size",
                table: "pets");

            migrationBuilder.DropColumn(
                name: "attachment_url",
                table: "medical_records");

            migrationBuilder.DropColumn(
                name: "vet_name",
                table: "medical_records");

            migrationBuilder.DropColumn(
                name: "cost",
                table: "grooming_records");

            migrationBuilder.DropColumn(
                name: "service_booking_id",
                table: "grooming_records");
        }
    }
}
