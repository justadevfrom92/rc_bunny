using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RcBunny.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    actor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    subject = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "byop_products",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    step = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    rate_adj = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    monthly = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_byop_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contact_messages",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    first_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    topic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    handled_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    handled_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "markets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    @short = table.Column<string>(name: "short", type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    region = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_markets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plan_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    slug = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    type = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    term = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    @internal = table.Column<string>(name: "internal", type: "nvarchar(450)", nullable: false),
                    slug = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    rolloff = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    etf = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    mrc = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    green = table.Column<int>(type: "int", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    perks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    perms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "term_discounts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    term = table.Column<int>(type: "int", nullable: false),
                    region = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    discount = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_term_discounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "term_etfs",
                columns: table => new
                {
                    term = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_term_etfs", x => x.term);
                });

            migrationBuilder.CreateTable(
                name: "tdsp_fees",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    market_id = table.Column<int>(type: "int", nullable: false),
                    per_kwh = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    per_bill = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    effective_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tdsp_fees", x => x.id);
                    table.ForeignKey(
                        name: "fk_tdsp_fees_markets_market_id",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zip_ranges",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    zip_from = table.Column<int>(type: "int", nullable: false),
                    zip_to = table.Column<int>(type: "int", nullable: false),
                    market_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zip_ranges", x => x.id);
                    table.ForeignKey(
                        name: "fk_zip_ranges_markets_market_id",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ticket = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    first_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    last_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    city = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    zip = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    market_id = table.Column<int>(type: "int", nullable: true),
                    esiid = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    plan_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    exception = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    enrollment_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    balance = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    deposit_due = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    autopay = table.Column<bool>(type: "bit", nullable: false),
                    paperless = table.Column<bool>(type: "bit", nullable: false),
                    peak_perks = table.Column<bool>(type: "bit", nullable: false),
                    marketing_opt_in = table.Column<bool>(type: "bit", nullable: false),
                    stars = table.Column<int>(type: "int", nullable: false),
                    language = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    username = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    referral_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                    table.ForeignKey(
                        name: "fk_customers_markets_market_id",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_customers_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "plan_group_plan",
                columns: table => new
                {
                    plan_group_id = table.Column<int>(type: "int", nullable: false),
                    plan_id = table.Column<int>(type: "int", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_group_plan", x => new { x.plan_group_id, x.plan_id });
                    table.ForeignKey(
                        name: "fk_plan_group_plan_plan_groups_plan_group_id",
                        column: x => x.plan_group_id,
                        principalTable: "plan_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_plan_group_plan_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    plan_id = table.Column<int>(type: "int", nullable: false),
                    market_id = table.Column<int>(type: "int", nullable: false),
                    energy = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    effective_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rates", x => x.id);
                    table.ForeignKey(
                        name: "fk_rates_markets_market_id",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rates_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    role_id = table.Column<int>(type: "int", nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    company = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    access_ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    manager_id = table.Column<int>(type: "int", nullable: true),
                    phone_ext = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    hired_on = table.Column<DateOnly>(type: "date", nullable: true),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    extra_perms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    denied_perms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    access_note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    access_version = table.Column<int>(type: "int", nullable: false),
                    security_stamp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_users_users_manager_id",
                        column: x => x.manager_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "bills",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    reference = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    billed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    kwh = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    invoice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bills", x => x.id);
                    table.ForeignKey(
                        name: "fk_bills_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    author = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    disposition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    action = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    priority = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_notes_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    reference = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    confirmation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    reversed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    reversed_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bookmarks",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookmarks", x => new { x.user_id, x.customer_id });
                    table.ForeignKey(
                        name: "fk_bookmarks_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bookmarks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_created_at",
                table: "audit_entries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_user_id",
                table: "audit_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_bills_customer_id",
                table: "bills",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_bills_reference",
                table: "bills",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookmarks_customer_id",
                table: "bookmarks",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_byop_products_key",
                table: "byop_products",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_account",
                table: "customers",
                column: "account",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_esiid",
                table: "customers",
                column: "esiid");

            migrationBuilder.CreateIndex(
                name: "ix_customers_exception",
                table: "customers",
                column: "exception");

            migrationBuilder.CreateIndex(
                name: "ix_customers_market_id",
                table: "customers",
                column: "market_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_plan_id",
                table: "customers",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_status",
                table: "customers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_customers_ticket",
                table: "customers",
                column: "ticket",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_username",
                table: "customers",
                column: "username",
                unique: true,
                filter: "[username] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_markets_name",
                table: "markets",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notes_customer_id",
                table: "notes",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_customer_id",
                table: "payments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_reference",
                table: "payments",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plan_group_plan_plan_id",
                table: "plan_group_plan",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_groups_slug",
                table: "plan_groups",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plans_internal",
                table: "plans",
                column: "internal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rates_market_id",
                table: "rates",
                column: "market_id");

            migrationBuilder.CreateIndex(
                name: "ix_rates_plan_id_market_id_effective_on",
                table: "rates",
                columns: new[] { "plan_id", "market_id", "effective_on" });

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tdsp_fees_market_id_effective_on",
                table: "tdsp_fees",
                columns: new[] { "market_id", "effective_on" });

            migrationBuilder.CreateIndex(
                name: "ix_term_discounts_term_region",
                table: "term_discounts",
                columns: new[] { "term", "region" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_company",
                table: "users",
                column: "company");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_employee_id",
                table: "users",
                column: "employee_id",
                unique: true,
                filter: "[employee_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_manager_id",
                table: "users",
                column: "manager_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_zip_ranges_market_id",
                table: "zip_ranges",
                column: "market_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "bills");

            migrationBuilder.DropTable(
                name: "bookmarks");

            migrationBuilder.DropTable(
                name: "byop_products");

            migrationBuilder.DropTable(
                name: "contact_messages");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "plan_group_plan");

            migrationBuilder.DropTable(
                name: "rates");

            migrationBuilder.DropTable(
                name: "tdsp_fees");

            migrationBuilder.DropTable(
                name: "term_discounts");

            migrationBuilder.DropTable(
                name: "term_etfs");

            migrationBuilder.DropTable(
                name: "zip_ranges");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "plan_groups");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "markets");

            migrationBuilder.DropTable(
                name: "plans");
        }
    }
}
