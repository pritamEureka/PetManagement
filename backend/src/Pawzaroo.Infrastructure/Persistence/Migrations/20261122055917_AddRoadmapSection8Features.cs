using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pawzaroo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoadmapSection8Features : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false),
                    occurrences = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_series", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointment_series_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointment_waitlist_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    preferred_from = table.Column<DateOnly>(type: "date", nullable: true),
                    preferred_to = table.Column<DateOnly>(type: "date", nullable: true),
                    consultation_type = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_waitlist_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointment_waitlist_entries_doctors_doctor_id",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assistant_conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    has_escalation = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assistant_conversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backup_recovery_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backup_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cart_item_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cart_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_item_variants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "communities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    species = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    breed = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_communities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "community_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    community_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    venue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "community_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    community_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_members", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consent_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    granted = table.Column<bool>(type: "boolean", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "consultation_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_doctor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subjective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    objective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    assessment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    plan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consultation_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_consultation_notes_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_consultation_notes_doctors_author_doctor_id",
                        column: x => x.author_doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_checkpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    location_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    external_event_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_checkpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_proofs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    photo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    signature_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    otp_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    captured_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_proofs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_rsvps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rsvp_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_rsvps", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    available_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    memo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    in_app_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    push_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_quiet_hours",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    start_minute = table.Column<int>(type: "integer", nullable: false),
                    end_minute = table.Column<int>(type: "integer", nullable: false),
                    timezone_offset_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_quiet_hours", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "price_watches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    baseline_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    target_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    last_notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_price_watches", x => x.id);
                    table.ForeignKey(
                        name: "fk_price_watches_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    interval_days = table.Column<int>(type: "integer", nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    failure_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    shipping_address_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_subscriptions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    attributes_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    price_override = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    stock_quantity = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_product_variants", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "push_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    p256dh = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    auth = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_agent = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_push_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recently_viewed_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recently_viewed_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saved_searches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    query_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    alert_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_searches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settlement_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payout_count = table.Column<int>(type: "integer", nullable: false),
                    cutoff_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reconciled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settlement_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "step_up_verifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_step_up_verifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_stock_reservations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "store_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "store_payout_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_payout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_payout_lines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "telemedicine_rooms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    join_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    opens_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consent_given = table.Column<bool>(type: "boolean", nullable: false),
                    consent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pre_consult_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    patient_checked_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    patient_admitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telemedicine_rooms", x => x.id);
                    table.ForeignKey(
                        name: "fk_telemedicine_rooms_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointment_series_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_series_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointment_series_items_appointment_series_series_id",
                        column: x => x.series_id,
                        principalTable: "appointment_series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_appointment_series_items_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assistant_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    citations_json = table.Column<string>(type: "jsonb", nullable: true),
                    escalated = table.Column<bool>(type: "boolean", nullable: false),
                    intent = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assistant_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_assistant_messages_assistant_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "assistant_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_payouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    settlement_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    gateway_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    gateway_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reconciled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_payouts", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_payouts_settlement_batches_settlement_batch_id",
                        column: x => x.settlement_batch_id,
                        principalTable: "settlement_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telemedicine_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telemedicine_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_telemedicine_events_telemedicine_room_room_id",
                        column: x => x.room_id,
                        principalTable: "telemedicine_rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_series_doctor_id",
                table: "appointment_series",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_series_patient_user_id",
                table: "appointment_series",
                column: "patient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_series_items_appointment_id",
                table: "appointment_series_items",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointment_series_items_series_id",
                table: "appointment_series_items",
                column: "series_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_waitlist_entries_doctor_id_status_created_at",
                table: "appointment_waitlist_entries",
                columns: new[] { "doctor_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_waitlist_entries_user_id",
                table: "appointment_waitlist_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assistant_conversations_user_id_last_message_at",
                table: "assistant_conversations",
                columns: new[] { "user_id", "last_message_at" });

            migrationBuilder.CreateIndex(
                name: "ix_assistant_messages_conversation_id_created_at",
                table: "assistant_messages",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_backup_recovery_codes_user_id_code_hash",
                table: "backup_recovery_codes",
                columns: new[] { "user_id", "code_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_cart_item_variants_cart_item_id",
                table: "cart_item_variants",
                column: "cart_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_communities_slug",
                table: "communities",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_communities_species_breed",
                table: "communities",
                columns: new[] { "species", "breed" });

            migrationBuilder.CreateIndex(
                name: "ix_community_events_community_id_starts_at",
                table: "community_events",
                columns: new[] { "community_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_community_events_is_cancelled_starts_at_latitude_longitude",
                table: "community_events",
                columns: new[] { "is_cancelled", "starts_at", "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_community_members_community_id_user_id",
                table: "community_members",
                columns: new[] { "community_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_members_user_id_status",
                table: "community_members",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_user_id_recorded_at",
                table: "consent_records",
                columns: new[] { "user_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_consultation_notes_appointment_id",
                table: "consultation_notes",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_consultation_notes_author_doctor_id",
                table: "consultation_notes",
                column: "author_doctor_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_checkpoints_order_id_occurred_at",
                table: "delivery_checkpoints",
                columns: new[] { "order_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_checkpoints_provider_external_event_id",
                table: "delivery_checkpoints",
                columns: new[] { "provider", "external_event_id" },
                unique: true,
                filter: "external_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_proofs_order_id",
                table: "delivery_proofs",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_rsvps_event_id_status_rsvp_at",
                table: "event_rsvps",
                columns: new[] { "event_id", "status", "rsvp_at" });

            migrationBuilder.CreateIndex(
                name: "ix_event_rsvps_event_id_user_id",
                table: "event_rsvps",
                columns: new[] { "event_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_order_id",
                table: "ledger_entries",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_order_item_id_type",
                table: "ledger_entries",
                columns: new[] { "order_item_id", "type" },
                unique: true,
                filter: "order_item_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_store_id_available_at",
                table: "ledger_entries",
                columns: new[] { "store_id", "available_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_store_id_created_at",
                table: "ledger_entries",
                columns: new[] { "store_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_preferences_user_id_category",
                table: "notification_preferences",
                columns: new[] { "user_id", "category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_quiet_hours_user_id",
                table: "notification_quiet_hours",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_price_watches_is_active",
                table: "price_watches",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_price_watches_product_id",
                table: "price_watches",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_watches_user_id_product_id",
                table: "price_watches",
                columns: new[] { "user_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_subscriptions_product_id",
                table: "product_subscriptions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_subscriptions_status_next_run_at",
                table: "product_subscriptions",
                columns: new[] { "status", "next_run_at" });

            migrationBuilder.CreateIndex(
                name: "ix_product_subscriptions_user_id",
                table: "product_subscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_product_id_sku",
                table: "product_variants",
                columns: new[] { "product_id", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_endpoint",
                table: "push_subscriptions",
                column: "endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_user_id",
                table: "push_subscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_recently_viewed_items_user_id_product_id",
                table: "recently_viewed_items",
                columns: new[] { "user_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recently_viewed_items_user_id_viewed_at",
                table: "recently_viewed_items",
                columns: new[] { "user_id", "viewed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_saved_searches_alert_enabled",
                table: "saved_searches",
                column: "alert_enabled");

            migrationBuilder.CreateIndex(
                name: "ix_saved_searches_user_id",
                table: "saved_searches",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_settlement_batches_created_at",
                table: "settlement_batches",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_settlement_batches_reference",
                table: "settlement_batches",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_step_up_verifications_user_id_verified_at",
                table: "step_up_verifications",
                columns: new[] { "user_id", "verified_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_product_id_variant_id",
                table: "stock_reservations",
                columns: new[] { "product_id", "variant_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_status_expires_at",
                table: "stock_reservations",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_user_id",
                table: "stock_reservations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_locations_store_id",
                table: "store_locations",
                column: "store_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_payout_lines_ledger_entry_id",
                table: "store_payout_lines",
                column: "ledger_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_payout_lines_store_payout_id",
                table: "store_payout_lines",
                column: "store_payout_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_payouts_settlement_batch_id_store_id",
                table: "store_payouts",
                columns: new[] { "settlement_batch_id", "store_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_payouts_store_id_created_at",
                table: "store_payouts",
                columns: new[] { "store_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_events_room_id_at",
                table: "telemedicine_events",
                columns: new[] { "room_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_rooms_appointment_id",
                table: "telemedicine_rooms",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_telemedicine_rooms_closed_at_expires_at",
                table: "telemedicine_rooms",
                columns: new[] { "closed_at", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_series_items");

            migrationBuilder.DropTable(
                name: "appointment_waitlist_entries");

            migrationBuilder.DropTable(
                name: "assistant_messages");

            migrationBuilder.DropTable(
                name: "backup_recovery_codes");

            migrationBuilder.DropTable(
                name: "cart_item_variants");

            migrationBuilder.DropTable(
                name: "communities");

            migrationBuilder.DropTable(
                name: "community_events");

            migrationBuilder.DropTable(
                name: "community_members");

            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "consultation_notes");

            migrationBuilder.DropTable(
                name: "delivery_checkpoints");

            migrationBuilder.DropTable(
                name: "delivery_proofs");

            migrationBuilder.DropTable(
                name: "event_rsvps");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notification_quiet_hours");

            migrationBuilder.DropTable(
                name: "price_watches");

            migrationBuilder.DropTable(
                name: "product_subscriptions");

            migrationBuilder.DropTable(
                name: "product_variants");

            migrationBuilder.DropTable(
                name: "push_subscriptions");

            migrationBuilder.DropTable(
                name: "recently_viewed_items");

            migrationBuilder.DropTable(
                name: "saved_searches");

            migrationBuilder.DropTable(
                name: "step_up_verifications");

            migrationBuilder.DropTable(
                name: "stock_reservations");

            migrationBuilder.DropTable(
                name: "store_locations");

            migrationBuilder.DropTable(
                name: "store_payout_lines");

            migrationBuilder.DropTable(
                name: "store_payouts");

            migrationBuilder.DropTable(
                name: "telemedicine_events");

            migrationBuilder.DropTable(
                name: "appointment_series");

            migrationBuilder.DropTable(
                name: "assistant_conversations");

            migrationBuilder.DropTable(
                name: "settlement_batches");

            migrationBuilder.DropTable(
                name: "telemedicine_rooms");
        }
    }
}
