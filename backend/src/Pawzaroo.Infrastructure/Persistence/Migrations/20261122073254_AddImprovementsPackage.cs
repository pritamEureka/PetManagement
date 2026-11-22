using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pawzaroo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImprovementsPackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_status_next_attempt_at",
                table: "outbox_messages");

            migrationBuilder.AddColumn<string>(
                name: "claimed_by",
                table: "outbox_messages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "claimed_until",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "adopter_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_size = table.Column<int>(type: "integer", nullable: false),
                    children_ages = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    existing_pets_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    experience = table.Column<int>(type: "integer", nullable: false),
                    home_type = table.Column<int>(type: "integer", nullable: true),
                    has_yard = table.Column<bool>(type: "boolean", nullable: false),
                    travel_distance_km = table.Column<int>(type: "integer", nullable: false),
                    activity = table.Column<int>(type: "integer", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adopter_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_adopter_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adoption_follow_ups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adopter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_offset = table.Column<int>(type: "integer", nullable: false),
                    due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    welfare_score = table.Column<int>(type: "integer", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reminder_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adoption_follow_ups", x => x.id);
                    table.ForeignKey(
                        name: "fk_adoption_follow_ups_adoption_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "adoption_listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adoption_home_visits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    outcome = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adoption_home_visits", x => x.id);
                    table.ForeignKey(
                        name: "fk_adoption_home_visits_adoption_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "adoption_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adoption_listing_risk_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adoption_listing_risk_flags", x => x.id);
                    table.ForeignKey(
                        name: "fk_adoption_listing_risk_flags_adoption_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "adoption_listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adoption_references",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    relationship = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adoption_references", x => x.id);
                    table.ForeignKey(
                        name: "fk_adoption_references_adoption_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "adoption_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adoption_request_stage_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<int>(type: "integer", nullable: false),
                    entered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    overdue_flagged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reminder_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adoption_request_stage_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_adoption_request_stage_events_adoption_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "adoption_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointment_deposits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fee_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_deposits", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointment_deposits_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointment_follow_ups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    in_days = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    follow_up_appointment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_follow_ups", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointment_follow_ups_appointments_follow_up_appointment_id",
                        column: x => x.follow_up_appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_appointment_follow_ups_appointments_source_appointment_id",
                        column: x => x.source_appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bundle_cart_discounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bundle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bundle_cart_discounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_shares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    token_hint = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_shares", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "care_plan_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    species = table.Column<int>(type: "integer", nullable: false),
                    breed = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_care_plan_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clinical_encounters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    chief_complaint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    findings = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    diagnosis = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    treatment_plan = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    finalized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finalized_by_doctor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signature_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clinical_encounters", x => x.id);
                    table.ForeignKey(
                        name: "fk_clinical_encounters_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_poll_votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    option_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_poll_votes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "community_polls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    community_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    multi_choice = table.Column<bool>(type: "boolean", nullable: false),
                    closes_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_polls", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversation_folders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_folders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversation_pins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_pins", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversation_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disappear_after_seconds = table.Column<int>(type: "integer", nullable: true),
                    muted_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    e2ee_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dead_letter_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    partition = table.Column<int>(type: "integer", nullable: false),
                    offset = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    payload = table.Column<string>(type: "text", nullable: false),
                    headers_json = table.Column<string>(type: "text", nullable: true),
                    consumer_group = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    failure_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    first_failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    redrive_count = table.Column<int>(type: "integer", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dead_letter_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cities = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    base_fee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    free_shipping_threshold = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    estimated_min_hours = table.Column<int>(type: "integer", nullable: false),
                    estimated_max_hours = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_delivery_zones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "doctor_booking_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    deposit_fixed = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cancellation_window_hours = table.Column<int>(type: "integer", nullable: false),
                    late_cancel_fee_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    no_show_fee_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    allow_reschedule_hours = table.Column<int>(type: "integer", nullable: false),
                    requires_questionnaire = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_booking_policies", x => x.id);
                    table.ForeignKey(
                        name: "fk_doctor_booking_policies_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "doctor_emergency_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is24x7 = table.Column<bool>(type: "boolean", nullable: false),
                    emergency_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    clinic_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    clinic_address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    accepts_walk_in_emergency = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doctor_emergency_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_doctor_emergency_profiles_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "encounter_completion_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encounter_completion_overrides", x => x.id);
                    table.ForeignKey(
                        name: "fk_encounter_completion_overrides_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exposure_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substance = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount_estimate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    onset_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    exposed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    symptoms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    escalated = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exposure_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_exposure_reports_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "foster_placements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    foster_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_foster_placements", x => x.id);
                    table.ForeignKey(
                        name: "fk_foster_placements_adoption_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "adoption_listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fulfillment_slas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: true),
                    max_hours_to_ship = table.Column<int>(type: "integer", nullable: false),
                    max_hours_to_deliver = table.Column<int>(type: "integer", nullable: false),
                    penalty_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_fulfillment_slas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gift_card_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gift_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gift_card_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gift_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    initial_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    purchaser_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gift_cards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "health_record_seals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_type = table.Column<int>(type: "integer", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sealed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vet_doctor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sealed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    signature_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_health_record_seals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "health_record_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_type = table.Column<int>(type: "integer", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_health_record_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: true),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "livestock_treatments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagnosis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    treatment = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    administered_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    meat_withdrawal_days = table.Column<int>(type: "integer", nullable: false),
                    milk_withdrawal_days = table.Column<int>(type: "integer", nullable: false),
                    meat_safe_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    milk_safe_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_livestock_treatments", x => x.id);
                    table.ForeignKey(
                        name: "fk_livestock_treatments_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lost_found_alert_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    radius_km = table.Column<double>(type: "double precision", nullable: false),
                    animal_type = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lost_found_alert_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lost_found_detail_extras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    markings = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    collar_description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    age_estimate = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    microchip_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    time_window_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    time_window_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    location_expired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lost_found_detail_extras", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lost_found_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lost_found_evidence", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lost_found_sightings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    photo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    verified_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lost_found_sightings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance = table.Column<int>(type: "integer", nullable: false),
                    lifetime_points = table.Column<int>(type: "integer", nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loyalty_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loyalty_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_edits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_content = table.Column<string>(type: "text", nullable: true),
                    edited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_edits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_digest_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_digest_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_digest_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_mutes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scope_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    muted_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_mutes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_applied_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    points = table.Column<int>(type: "integer", nullable: true),
                    reversed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_applied_adjustments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    gateway_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_refunds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_audit_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    event_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    raw_payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    raw_payload_json = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_callback_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    event_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    result = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_callback_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "peak_hour_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    days_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    end_time = table.Column<TimeSpan>(type: "interval", nullable: false),
                    surcharge_fixed = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    surcharge_percent = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_peak_hour_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pet_albums",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    visibility = table.Column<int>(type: "integer", nullable: false),
                    cover_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_albums", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pet_allergies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allergen = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    reaction = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    noted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_allergies", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_allergies_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_diet_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    food = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    frequency = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_diet_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_diet_entries_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_emergency_card_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_emergency_card_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_emergency_card_links_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_follows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_follows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pet_lab_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reference_range = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    flag = table.Column<int>(type: "integer", nullable: false),
                    taken_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    document_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ocr_text = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_lab_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_lab_results_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prescription_seals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prescription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sealed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prescription_seals", x => x.id);
                    table.ForeignKey(
                        name: "fk_prescription_seals_prescriptions_prescription_id",
                        column: x => x.prescription_id,
                        principalTable: "prescriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_bundles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
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
                    table.PrimaryKey("pk_product_bundles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_stock_thresholds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    threshold = table.Column<int>(type: "integer", nullable: false),
                    last_alerted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_stock_thresholds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    scope_ref_id = table.Column<Guid>(type: "uuid", nullable: true),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    buy_quantity = table.Column<int>(type: "integer", nullable: false),
                    get_quantity = table.Column<int>(type: "integer", nullable: false),
                    min_subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usage_limit = table.Column<int>(type: "integer", nullable: true),
                    used_count = table.Column<int>(type: "integer", nullable: false),
                    stackable = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "questionnaire_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    questions_json = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("pk_questionnaire_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_questionnaire_templates_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_share_consents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grantee_doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    referral_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_record_share_consents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "referral_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referral_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "referral_redemptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    referrer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referred_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    redeemed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rewarded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referral_redemptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "replenishment_reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expected_run_out_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reminded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dismissed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_replenishment_reminders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reputation_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    community_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reputation_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "return_pickup_checkpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_pickup_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_return_pickup_checkpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "return_pickups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_window_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scheduled_window_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    address_snapshot = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    delivery_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    picked_up_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    proof_photo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    proof_signature_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reschedule_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_return_pickups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "security_signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    details_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_signals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sla_breaches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    breached_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_breaches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sponsored_placements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    budget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    spent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    bid_weight = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    impressions = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsored_placements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_e2ee_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_key_jwk = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_e2ee_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_reputations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    community_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_reputations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vaccination_certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    issued_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vaccination_certificates", x => x.id);
                    table.ForeignKey(
                        name: "fk_vaccination_certificates_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "care_plan_template_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    offset_days = table.Column<int>(type: "integer", nullable: false),
                    repeat_every_days = table.Column<int>(type: "integer", nullable: true),
                    repeat_count = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_care_plan_template_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_care_plan_template_items_care_plan_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "care_plan_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_care_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_custom = table.Column<bool>(type: "boolean", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_care_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_care_plans_care_plan_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "care_plan_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_pet_care_plans_pets_pet_id",
                        column: x => x.pet_id,
                        principalTable: "pets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "encounter_amendments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    amended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amended_by_doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signature_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encounter_amendments", x => x.id);
                    table.ForeignKey(
                        name: "fk_encounter_amendments_clinical_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalTable: "clinical_encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "encounter_diagnosis_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    system = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encounter_diagnosis_codes", x => x.id);
                    table.ForeignKey(
                        name: "fk_encounter_diagnosis_codes_clinical_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalTable: "clinical_encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "encounter_lab_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reference_range = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    flag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    document_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encounter_lab_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_encounter_lab_results_clinical_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalTable: "clinical_encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "referrals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_doctor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_clinic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    consent_to_share_records = table.Column<bool>(type: "boolean", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referrals", x => x.id);
                    table.ForeignKey(
                        name: "fk_referrals_clinical_encounters_encounter_id",
                        column: x => x.encounter_id,
                        principalTable: "clinical_encounters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_poll_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_poll_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_community_poll_options_community_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "community_polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversation_folder_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_folder_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversation_folder_items_conversation_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "conversation_folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exposure_report_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exposure_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exposure_report_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_exposure_report_events_exposure_reports_exposure_report_id",
                        column: x => x.exposure_report_id,
                        principalTable: "exposure_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pet_album_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    album_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    taken_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_album_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_album_photos_pet_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "pet_albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_bundle_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bundle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_bundle_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_bundle_items_product_bundles_bundle_id",
                        column: x => x.bundle_id,
                        principalTable: "product_bundles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointment_questionnaire_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answers_json = table.Column<string>(type: "jsonb", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_questionnaire_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointment_questionnaire_responses_appointments_appointmen",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_appointment_questionnaire_responses_questionnaire_template_",
                        column: x => x.template_id,
                        principalTable: "questionnaire_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pet_care_plan_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    linked_record_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    linked_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_notified_stage = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pet_care_plan_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_pet_care_plan_items_pet_care_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "pet_care_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending_next_attempt",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at" },
                filter: "status = 0");

            migrationBuilder.CreateIndex(
                name: "ix_adopter_profiles_user_id",
                table: "adopter_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_adoption_follow_ups_adopter_id",
                table: "adoption_follow_ups",
                column: "adopter_id");

            migrationBuilder.CreateIndex(
                name: "ix_adoption_follow_ups_listing_id_day_offset",
                table: "adoption_follow_ups",
                columns: new[] { "listing_id", "day_offset" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_adoption_follow_ups_status_due_at",
                table: "adoption_follow_ups",
                columns: new[] { "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_adoption_home_visits_request_id",
                table: "adoption_home_visits",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_adoption_listing_risk_flags_listing_id",
                table: "adoption_listing_risk_flags",
                column: "listing_id");

            migrationBuilder.CreateIndex(
                name: "ix_adoption_references_request_id",
                table: "adoption_references",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_adoption_request_stage_events_request_id_entered_at",
                table: "adoption_request_stage_events",
                columns: new[] { "request_id", "entered_at" });

            migrationBuilder.CreateIndex(
                name: "ix_adoption_request_stage_events_stage_due_at",
                table: "adoption_request_stage_events",
                columns: new[] { "stage", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_deposits_appointment_id",
                table: "appointment_deposits",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointment_follow_ups_follow_up_appointment_id",
                table: "appointment_follow_ups",
                column: "follow_up_appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_follow_ups_patient_user_id_status",
                table: "appointment_follow_ups",
                columns: new[] { "patient_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_follow_ups_source_appointment_id",
                table: "appointment_follow_ups",
                column: "source_appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_questionnaire_responses_appointment_id",
                table: "appointment_questionnaire_responses",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointment_questionnaire_responses_template_id",
                table: "appointment_questionnaire_responses",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_bundle_cart_discounts_user_id_bundle_id",
                table: "bundle_cart_discounts",
                columns: new[] { "user_id", "bundle_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendar_shares_owner_user_id",
                table: "calendar_shares",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_shares_token_hash",
                table: "calendar_shares",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_care_plan_template_items_template_id",
                table: "care_plan_template_items",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_care_plan_templates_is_published_species",
                table: "care_plan_templates",
                columns: new[] { "is_published", "species" });

            migrationBuilder.CreateIndex(
                name: "ix_care_plan_templates_published_by_user_id",
                table: "care_plan_templates",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_clinical_encounters_appointment_id",
                table: "clinical_encounters",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clinical_encounters_doctor_id_status",
                table: "clinical_encounters",
                columns: new[] { "doctor_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_poll_options_poll_id",
                table: "community_poll_options",
                column: "poll_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_poll_votes_option_id",
                table: "community_poll_votes",
                column: "option_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_poll_votes_poll_id_user_id",
                table: "community_poll_votes",
                columns: new[] { "poll_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_community_poll_votes_user_id_option_id",
                table: "community_poll_votes",
                columns: new[] { "user_id", "option_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_polls_community_id_created_at",
                table: "community_polls",
                columns: new[] { "community_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_folder_items_folder_id_conversation_id",
                table: "conversation_folder_items",
                columns: new[] { "folder_id", "conversation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversation_folders_user_id",
                table: "conversation_folders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_pins_conversation_id_message_id",
                table: "conversation_pins",
                columns: new[] { "conversation_id", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversation_settings_conversation_id",
                table: "conversation_settings",
                column: "conversation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_records_status_first_failed_at",
                table: "dead_letter_records",
                columns: new[] { "status", "first_failed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_records_topic_partition_offset_consumer_group",
                table: "dead_letter_records",
                columns: new[] { "topic", "partition", "offset", "consumer_group" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_zones_store_id_is_active",
                table: "delivery_zones",
                columns: new[] { "store_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_doctor_booking_policies_doctor_id",
                table: "doctor_booking_policies",
                column: "doctor_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_doctor_emergency_profiles_doctor_id",
                table: "doctor_emergency_profiles",
                column: "doctor_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_doctor_emergency_profiles_is24x7_city",
                table: "doctor_emergency_profiles",
                columns: new[] { "is24x7", "city" });

            migrationBuilder.CreateIndex(
                name: "ix_encounter_amendments_encounter_id_version",
                table: "encounter_amendments",
                columns: new[] { "encounter_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_encounter_completion_overrides_appointment_id",
                table: "encounter_completion_overrides",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_encounter_diagnosis_codes_encounter_id",
                table: "encounter_diagnosis_codes",
                column: "encounter_id");

            migrationBuilder.CreateIndex(
                name: "ix_encounter_diagnosis_codes_system_code",
                table: "encounter_diagnosis_codes",
                columns: new[] { "system", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_encounter_lab_results_encounter_id",
                table: "encounter_lab_results",
                column: "encounter_id");

            migrationBuilder.CreateIndex(
                name: "ix_exposure_report_events_exposure_report_id_at",
                table: "exposure_report_events",
                columns: new[] { "exposure_report_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_exposure_reports_pet_id_created_at",
                table: "exposure_reports",
                columns: new[] { "pet_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_foster_placements_foster_user_id",
                table: "foster_placements",
                column: "foster_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_foster_placements_listing_id",
                table: "foster_placements",
                column: "listing_id");

            migrationBuilder.CreateIndex(
                name: "ix_fulfillment_slas_store_id",
                table: "fulfillment_slas",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_card_transactions_gift_card_id",
                table: "gift_card_transactions",
                column: "gift_card_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_card_transactions_order_id_gift_card_id_type",
                table: "gift_card_transactions",
                columns: new[] { "order_id", "gift_card_id", "type" },
                unique: true,
                filter: "order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_gift_cards_code",
                table: "gift_cards",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gift_cards_code_hash",
                table: "gift_cards",
                column: "code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_record_seals_pet_id",
                table: "health_record_seals",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_health_record_seals_record_type_record_id",
                table: "health_record_seals",
                columns: new[] { "record_type", "record_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_health_record_versions_record_type_record_id_version",
                table: "health_record_versions",
                columns: new[] { "record_type", "record_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_expires_at",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_user_id_key_method_path",
                table: "idempotency_records",
                columns: new[] { "user_id", "key", "method", "path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_livestock_treatments_batch_id",
                table: "livestock_treatments",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_livestock_treatments_owner_id_administered_on",
                table: "livestock_treatments",
                columns: new[] { "owner_id", "administered_on" });

            migrationBuilder.CreateIndex(
                name: "ix_livestock_treatments_pet_group_id",
                table: "livestock_treatments",
                column: "pet_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_livestock_treatments_pet_id",
                table: "livestock_treatments",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_lost_found_alert_subscriptions_active_latitude_longitude",
                table: "lost_found_alert_subscriptions",
                columns: new[] { "active", "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_lost_found_alert_subscriptions_user_id",
                table: "lost_found_alert_subscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_lost_found_detail_extras_microchip_number",
                table: "lost_found_detail_extras",
                column: "microchip_number");

            migrationBuilder.CreateIndex(
                name: "ix_lost_found_detail_extras_report_id",
                table: "lost_found_detail_extras",
                column: "report_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lost_found_evidence_report_id",
                table: "lost_found_evidence",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_lost_found_sightings_report_id_created_at",
                table: "lost_found_sightings",
                columns: new[] { "report_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_accounts_user_id",
                table: "loyalty_accounts",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_transactions_order_id_type",
                table: "loyalty_transactions",
                columns: new[] { "order_id", "type" },
                unique: true,
                filter: "order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_transactions_user_id_created_at",
                table: "loyalty_transactions",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_message_edits_message_id",
                table: "message_edits",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_digest_preferences_user_id_category",
                table: "notification_digest_preferences",
                columns: new[] { "user_id", "category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_mutes_user_id_scope_scope_id",
                table: "notification_mutes",
                columns: new[] { "user_id", "scope", "scope_id" });

            migrationBuilder.CreateIndex(
                name: "ix_order_applied_adjustments_order_id",
                table: "order_applied_adjustments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_refunds_order_id",
                table: "order_refunds",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_refunds_order_item_id",
                table: "order_refunds",
                column: "order_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_audit_entries_event_type_created_at",
                table: "payment_audit_entries",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_audit_entries_order_id_created_at",
                table: "payment_audit_entries",
                columns: new[] { "order_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_audit_entries_payment_id",
                table: "payment_audit_entries",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_callback_logs_order_id",
                table: "payment_callback_logs",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_callback_logs_provider_transaction_id_event_type",
                table: "payment_callback_logs",
                columns: new[] { "provider", "transaction_id", "event_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_peak_hour_rules_zone_id_is_active",
                table: "peak_hour_rules",
                columns: new[] { "zone_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_pet_album_photos_album_id_sort_order",
                table: "pet_album_photos",
                columns: new[] { "album_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_pet_album_photos_created_at",
                table: "pet_album_photos",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_pet_albums_pet_id",
                table: "pet_albums",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_allergies_allergen",
                table: "pet_allergies",
                column: "allergen");

            migrationBuilder.CreateIndex(
                name: "ix_pet_allergies_pet_id_active",
                table: "pet_allergies",
                columns: new[] { "pet_id", "active" });

            migrationBuilder.CreateIndex(
                name: "ix_pet_care_plan_items_pet_id_status_due_date",
                table: "pet_care_plan_items",
                columns: new[] { "pet_id", "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_pet_care_plan_items_plan_id",
                table: "pet_care_plan_items",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_care_plan_items_status_due_date",
                table: "pet_care_plan_items",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_pet_care_plans_pet_id_status",
                table: "pet_care_plans",
                columns: new[] { "pet_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_pet_care_plans_template_id",
                table: "pet_care_plans",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_diet_entries_pet_id_active",
                table: "pet_diet_entries",
                columns: new[] { "pet_id", "active" });

            migrationBuilder.CreateIndex(
                name: "ix_pet_emergency_card_links_pet_id",
                table: "pet_emergency_card_links",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_emergency_card_links_token",
                table: "pet_emergency_card_links",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pet_follows_pet_id",
                table: "pet_follows",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_pet_follows_user_id_pet_id",
                table: "pet_follows",
                columns: new[] { "user_id", "pet_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pet_lab_results_pet_id_taken_at",
                table: "pet_lab_results",
                columns: new[] { "pet_id", "taken_at" });

            migrationBuilder.CreateIndex(
                name: "ix_post_locations_city",
                table: "post_locations",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "ix_post_locations_latitude_longitude",
                table: "post_locations",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_post_locations_post_id",
                table: "post_locations",
                column: "post_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prescription_seals_encounter_id",
                table: "prescription_seals",
                column: "encounter_id");

            migrationBuilder.CreateIndex(
                name: "ix_prescription_seals_prescription_id",
                table: "prescription_seals",
                column: "prescription_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_bundle_items_bundle_id",
                table: "product_bundle_items",
                column: "bundle_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_bundle_items_product_id",
                table: "product_bundle_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_bundles_store_id",
                table: "product_bundles",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_stock_thresholds_product_id_variant_id",
                table: "product_stock_thresholds",
                columns: new[] { "product_id", "variant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_promotions_is_active_starts_at_ends_at",
                table: "promotions",
                columns: new[] { "is_active", "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_questionnaire_templates_doctor_id_is_active",
                table: "questionnaire_templates",
                columns: new[] { "doctor_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_record_share_consents_pet_id_grantee_doctor_id",
                table: "record_share_consents",
                columns: new[] { "pet_id", "grantee_doctor_id" });

            migrationBuilder.CreateIndex(
                name: "ix_referral_codes_code",
                table: "referral_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_referral_codes_user_id",
                table: "referral_codes",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_referral_redemptions_referred_user_id",
                table: "referral_redemptions",
                column: "referred_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_referral_redemptions_referrer_user_id",
                table: "referral_redemptions",
                column: "referrer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_referrals_encounter_id",
                table: "referrals",
                column: "encounter_id");

            migrationBuilder.CreateIndex(
                name: "ix_referrals_to_doctor_id_status",
                table: "referrals",
                columns: new[] { "to_doctor_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_replenishment_reminders_expected_run_out_at",
                table: "replenishment_reminders",
                column: "expected_run_out_at");

            migrationBuilder.CreateIndex(
                name: "ix_replenishment_reminders_user_id_product_id",
                table: "replenishment_reminders",
                columns: new[] { "user_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reputation_events_community_id",
                table: "reputation_events",
                column: "community_id");

            migrationBuilder.CreateIndex(
                name: "ix_reputation_events_user_id_created_at",
                table: "reputation_events",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reputation_events_user_id_kind_source_id",
                table: "reputation_events",
                columns: new[] { "user_id", "kind", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_return_pickup_checkpoints_return_pickup_id_occurred_at",
                table: "return_pickup_checkpoints",
                columns: new[] { "return_pickup_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_return_pickups_delivery_user_id_status",
                table: "return_pickups",
                columns: new[] { "delivery_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_return_pickups_return_request_id",
                table: "return_pickups",
                column: "return_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_security_signals_created_at",
                table: "security_signals",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_security_signals_type_created_at",
                table: "security_signals",
                columns: new[] { "type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_security_signals_user_id",
                table: "security_signals",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sla_breaches_order_id_store_id_kind",
                table: "sla_breaches",
                columns: new[] { "order_id", "store_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sla_breaches_store_id_breached_at",
                table: "sla_breaches",
                columns: new[] { "store_id", "breached_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sponsored_placements_product_id",
                table: "sponsored_placements",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsored_placements_status_is_active_starts_at_ends_at",
                table: "sponsored_placements",
                columns: new[] { "status", "is_active", "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sponsored_placements_store_id",
                table: "sponsored_placements",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_e2ee_keys_user_id",
                table: "user_e2ee_keys",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_reputations_community_id_total",
                table: "user_reputations",
                columns: new[] { "community_id", "total" });

            migrationBuilder.CreateIndex(
                name: "ix_user_reputations_user_id_community_id",
                table: "user_reputations",
                columns: new[] { "user_id", "community_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vaccination_certificates_pet_id",
                table: "vaccination_certificates",
                column: "pet_id");

            migrationBuilder.CreateIndex(
                name: "ix_vaccination_certificates_token",
                table: "vaccination_certificates",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adopter_profiles");

            migrationBuilder.DropTable(
                name: "adoption_follow_ups");

            migrationBuilder.DropTable(
                name: "adoption_home_visits");

            migrationBuilder.DropTable(
                name: "adoption_listing_risk_flags");

            migrationBuilder.DropTable(
                name: "adoption_references");

            migrationBuilder.DropTable(
                name: "adoption_request_stage_events");

            migrationBuilder.DropTable(
                name: "appointment_deposits");

            migrationBuilder.DropTable(
                name: "appointment_follow_ups");

            migrationBuilder.DropTable(
                name: "appointment_questionnaire_responses");

            migrationBuilder.DropTable(
                name: "bundle_cart_discounts");

            migrationBuilder.DropTable(
                name: "calendar_shares");

            migrationBuilder.DropTable(
                name: "care_plan_template_items");

            migrationBuilder.DropTable(
                name: "community_poll_options");

            migrationBuilder.DropTable(
                name: "community_poll_votes");

            migrationBuilder.DropTable(
                name: "conversation_folder_items");

            migrationBuilder.DropTable(
                name: "conversation_pins");

            migrationBuilder.DropTable(
                name: "conversation_settings");

            migrationBuilder.DropTable(
                name: "dead_letter_records");

            migrationBuilder.DropTable(
                name: "delivery_zones");

            migrationBuilder.DropTable(
                name: "doctor_booking_policies");

            migrationBuilder.DropTable(
                name: "doctor_emergency_profiles");

            migrationBuilder.DropTable(
                name: "encounter_amendments");

            migrationBuilder.DropTable(
                name: "encounter_completion_overrides");

            migrationBuilder.DropTable(
                name: "encounter_diagnosis_codes");

            migrationBuilder.DropTable(
                name: "encounter_lab_results");

            migrationBuilder.DropTable(
                name: "exposure_report_events");

            migrationBuilder.DropTable(
                name: "foster_placements");

            migrationBuilder.DropTable(
                name: "fulfillment_slas");

            migrationBuilder.DropTable(
                name: "gift_card_transactions");

            migrationBuilder.DropTable(
                name: "gift_cards");

            migrationBuilder.DropTable(
                name: "health_record_seals");

            migrationBuilder.DropTable(
                name: "health_record_versions");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "livestock_treatments");

            migrationBuilder.DropTable(
                name: "lost_found_alert_subscriptions");

            migrationBuilder.DropTable(
                name: "lost_found_detail_extras");

            migrationBuilder.DropTable(
                name: "lost_found_evidence");

            migrationBuilder.DropTable(
                name: "lost_found_sightings");

            migrationBuilder.DropTable(
                name: "loyalty_accounts");

            migrationBuilder.DropTable(
                name: "loyalty_transactions");

            migrationBuilder.DropTable(
                name: "message_edits");

            migrationBuilder.DropTable(
                name: "notification_digest_preferences");

            migrationBuilder.DropTable(
                name: "notification_mutes");

            migrationBuilder.DropTable(
                name: "order_applied_adjustments");

            migrationBuilder.DropTable(
                name: "order_refunds");

            migrationBuilder.DropTable(
                name: "payment_audit_entries");

            migrationBuilder.DropTable(
                name: "payment_callback_logs");

            migrationBuilder.DropTable(
                name: "peak_hour_rules");

            migrationBuilder.DropTable(
                name: "pet_album_photos");

            migrationBuilder.DropTable(
                name: "pet_allergies");

            migrationBuilder.DropTable(
                name: "pet_care_plan_items");

            migrationBuilder.DropTable(
                name: "pet_diet_entries");

            migrationBuilder.DropTable(
                name: "pet_emergency_card_links");

            migrationBuilder.DropTable(
                name: "pet_follows");

            migrationBuilder.DropTable(
                name: "pet_lab_results");

            migrationBuilder.DropTable(
                name: "post_locations");

            migrationBuilder.DropTable(
                name: "prescription_seals");

            migrationBuilder.DropTable(
                name: "product_bundle_items");

            migrationBuilder.DropTable(
                name: "product_stock_thresholds");

            migrationBuilder.DropTable(
                name: "promotions");

            migrationBuilder.DropTable(
                name: "record_share_consents");

            migrationBuilder.DropTable(
                name: "referral_codes");

            migrationBuilder.DropTable(
                name: "referral_redemptions");

            migrationBuilder.DropTable(
                name: "referrals");

            migrationBuilder.DropTable(
                name: "replenishment_reminders");

            migrationBuilder.DropTable(
                name: "reputation_events");

            migrationBuilder.DropTable(
                name: "return_pickup_checkpoints");

            migrationBuilder.DropTable(
                name: "return_pickups");

            migrationBuilder.DropTable(
                name: "security_signals");

            migrationBuilder.DropTable(
                name: "sla_breaches");

            migrationBuilder.DropTable(
                name: "sponsored_placements");

            migrationBuilder.DropTable(
                name: "user_e2ee_keys");

            migrationBuilder.DropTable(
                name: "user_reputations");

            migrationBuilder.DropTable(
                name: "vaccination_certificates");

            migrationBuilder.DropTable(
                name: "questionnaire_templates");

            migrationBuilder.DropTable(
                name: "community_polls");

            migrationBuilder.DropTable(
                name: "conversation_folders");

            migrationBuilder.DropTable(
                name: "exposure_reports");

            migrationBuilder.DropTable(
                name: "pet_albums");

            migrationBuilder.DropTable(
                name: "pet_care_plans");

            migrationBuilder.DropTable(
                name: "product_bundles");

            migrationBuilder.DropTable(
                name: "clinical_encounters");

            migrationBuilder.DropTable(
                name: "care_plan_templates");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_pending_next_attempt",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "claimed_by",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "claimed_until",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt_at",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at" });
        }
    }
}
