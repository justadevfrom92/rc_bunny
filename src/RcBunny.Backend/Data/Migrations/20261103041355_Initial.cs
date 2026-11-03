using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RcBunny.Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_models",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    provider = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    model_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    source = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    size_gb = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    quantization = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    context_window = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    checked_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_models", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    actor = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    subject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "block_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_block_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "byop_products",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    key = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    model = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    step = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    rate_adj = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    monthly = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    min_discount = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    max_discount = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_byop_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_parts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    html = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    is_default = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_parts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "heartbeat_pulses",
                columns: table => new
                {
                    key = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    beat_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    meta = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heartbeat_pulses", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "heartbeats",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    checked_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    results = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    duration_ms = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heartbeats", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "marketing_channels",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    msid = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketing_channels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "markets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    @short = table.Column<string>(name: "short", type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    region = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    state = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    commodity = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    units = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    duns_number = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    edi_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_markets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pdf_parts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    subtitle = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    color = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_default = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_parts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plan_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    type = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    term = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    @internal = table.Column<string>(name: "internal", type: "nvarchar(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    rolloff = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    etf = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    mrc = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    green = table.Column<int>(type: "int", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    perks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "promo_codes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    credit = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: true),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promo_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reference_rows",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    table_key = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    cells = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reference_rows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reward_offers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    stars = table.Column<int>(type: "int", nullable: false),
                    effect = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    value = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reward_offers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reward_rules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    key = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    label = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    stars = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    per_dollar = table.Column<bool>(type: "bit", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reward_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    perms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shop_products",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    sku = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    cat = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    was = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    rating = table.Column<decimal>(type: "decimal(2,1)", precision: 2, scale: 1, nullable: false),
                    reviews = table.Column<int>(type: "int", nullable: false),
                    stars = table.Column<int>(type: "int", nullable: false),
                    stock = table.Column<bool>(type: "bit", nullable: false),
                    badge = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    icon = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    blurb = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sites",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    address_box_title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "survey_answer_sets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    group = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    options = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_answer_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "survey_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "surveys",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    intro = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    thank_you = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    opens_on = table.Column<DateOnly>(type: "date", nullable: true),
                    closes_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surveys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    file = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "term_discounts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    term = table.Column<int>(type: "int", nullable: false),
                    region = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    discount = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_term_discounts", x => x.id);
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
                    table.PrimaryKey("PK_term_etfs", x => x.term);
                });

            migrationBuilder.CreateTable(
                name: "ai_agents",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    activity = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ai_model_id = table.Column<int>(type: "int", nullable: false),
                    fallback_model_id = table.Column<int>(type: "int", nullable: true),
                    max_tokens = table.Column<int>(type: "int", nullable: false),
                    system_prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    temperature = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    schedule = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agents", x => x.id);
                    table.ForeignKey(
                        name: "ai_agents_ai_model_id_foreign",
                        column: x => x.ai_model_id,
                        principalTable: "ai_models",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "ai_agents_fallback_model_id_foreign",
                        column: x => x.fallback_model_id,
                        principalTable: "ai_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "email_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    subject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    email_category_id = table.Column<int>(type: "int", nullable: true),
                    email_header_id = table.Column<int>(type: "int", nullable: true),
                    email_footer_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_templates", x => x.id);
                    table.ForeignKey(
                        name: "email_templates_email_category_id_foreign",
                        column: x => x.email_category_id,
                        principalTable: "email_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "email_templates_email_footer_id_foreign",
                        column: x => x.email_footer_id,
                        principalTable: "email_parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "email_templates_email_header_id_foreign",
                        column: x => x.email_header_id,
                        principalTable: "email_parts",
                        principalColumn: "id");
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
                    table.PrimaryKey("PK_zip_ranges", x => x.id);
                    table.ForeignKey(
                        name: "zip_ranges_market_id_foreign",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pdf_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    category = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    footer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    show_address = table.Column<bool>(type: "bit", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    pdf_header_id = table.Column<int>(type: "int", nullable: true),
                    pdf_footer_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_templates", x => x.id);
                    table.ForeignKey(
                        name: "pdf_templates_pdf_footer_id_foreign",
                        column: x => x.pdf_footer_id,
                        principalTable: "pdf_parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "pdf_templates_pdf_header_id_foreign",
                        column: x => x.pdf_header_id,
                        principalTable: "pdf_parts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ticket = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    city = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    zip = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    market_id = table.Column<int>(type: "int", nullable: true),
                    esiid = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    plan_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    exception = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    source = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    enrollment_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    balance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    autopay = table.Column<bool>(type: "bit", nullable: false),
                    paperless = table.Column<bool>(type: "bit", nullable: false),
                    peak_perks = table.Column<bool>(type: "bit", nullable: false),
                    stars = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    first_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    last_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    phone_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    language = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    tec_score = table.Column<int>(type: "int", nullable: true),
                    ssn_last4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ip_location = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    unit = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    meter_number = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    load_profile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    load_zone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    meter_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    billing_street = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    billing_unit = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    billing_city = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    billing_state = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    billing_zip = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    move_switch = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    requested_start = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_start = table.Column<DateOnly>(type: "date", nullable: true),
                    status_info = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    rate_class = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    channel = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    msid = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    promo_code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    marketing_opt_in = table.Column<bool>(type: "bit", nullable: false),
                    authorized_users = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    linked_accounts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    remember_token = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    deposit_due = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    referral_code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    referred_by = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    deposit_held = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                    table.ForeignKey(
                        name: "customers_market_id_foreign",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "customers_plan_id_foreign",
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
                    table.PrimaryKey("PK_plan_group_plan", x => new { x.plan_group_id, x.plan_id });
                    table.ForeignKey(
                        name: "plan_group_plan_plan_group_id_foreign",
                        column: x => x.plan_group_id,
                        principalTable: "plan_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "plan_group_plan_plan_id_foreign",
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
                    role_id = table.Column<int>(type: "int", nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    email_verified_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    remember_token = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    employee_id = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    department = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    manager_id = table.Column<int>(type: "int", nullable: true),
                    phone_ext = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    location = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    hired_on = table.Column<DateOnly>(type: "date", nullable: true),
                    extra_perms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    denied_perms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    access_note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    company = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    access_ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    notification_prefs = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    access_version = table.Column<int>(type: "int", nullable: false),
                    security_stamp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "users_manager_id_foreign",
                        column: x => x.manager_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "users_role_id_foreign",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "survey_bank_questions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    survey_category_id = table.Column<int>(type: "int", nullable: true),
                    question = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    survey_answer_set_id = table.Column<int>(type: "int", nullable: true),
                    help = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_bank_questions", x => x.id);
                    table.ForeignKey(
                        name: "survey_bank_questions_survey_answer_set_id_foreign",
                        column: x => x.survey_answer_set_id,
                        principalTable: "survey_answer_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "survey_bank_questions_survey_category_id_foreign",
                        column: x => x.survey_category_id,
                        principalTable: "survey_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_automation_runs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ai_agent_id = table.Column<int>(type: "int", nullable: false),
                    ai_model_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    @checked = table.Column<int>(name: "checked", type: "int", nullable: false),
                    @fixed = table.Column<int>(name: "fixed", type: "int", nullable: false),
                    flagged = table.Column<int>(type: "int", nullable: false),
                    summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    duration_sec = table.Column<int>(type: "int", nullable: false),
                    input_tokens = table.Column<int>(type: "int", nullable: false),
                    output_tokens = table.Column<int>(type: "int", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_automation_runs", x => x.id);
                    table.ForeignKey(
                        name: "ai_automation_runs_ai_agent_id_foreign",
                        column: x => x.ai_agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ai_automation_runs_ai_model_id_foreign",
                        column: x => x.ai_model_id,
                        principalTable: "ai_models",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "api_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    api = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    action = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    response_ms = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    request = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    response = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_logs", x => x.id);
                    table.ForeignKey(
                        name: "api_logs_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bills",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    reference = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    billed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    kwh = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    bill_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true),
                    due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    amount_paid = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    balance_after = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    invoice = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ontime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bills", x => x.id);
                    table.ForeignKey(
                        name: "bills_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_files",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    kind = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    path = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    generated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    size = table.Column<int>(type: "int", nullable: true),
                    regenerated = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_files", x => x.id);
                    table.ForeignKey(
                        name: "customer_files_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_password_resets",
                columns: table => new
                {
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    token = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_password_resets", x => x.customer_id);
                    table.ForeignKey(
                        name: "customer_password_resets_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "data_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    model = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    record_id = table.Column<long>(type: "bigint", nullable: true),
                    summary = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_items", x => x.id);
                    table.ForeignKey(
                        name: "data_items_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ercot_transactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    esiid = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    trans_date = table.Column<DateOnly>(type: "date", nullable: false),
                    purpose = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    scheduled_on = table.Column<DateOnly>(type: "date", nullable: true),
                    trans_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    label = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    tracking = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ercot_transactions", x => x.id);
                    table.ForeignKey(
                        name: "ercot_transactions_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_methods",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    last4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    expires = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    nickname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    vendor = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    autopay = table.Column<bool>(type: "bit", nullable: false),
                    removed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_methods", x => x.id);
                    table.ForeignKey(
                        name: "payment_methods_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_terms",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    plan_id = table.Column<int>(type: "int", nullable: true),
                    rate_class = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    energy_charge = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    rate_2000 = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    ordered_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    contract_start = table.Column<DateOnly>(type: "date", nullable: true),
                    contract_end = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_terms", x => x.id);
                    table.ForeignKey(
                        name: "plan_terms_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "plan_terms_plan_id_foreign",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "service_addresses",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    esiid = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    street = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    city = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    zip = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ordered_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    service_start = table.Column<DateOnly>(type: "date", nullable: true),
                    service_end = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_addresses", x => x.id);
                    table.ForeignKey(
                        name: "service_addresses_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "signups",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    step = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signups", x => x.id);
                    table.ForeignKey(
                        name: "signups_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "survey_responses",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    survey_id = table.Column<int>(type: "int", nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    answers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    source = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_responses", x => x.id);
                    table.ForeignKey(
                        name: "survey_responses_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "survey_responses_survey_id_foreign",
                        column: x => x.survey_id,
                        principalTable: "surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admin_apps",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    key = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    icon = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    menu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    position = table.Column<int>(type: "int", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_apps", x => x.id);
                    table.ForeignKey(
                        name: "admin_apps_created_by_foreign",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "admin_notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    read_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_notifications", x => x.id);
                    table.ForeignKey(
                        name: "admin_notifications_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_conversations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ai_agent_id = table.Column<int>(type: "int", nullable: false),
                    ai_model_id = table.Column<int>(type: "int", nullable: true),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    channel = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    topic = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    outcome = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    handed_to_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    transcript = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    duration_sec = table.Column<int>(type: "int", nullable: false),
                    input_tokens = table.Column<int>(type: "int", nullable: false),
                    output_tokens = table.Column<int>(type: "int", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    last_message_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_conversations", x => x.id);
                    table.ForeignKey(
                        name: "ai_conversations_ai_agent_id_foreign",
                        column: x => x.ai_agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ai_conversations_ai_model_id_foreign",
                        column: x => x.ai_model_id,
                        principalTable: "ai_models",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "ai_conversations_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "ai_conversations_handed_to_id_foreign",
                        column: x => x.handed_to_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "ai_conversations_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "bookmarks",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookmarks", x => new { x.user_id, x.customer_id });
                    table.ForeignKey(
                        name: "bookmarks_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "bookmarks_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaigns",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    channel = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    email_template_id = table.Column<int>(type: "int", nullable: true),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    audience = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    recipients = table.Column<int>(type: "int", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    goal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    product = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    msid = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    promo_code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    scheduled_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cap_days = table.Column<int>(type: "int", nullable: false),
                    skipped = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.id);
                    table.ForeignKey(
                        name: "campaigns_created_by_foreign",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "campaigns_email_template_id_foreign",
                        column: x => x.email_template_id,
                        principalTable: "email_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "contact_messages",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    first_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    topic = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ip = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    handled_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    handled_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    reply = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    replied_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    replied_by = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_messages", x => x.id);
                    table.ForeignKey(
                        name: "contact_messages_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "contact_messages_handled_by_foreign",
                        column: x => x.handled_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "contact_messages_replied_by_foreign",
                        column: x => x.replied_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "content_blocks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    html = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    updated_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    slug = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    site_id = table.Column<int>(type: "int", nullable: true),
                    category_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_blocks", x => x.id);
                    table.ForeignKey(
                        name: "content_blocks_category_id_foreign",
                        column: x => x.category_id,
                        principalTable: "block_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "content_blocks_site_id_foreign",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "content_blocks_updated_by_foreign",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "cookie_consents",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    visitor = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    choice = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    analytics = table.Column<bool>(type: "bit", nullable: false),
                    marketing = table.Column<bool>(type: "bit", nullable: false),
                    policy_version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    page = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    cookie_jar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    cookies_seen_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cookie_consents", x => x.id);
                    table.ForeignKey(
                        name: "cookie_consents_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "cookie_consents_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "crm_partners",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    contact_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    city = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    commission_rate = table.Column<decimal>(type: "decimal(6,4)", precision: 6, scale: 4, nullable: false),
                    commission_basis = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    owner_id = table.Column<int>(type: "int", nullable: true),
                    agreement_ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_partners", x => x.id);
                    table.ForeignKey(
                        name: "crm_partners_owner_id_foreign",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "customer_flags",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    flag = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    author = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    removed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_flags", x => x.id);
                    table.ForeignKey(
                        name: "customer_flags_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "customer_flags_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "customer_products",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    product = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    removed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_products", x => x.id);
                    table.ForeignKey(
                        name: "customer_products_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "customer_products_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "email_suppressions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_suppressions", x => x.id);
                    table.ForeignKey(
                        name: "email_suppressions_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "email_suppressions_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "email_test_sends",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    email_template_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    mode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    target = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    recipients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    subject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    @params = table.Column<string>(name: "params", type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_test_sends", x => x.id);
                    table.ForeignKey(
                        name: "email_test_sends_email_template_id_foreign",
                        column: x => x.email_template_id,
                        principalTable: "email_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "email_test_sends_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "heartbeat_actions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    check_key = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    ok = table.Column<bool>(type: "bit", nullable: false),
                    output = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    duration_ms = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heartbeat_actions", x => x.id);
                    table.ForeignKey(
                        name: "heartbeat_actions_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "heartbeat_controls",
                columns: table => new
                {
                    key = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    state = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    changed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heartbeat_controls", x => x.key);
                    table.ForeignKey(
                        name: "heartbeat_controls_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "history_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    model = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    group = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    record_id = table.Column<long>(type: "bigint", nullable: true),
                    action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    summary = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_history_items", x => x.id);
                    table.ForeignKey(
                        name: "history_items_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "history_items_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "integration_tests",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    integration = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ok = table.Column<bool>(type: "bit", nullable: false),
                    message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    response_ms = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_tests", x => x.id);
                    table.ForeignKey(
                        name: "integration_tests_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "job_runs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    command = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    finished_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    duration_ms = table.Column<int>(type: "int", nullable: true),
                    triggered_by = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_runs", x => x.id);
                    table.ForeignKey(
                        name: "job_runs_triggered_by_foreign",
                        column: x => x.triggered_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    decided_by = table.Column<int>(type: "int", nullable: true),
                    decided_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    decision_note = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "ledger_entries_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ledger_entries_decided_by_foreign",
                        column: x => x.decided_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "ledger_entries_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    reference = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    method = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    source = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    reversed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    reversed_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    paid_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    kind = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    payment_method_id = table.Column<int>(type: "int", nullable: true),
                    confirmation = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.ForeignKey(
                        name: "payments_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "payments_reversed_by_foreign",
                        column: x => x.reversed_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "pdf_generations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    pdf_template_id = table.Column<int>(type: "int", nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    pages = table.Column<int>(type: "int", nullable: false),
                    size = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    footer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    show_address = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    header = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_generations", x => x.id);
                    table.ForeignKey(
                        name: "pdf_generations_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "pdf_generations_pdf_template_id_foreign",
                        column: x => x.pdf_template_id,
                        principalTable: "pdf_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "pdf_generations_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "pdf_test_sends",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    pdf_template_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    recipients = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_test_sends", x => x.id);
                    table.ForeignKey(
                        name: "pdf_test_sends_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "pdf_test_sends_pdf_template_id_foreign",
                        column: x => x.pdf_template_id,
                        principalTable: "pdf_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "pdf_test_sends_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "phonecalls",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    agent_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    direction = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    duration_sec = table.Column<int>(type: "int", nullable: false),
                    disposition = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    transcript = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_phonecalls", x => x.id);
                    table.ForeignKey(
                        name: "phonecalls_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "phonecalls_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "queue_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    queue = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    entered_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    exited_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_queue_logs", x => x.id);
                    table.ForeignKey(
                        name: "queue_logs_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "queue_logs_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ranger_agents",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    team = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    added_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranger_agents", x => x.id);
                    table.ForeignKey(
                        name: "ranger_agents_added_by_foreign",
                        column: x => x.added_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "ranger_agents_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rate_imports",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    path = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    rows = table.Column<int>(type: "int", nullable: false),
                    unchanged = table.Column<int>(type: "int", nullable: false),
                    changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    problems = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    effective_on = table.Column<DateOnly>(type: "date", nullable: true),
                    applied_by = table.Column<int>(type: "int", nullable: true),
                    applied_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    finished_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_imports", x => x.id);
                    table.ForeignKey(
                        name: "rate_imports_applied_by_foreign",
                        column: x => x.applied_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "rate_imports_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "rates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    plan_id = table.Column<int>(type: "int", nullable: false),
                    market_id = table.Column<int>(type: "int", nullable: false),
                    energy = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    effective_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rates", x => x.id);
                    table.ForeignKey(
                        name: "rates_created_by_foreign",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "rates_market_id_foreign",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "rates_plan_id_foreign",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    kind = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    method = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    requested_by = table.Column<int>(type: "int", nullable: true),
                    decided_by = table.Column<int>(type: "int", nullable: true),
                    decided_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    paid_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    decision_note = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    check_number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refunds", x => x.id);
                    table.ForeignKey(
                        name: "refunds_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "refunds_decided_by_foreign",
                        column: x => x.decided_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "refunds_requested_by_foreign",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "report_columns",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    report = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    columns = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_columns", x => x.id);
                    table.ForeignKey(
                        name: "report_columns_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "report_uploads",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    category = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    path = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    original_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_uploads", x => x.id);
                    table.ForeignKey(
                        name: "report_uploads_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "saved_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    report = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    @params = table.Column<string>(name: "params", type: "nvarchar(max)", nullable: true),
                    format = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    summary = table.Column<bool>(type: "bit", nullable: false),
                    shared = table.Column<bool>(type: "bit", nullable: false),
                    last_run_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_reports", x => x.id);
                    table.ForeignKey(
                        name: "saved_reports_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "site_blocks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    value = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    message = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    hits = table.Column<int>(type: "int", nullable: false),
                    last_hit_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_blocks", x => x.id);
                    table.ForeignKey(
                        name: "site_blocks_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "star_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    stars = table.Column<decimal>(type: "decimal(8,1)", precision: 8, scale: 1, nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    reward_offer_id = table.Column<int>(type: "int", nullable: true),
                    fulfillment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_star_entries", x => x.id);
                    table.ForeignKey(
                        name: "star_entries_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "star_entries_reward_offer_id_foreign",
                        column: x => x.reward_offer_id,
                        principalTable: "reward_offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "star_entries_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "support_tickets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    topic = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    channel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    assigned_to = table.Column<int>(type: "int", nullable: true),
                    first_response_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    solved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    rating = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    rating_comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    rated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_activity_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    manager_requested_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    manager_requested_by = table.Column<int>(type: "int", nullable: true),
                    manager_request_note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_tickets", x => x.id);
                    table.ForeignKey(
                        name: "support_tickets_assigned_to_foreign",
                        column: x => x.assigned_to,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "support_tickets_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "support_tickets_manager_requested_by_foreign",
                        column: x => x.manager_requested_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "table_snapshots",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    kind = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    key = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    app = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    html = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    columns = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    rows = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    record_id = table.Column<long>(type: "bigint", nullable: true),
                    row_count = table.Column<int>(type: "int", nullable: false),
                    hash = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "table_snapshots_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tdsp_fees",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    market_id = table.Column<int>(type: "int", nullable: false),
                    per_kwh = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    per_bill = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    effective_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tdsp_fees", x => x.id);
                    table.ForeignKey(
                        name: "tdsp_fees_created_by_foreign",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "tdsp_fees_market_id_foreign",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    queue = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    summary = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    resolved_by = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_items", x => x.id);
                    table.ForeignKey(
                        name: "work_items_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "work_items_resolved_by_foreign",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "survey_questions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    survey_id = table.Column<int>(type: "int", nullable: false),
                    question = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    options = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    position = table.Column<int>(type: "int", nullable: false),
                    survey_category_id = table.Column<int>(type: "int", nullable: true),
                    survey_answer_set_id = table.Column<int>(type: "int", nullable: true),
                    survey_bank_question_id = table.Column<int>(type: "int", nullable: true),
                    required = table.Column<bool>(type: "bit", nullable: false),
                    help = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_questions", x => x.id);
                    table.ForeignKey(
                        name: "survey_questions_survey_answer_set_id_foreign",
                        column: x => x.survey_answer_set_id,
                        principalTable: "survey_answer_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "survey_questions_survey_bank_question_id_foreign",
                        column: x => x.survey_bank_question_id,
                        principalTable: "survey_bank_questions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "survey_questions_survey_category_id_foreign",
                        column: x => x.survey_category_id,
                        principalTable: "survey_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "survey_questions_survey_id_foreign",
                        column: x => x.survey_id,
                        principalTable: "surveys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    number = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    items = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    subtotal = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    tax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    shipping = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    monthly = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    stars = table.Column<int>(type: "int", nullable: false),
                    pay_with = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    payment_method_id = table.Column<int>(type: "int", nullable: true),
                    ship_to = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    payment_status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    placed_by = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_orders", x => x.id);
                    table.ForeignKey(
                        name: "shop_orders_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "shop_orders_payment_method_id_foreign",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "shop_orders_placed_by_foreign",
                        column: x => x.placed_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "contact_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    channel = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    template = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    dropped_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    opened_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    clicked_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    direction = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    campaign_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_logs", x => x.id);
                    table.ForeignKey(
                        name: "contact_logs_campaign_id_foreign",
                        column: x => x.campaign_id,
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "contact_logs_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "contact_logs_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "pages",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    path = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    template_id = table.Column<int>(type: "int", nullable: true),
                    redirect = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    meta_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    html_title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    meta_keywords = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    promo_code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    no_index = table.Column<bool>(type: "bit", nullable: false),
                    site_id = table.Column<int>(type: "int", nullable: true),
                    file = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    page_title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    head_content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    phone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    canonical = table.Column<bool>(type: "bit", nullable: false),
                    market_id = table.Column<int>(type: "int", nullable: true),
                    pricegrid_type = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    pricegrid_header = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    pricegrid_group_id = table.Column<int>(type: "int", nullable: true),
                    rating_formula = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    copy_from_id = table.Column<int>(type: "int", nullable: true),
                    hard_cache = table.Column<bool>(type: "bit", nullable: false),
                    rep_id = table.Column<int>(type: "int", nullable: true),
                    market_label = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    address_box_title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    content_primary_id = table.Column<int>(type: "int", nullable: true),
                    content_secondary_id = table.Column<int>(type: "int", nullable: true),
                    content_parent_id = table.Column<int>(type: "int", nullable: true),
                    content_auxiliary_id = table.Column<int>(type: "int", nullable: true),
                    content_amp_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pages", x => x.id);
                    table.ForeignKey(
                        name: "pages_content_amp_id_foreign",
                        column: x => x.content_amp_id,
                        principalTable: "content_blocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "pages_content_auxiliary_id_foreign",
                        column: x => x.content_auxiliary_id,
                        principalTable: "content_blocks",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "pages_content_parent_id_foreign",
                        column: x => x.content_parent_id,
                        principalTable: "content_blocks",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "pages_content_primary_id_foreign",
                        column: x => x.content_primary_id,
                        principalTable: "content_blocks",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "pages_content_secondary_id_foreign",
                        column: x => x.content_secondary_id,
                        principalTable: "content_blocks",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "pages_copy_from_id_foreign",
                        column: x => x.copy_from_id,
                        principalTable: "pages",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "pages_market_id_foreign",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "pages_pricegrid_group_id_foreign",
                        column: x => x.pricegrid_group_id,
                        principalTable: "plan_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "pages_site_id_foreign",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "pages_template_id_foreign",
                        column: x => x.template_id,
                        principalTable: "templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "crm_leads",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    company = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    city = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    market_id = table.Column<int>(type: "int", nullable: true),
                    source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    partner_id = table.Column<int>(type: "int", nullable: true),
                    stage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    annual_kwh = table.Column<int>(type: "int", nullable: false),
                    est_value = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    sites = table.Column<int>(type: "int", nullable: false),
                    current_provider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    contract_ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    owner_id = table.Column<int>(type: "int", nullable: true),
                    next_follow_up_on = table.Column<DateOnly>(type: "date", nullable: true),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    lost_reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    stage_changed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    closed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_leads", x => x.id);
                    table.ForeignKey(
                        name: "crm_leads_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "crm_leads_market_id_foreign",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "crm_leads_owner_id_foreign",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "crm_leads_partner_id_foreign",
                        column: x => x.partner_id,
                        principalTable: "crm_partners",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "scheduled_payments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    payment_method_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    pay_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    payment_id = table.Column<int>(type: "int", nullable: true),
                    note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_payments", x => x.id);
                    table.ForeignKey(
                        name: "scheduled_payments_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "scheduled_payments_payment_id_foreign",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "scheduled_payments_payment_method_id_foreign",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "report_runs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    report = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    @params = table.Column<string>(name: "params", type: "nvarchar(max)", nullable: true),
                    rows = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    file = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    app = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    model = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    finished_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    duration_ms = table.Column<int>(type: "int", nullable: true),
                    error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    preview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    rerun_of = table.Column<int>(type: "int", nullable: true),
                    format = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    summary = table.Column<bool>(type: "bit", nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    seen_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    saved_report_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_runs", x => x.id);
                    table.ForeignKey(
                        name: "report_runs_rerun_of_foreign",
                        column: x => x.rerun_of,
                        principalTable: "report_runs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "report_runs_saved_report_id_foreign",
                        column: x => x.saved_report_id,
                        principalTable: "saved_reports",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "report_runs_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
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
                    author = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    disposition = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    category = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    action = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    priority = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    support_ticket_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notes", x => x.id);
                    table.ForeignKey(
                        name: "notes_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "notes_support_ticket_id_foreign",
                        column: x => x.support_ticket_id,
                        principalTable: "support_tickets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "notes_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "support_ticket_messages",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    support_ticket_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    author = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    via = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    direction = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    duration = table.Column<int>(type: "int", nullable: true),
                    contact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_ticket_messages", x => x.id);
                    table.ForeignKey(
                        name: "support_ticket_messages_support_ticket_id_foreign",
                        column: x => x.support_ticket_id,
                        principalTable: "support_tickets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "support_ticket_messages_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "support_ticket_watchers",
                columns: table => new
                {
                    support_ticket_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    joined_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_ticket_watchers", x => new { x.support_ticket_id, x.user_id });
                    table.ForeignKey(
                        name: "support_ticket_watchers_support_ticket_id_foreign",
                        column: x => x.support_ticket_id,
                        principalTable: "support_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "support_ticket_watchers_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "link_clicks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    contact_log_id = table.Column<int>(type: "int", nullable: true),
                    campaign_id = table.Column<int>(type: "int", nullable: true),
                    email_template_id = table.Column<int>(type: "int", nullable: true),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    link = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    device = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    clicked_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_link_clicks", x => x.id);
                    table.ForeignKey(
                        name: "link_clicks_campaign_id_foreign",
                        column: x => x.campaign_id,
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "link_clicks_contact_log_id_foreign",
                        column: x => x.contact_log_id,
                        principalTable: "contact_logs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "link_clicks_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "link_clicks_email_template_id_foreign",
                        column: x => x.email_template_id,
                        principalTable: "email_templates",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "page_components",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    page_id = table.Column<int>(type: "int", nullable: false),
                    content_block_id = table.Column<int>(type: "int", nullable: false),
                    zone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    vars = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_page_components", x => x.id);
                    table.ForeignKey(
                        name: "page_components_content_block_id_foreign",
                        column: x => x.content_block_id,
                        principalTable: "content_blocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "page_components_page_id_foreign",
                        column: x => x.page_id,
                        principalTable: "pages",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "site_visits",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    visitor = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    path = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    page_id = table.Column<int>(type: "int", nullable: true),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    referrer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    agent = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    blocked = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    seen_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_visits", x => x.id);
                    table.ForeignKey(
                        name: "site_visits_customer_id_foreign",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "site_visits_page_id_foreign",
                        column: x => x.page_id,
                        principalTable: "pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "crm_activities",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lead_id = table.Column<int>(type: "int", nullable: true),
                    partner_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    done_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_activities", x => x.id);
                    table.ForeignKey(
                        name: "crm_activities_lead_id_foreign",
                        column: x => x.lead_id,
                        principalTable: "crm_leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "crm_activities_partner_id_foreign",
                        column: x => x.partner_id,
                        principalTable: "crm_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "crm_activities_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "support_ticket_attachments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    support_ticket_id = table.Column<int>(type: "int", nullable: false),
                    support_ticket_message_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    from_customer = table.Column<bool>(type: "bit", nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    path = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    mime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    size = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_support_ticket_attachments", x => x.id);
                    table.ForeignKey(
                        name: "support_ticket_attachments_support_ticket_id_foreign",
                        column: x => x.support_ticket_id,
                        principalTable: "support_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "support_ticket_attachments_support_ticket_message_id_foreign",
                        column: x => x.support_ticket_message_id,
                        principalTable: "support_ticket_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "support_ticket_attachments_user_id_foreign",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "admin_apps_key_unique",
                table: "admin_apps",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_apps_created_by",
                table: "admin_apps",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "admin_notifications_user_id_read_at_index",
                table: "admin_notifications",
                columns: new[] { "user_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agents_ai_model_id",
                table: "ai_agents",
                column: "ai_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_agents_fallback_model_id",
                table: "ai_agents",
                column: "fallback_model_id");

            migrationBuilder.CreateIndex(
                name: "ai_automation_runs_started_at_index",
                table: "ai_automation_runs",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "IX_ai_automation_runs_ai_agent_id",
                table: "ai_automation_runs",
                column: "ai_agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_automation_runs_ai_model_id",
                table: "ai_automation_runs",
                column: "ai_model_id");

            migrationBuilder.CreateIndex(
                name: "ai_conversations_started_at_index",
                table: "ai_conversations",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ai_conversations_status_index",
                table: "ai_conversations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_ai_agent_id",
                table: "ai_conversations",
                column: "ai_agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_ai_model_id",
                table: "ai_conversations",
                column: "ai_model_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_customer_id",
                table: "ai_conversations",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_handed_to_id",
                table: "ai_conversations",
                column: "handed_to_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_conversations_user_id",
                table: "ai_conversations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ai_models_provider_model_id_unique",
                table: "ai_models",
                columns: new[] { "provider", "model_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_logs_customer_id",
                table: "api_logs",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "audit_entries_created_at_index",
                table: "audit_entries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "audit_entries_user_id_index",
                table: "audit_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "bills_reference_unique",
                table: "bills",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bills_customer_id",
                table: "bills",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "block_categories_name_unique",
                table: "block_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookmarks_customer_id",
                table: "bookmarks",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "byop_products_key_unique",
                table: "byop_products",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_created_by",
                table: "campaigns",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_email_template_id",
                table: "campaigns",
                column: "email_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_contact_logs_campaign_id",
                table: "contact_logs",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "IX_contact_logs_customer_id",
                table: "contact_logs",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_contact_logs_user_id",
                table: "contact_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_contact_messages_customer_id",
                table: "contact_messages",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_contact_messages_handled_by",
                table: "contact_messages",
                column: "handled_by");

            migrationBuilder.CreateIndex(
                name: "IX_contact_messages_replied_by",
                table: "contact_messages",
                column: "replied_by");

            migrationBuilder.CreateIndex(
                name: "content_blocks_slug_unique",
                table: "content_blocks",
                column: "slug",
                unique: true,
                filter: "[slug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_content_blocks_category_id",
                table: "content_blocks",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_content_blocks_site_id",
                table: "content_blocks",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_content_blocks_updated_by",
                table: "content_blocks",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "cookie_consents_created_at_index",
                table: "cookie_consents",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "cookie_consents_visitor_index",
                table: "cookie_consents",
                column: "visitor");

            migrationBuilder.CreateIndex(
                name: "IX_cookie_consents_customer_id",
                table: "cookie_consents",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_cookie_consents_user_id",
                table: "cookie_consents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "crm_activities_type_due_on_done_at_index",
                table: "crm_activities",
                columns: new[] { "type", "due_on", "done_at" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_activities_lead_id",
                table: "crm_activities",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activities_partner_id",
                table: "crm_activities",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_crm_activities_user_id",
                table: "crm_activities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "crm_leads_stage_owner_id_index",
                table: "crm_leads",
                columns: new[] { "stage", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_leads_customer_id",
                table: "crm_leads",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_crm_leads_market_id",
                table: "crm_leads",
                column: "market_id");

            migrationBuilder.CreateIndex(
                name: "IX_crm_leads_owner_id",
                table: "crm_leads",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_crm_leads_partner_id",
                table: "crm_leads",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_crm_partners_owner_id",
                table: "crm_partners",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_files_customer_id",
                table: "customer_files",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_flags_customer_id",
                table: "customer_flags",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_flags_user_id",
                table: "customer_flags",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_products_customer_id",
                table: "customer_products",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_products_user_id",
                table: "customer_products",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "customers_account_unique",
                table: "customers",
                column: "account",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "customers_esiid_index",
                table: "customers",
                column: "esiid");

            migrationBuilder.CreateIndex(
                name: "customers_exception_index",
                table: "customers",
                column: "exception");

            migrationBuilder.CreateIndex(
                name: "customers_referral_code_unique",
                table: "customers",
                column: "referral_code",
                unique: true,
                filter: "[referral_code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "customers_status_index",
                table: "customers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "customers_ticket_unique",
                table: "customers",
                column: "ticket",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_market_id",
                table: "customers",
                column: "market_id");

            migrationBuilder.CreateIndex(
                name: "IX_customers_plan_id",
                table: "customers",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "data_items_customer_id_model_index",
                table: "data_items",
                columns: new[] { "customer_id", "model" });

            migrationBuilder.CreateIndex(
                name: "data_items_model_record_id_index",
                table: "data_items",
                columns: new[] { "model", "record_id" });

            migrationBuilder.CreateIndex(
                name: "email_categories_name_unique",
                table: "email_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "email_parts_kind_name_unique",
                table: "email_parts",
                columns: new[] { "kind", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "email_suppressions_email_unique",
                table: "email_suppressions",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_suppressions_customer_id",
                table: "email_suppressions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_suppressions_user_id",
                table: "email_suppressions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "email_templates_name_unique",
                table: "email_templates",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_templates_email_category_id",
                table: "email_templates",
                column: "email_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_templates_email_footer_id",
                table: "email_templates",
                column: "email_footer_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_templates_email_header_id",
                table: "email_templates",
                column: "email_header_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_test_sends_email_template_id",
                table: "email_test_sends",
                column: "email_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_test_sends_user_id",
                table: "email_test_sends",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ercot_transactions_customer_id",
                table: "ercot_transactions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "heartbeat_actions_check_key_index",
                table: "heartbeat_actions",
                column: "check_key");

            migrationBuilder.CreateIndex(
                name: "heartbeat_actions_created_at_index",
                table: "heartbeat_actions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_heartbeat_actions_user_id",
                table: "heartbeat_actions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_heartbeat_controls_user_id",
                table: "heartbeat_controls",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "heartbeats_checked_at_index",
                table: "heartbeats",
                column: "checked_at");

            migrationBuilder.CreateIndex(
                name: "history_items_created_at_index",
                table: "history_items",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "history_items_customer_id_created_at_index",
                table: "history_items",
                columns: new[] { "customer_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "history_items_group_index",
                table: "history_items",
                column: "group");

            migrationBuilder.CreateIndex(
                name: "history_items_model_index",
                table: "history_items",
                column: "model");

            migrationBuilder.CreateIndex(
                name: "history_items_model_record_id_index",
                table: "history_items",
                columns: new[] { "model", "record_id" });

            migrationBuilder.CreateIndex(
                name: "history_items_user_id_created_at_index",
                table: "history_items",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "integration_tests_integration_created_at_index",
                table: "integration_tests",
                columns: new[] { "integration", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_tests_user_id",
                table: "integration_tests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_runs_triggered_by",
                table: "job_runs",
                column: "triggered_by");

            migrationBuilder.CreateIndex(
                name: "job_runs_command_index",
                table: "job_runs",
                column: "command");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_customer_id",
                table: "ledger_entries",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_decided_by",
                table: "ledger_entries",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_user_id",
                table: "ledger_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_link_clicks_contact_log_id",
                table: "link_clicks",
                column: "contact_log_id");

            migrationBuilder.CreateIndex(
                name: "IX_link_clicks_customer_id",
                table: "link_clicks",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "link_clicks_campaign_id_link_index",
                table: "link_clicks",
                columns: new[] { "campaign_id", "link" });

            migrationBuilder.CreateIndex(
                name: "link_clicks_clicked_at_index",
                table: "link_clicks",
                column: "clicked_at");

            migrationBuilder.CreateIndex(
                name: "link_clicks_email_template_id_link_index",
                table: "link_clicks",
                columns: new[] { "email_template_id", "link" });

            migrationBuilder.CreateIndex(
                name: "marketing_channels_msid_unique",
                table: "marketing_channels",
                column: "msid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "markets_name_unique",
                table: "markets",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notes_customer_id",
                table: "notes",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_notes_support_ticket_id",
                table: "notes",
                column: "support_ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_notes_user_id",
                table: "notes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_page_components_content_block_id",
                table: "page_components",
                column: "content_block_id");

            migrationBuilder.CreateIndex(
                name: "IX_page_components_page_id",
                table: "page_components",
                column: "page_id");

            migrationBuilder.CreateIndex(
                name: "IX_pages_content_amp_id",
                table: "pages",
                column: "content_amp_id");

            migrationBuilder.CreateIndex(
                name: "IX_pages_content_auxiliary_id",
                table: "pages",
                column: "content_auxiliary_id");

            migrationBuilder.CreateIndex(
                name: "IX_pages_content_parent_id",
                table: "pages",
                column: "content_parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_pages_content_primary_id",
                table: "pages",
                column: "content_primary_id");

            migrationBuilder.CreateIndex(
                name: "IX_pages_content_secondary_id",
                table: "pages",
                column: "content_secondary_id");

            migrationBuilder.CreateIndex(
                name: "IX_pages_copy_from_id",
                table: "pages",
                column: "copy_from_id");

            migrationBuilder.CreateIndex(
                name: "IX_pages_market_id",
                table: "pages",
                column: "market_id");

            migrationBuilder.CreateIndex(
                name: "IX_pages_pricegrid_group_id",
                table: "pages",
                column: "pricegrid_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_pages_template_id",
                table: "pages",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "pages_path_unique",
                table: "pages",
                column: "path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "pages_site_id_path_unique",
                table: "pages",
                columns: new[] { "site_id", "path" },
                unique: true,
                filter: "[site_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_methods_customer_id",
                table: "payment_methods",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_customer_id",
                table: "payments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_reversed_by",
                table: "payments",
                column: "reversed_by");

            migrationBuilder.CreateIndex(
                name: "payments_reference_unique",
                table: "payments",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_generations_customer_id",
                table: "pdf_generations",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_generations_pdf_template_id",
                table: "pdf_generations",
                column: "pdf_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_generations_user_id",
                table: "pdf_generations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "pdf_generations_created_at_index",
                table: "pdf_generations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_templates_pdf_footer_id",
                table: "pdf_templates",
                column: "pdf_footer_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_templates_pdf_header_id",
                table: "pdf_templates",
                column: "pdf_header_id");

            migrationBuilder.CreateIndex(
                name: "pdf_templates_name_unique",
                table: "pdf_templates",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_test_sends_customer_id",
                table: "pdf_test_sends",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_test_sends_pdf_template_id",
                table: "pdf_test_sends",
                column: "pdf_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_test_sends_user_id",
                table: "pdf_test_sends",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "pdf_test_sends_created_at_index",
                table: "pdf_test_sends",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_phonecalls_customer_id",
                table: "phonecalls",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_phonecalls_user_id",
                table: "phonecalls",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_plan_group_plan_plan_id",
                table: "plan_group_plan",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "plan_groups_slug_unique",
                table: "plan_groups",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_terms_customer_id",
                table: "plan_terms",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_plan_terms_plan_id",
                table: "plan_terms",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "plans_internal_unique",
                table: "plans",
                column: "internal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "promo_codes_code_unique",
                table: "promo_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_queue_logs_customer_id",
                table: "queue_logs",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_queue_logs_user_id",
                table: "queue_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ranger_agents_added_by",
                table: "ranger_agents",
                column: "added_by");

            migrationBuilder.CreateIndex(
                name: "ranger_agents_user_id_unique",
                table: "ranger_agents",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rate_imports_applied_by",
                table: "rate_imports",
                column: "applied_by");

            migrationBuilder.CreateIndex(
                name: "IX_rate_imports_user_id",
                table: "rate_imports",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_rates_created_by",
                table: "rates",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_rates_market_id",
                table: "rates",
                column: "market_id");

            migrationBuilder.CreateIndex(
                name: "rates_plan_id_market_id_effective_on_index",
                table: "rates",
                columns: new[] { "plan_id", "market_id", "effective_on" });

            migrationBuilder.CreateIndex(
                name: "reference_rows_table_key_index",
                table: "reference_rows",
                column: "table_key");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_customer_id",
                table: "refunds",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_decided_by",
                table: "refunds",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_requested_by",
                table: "refunds",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "IX_report_columns_user_id",
                table: "report_columns",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "report_columns_report_unique",
                table: "report_columns",
                column: "report",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_runs_rerun_of",
                table: "report_runs",
                column: "rerun_of");

            migrationBuilder.CreateIndex(
                name: "IX_report_runs_saved_report_id",
                table: "report_runs",
                column: "saved_report_id");

            migrationBuilder.CreateIndex(
                name: "IX_report_runs_user_id",
                table: "report_runs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_report_uploads_user_id",
                table: "report_uploads",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "reward_rules_key_unique",
                table: "reward_rules",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "roles_name_unique",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saved_reports_user_id",
                table: "saved_reports",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_payments_customer_id",
                table: "scheduled_payments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_payments_payment_id",
                table: "scheduled_payments",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_payments_payment_method_id",
                table: "scheduled_payments",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "scheduled_payments_pay_at_index",
                table: "scheduled_payments",
                column: "pay_at");

            migrationBuilder.CreateIndex(
                name: "IX_service_addresses_customer_id",
                table: "service_addresses",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_shop_orders_customer_id",
                table: "shop_orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_shop_orders_payment_method_id",
                table: "shop_orders",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_shop_orders_placed_by",
                table: "shop_orders",
                column: "placed_by");

            migrationBuilder.CreateIndex(
                name: "shop_orders_number_unique",
                table: "shop_orders",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "shop_products_sku_unique",
                table: "shop_products",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signups_customer_id",
                table: "signups",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "signups_token_unique",
                table: "signups",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_site_blocks_user_id",
                table: "site_blocks",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "site_blocks_type_value_unique",
                table: "site_blocks",
                columns: new[] { "type", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_site_visits_customer_id",
                table: "site_visits",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_site_visits_page_id",
                table: "site_visits",
                column: "page_id");

            migrationBuilder.CreateIndex(
                name: "site_visits_created_at_index",
                table: "site_visits",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "site_visits_ip_index",
                table: "site_visits",
                column: "ip");

            migrationBuilder.CreateIndex(
                name: "site_visits_path_index",
                table: "site_visits",
                column: "path");

            migrationBuilder.CreateIndex(
                name: "site_visits_seen_at_index",
                table: "site_visits",
                column: "seen_at");

            migrationBuilder.CreateIndex(
                name: "site_visits_visitor_index",
                table: "site_visits",
                column: "visitor");

            migrationBuilder.CreateIndex(
                name: "sites_domain_unique",
                table: "sites",
                column: "domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_star_entries_customer_id",
                table: "star_entries",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_star_entries_reward_offer_id",
                table: "star_entries",
                column: "reward_offer_id");

            migrationBuilder.CreateIndex(
                name: "IX_star_entries_user_id",
                table: "star_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_ticket_attachments_support_ticket_id",
                table: "support_ticket_attachments",
                column: "support_ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_ticket_attachments_support_ticket_message_id",
                table: "support_ticket_attachments",
                column: "support_ticket_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_ticket_attachments_user_id",
                table: "support_ticket_attachments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_ticket_messages_support_ticket_id",
                table: "support_ticket_messages",
                column: "support_ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_ticket_messages_user_id",
                table: "support_ticket_messages",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_ticket_watchers_user_id",
                table: "support_ticket_watchers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_assigned_to",
                table: "support_tickets",
                column: "assigned_to");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_customer_id",
                table: "support_tickets",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_manager_requested_by",
                table: "support_tickets",
                column: "manager_requested_by");

            migrationBuilder.CreateIndex(
                name: "support_tickets_last_activity_at_index",
                table: "support_tickets",
                column: "last_activity_at");

            migrationBuilder.CreateIndex(
                name: "support_tickets_manager_requested_at_index",
                table: "support_tickets",
                column: "manager_requested_at");

            migrationBuilder.CreateIndex(
                name: "support_tickets_status_index",
                table: "support_tickets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "survey_answer_sets_name_unique",
                table: "survey_answer_sets",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_bank_questions_survey_answer_set_id",
                table: "survey_bank_questions",
                column: "survey_answer_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_bank_questions_survey_category_id",
                table: "survey_bank_questions",
                column: "survey_category_id");

            migrationBuilder.CreateIndex(
                name: "survey_categories_name_unique",
                table: "survey_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_questions_survey_answer_set_id",
                table: "survey_questions",
                column: "survey_answer_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_questions_survey_bank_question_id",
                table: "survey_questions",
                column: "survey_bank_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_questions_survey_category_id",
                table: "survey_questions",
                column: "survey_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_questions_survey_id",
                table: "survey_questions",
                column: "survey_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_responses_customer_id",
                table: "survey_responses",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_survey_responses_survey_id",
                table: "survey_responses",
                column: "survey_id");

            migrationBuilder.CreateIndex(
                name: "surveys_slug_unique",
                table: "surveys",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_table_snapshots_user_id",
                table: "table_snapshots",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "table_snapshots_created_at_index",
                table: "table_snapshots",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "table_snapshots_key_index",
                table: "table_snapshots",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "table_snapshots_kind_key_id_index",
                table: "table_snapshots",
                columns: new[] { "kind", "key", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_tdsp_fees_created_by",
                table: "tdsp_fees",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "tdsp_fees_market_id_effective_on_index",
                table: "tdsp_fees",
                columns: new[] { "market_id", "effective_on" });

            migrationBuilder.CreateIndex(
                name: "templates_name_unique",
                table: "templates",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "term_discounts_term_region_unique",
                table: "term_discounts",
                columns: new[] { "term", "region" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_manager_id",
                table: "users",
                column: "manager_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "users_email_unique",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "users_employee_id_unique",
                table: "users",
                column: "employee_id",
                unique: true,
                filter: "[employee_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_customer_id",
                table: "work_items",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_resolved_by",
                table: "work_items",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "work_items_queue_index",
                table: "work_items",
                column: "queue");

            migrationBuilder.CreateIndex(
                name: "IX_zip_ranges_market_id",
                table: "zip_ranges",
                column: "market_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_apps");

            migrationBuilder.DropTable(
                name: "admin_notifications");

            migrationBuilder.DropTable(
                name: "ai_automation_runs");

            migrationBuilder.DropTable(
                name: "ai_conversations");

            migrationBuilder.DropTable(
                name: "api_logs");

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
                name: "cookie_consents");

            migrationBuilder.DropTable(
                name: "crm_activities");

            migrationBuilder.DropTable(
                name: "customer_files");

            migrationBuilder.DropTable(
                name: "customer_flags");

            migrationBuilder.DropTable(
                name: "customer_password_resets");

            migrationBuilder.DropTable(
                name: "customer_products");

            migrationBuilder.DropTable(
                name: "data_items");

            migrationBuilder.DropTable(
                name: "email_suppressions");

            migrationBuilder.DropTable(
                name: "email_test_sends");

            migrationBuilder.DropTable(
                name: "ercot_transactions");

            migrationBuilder.DropTable(
                name: "heartbeat_actions");

            migrationBuilder.DropTable(
                name: "heartbeat_controls");

            migrationBuilder.DropTable(
                name: "heartbeat_pulses");

            migrationBuilder.DropTable(
                name: "heartbeats");

            migrationBuilder.DropTable(
                name: "history_items");

            migrationBuilder.DropTable(
                name: "integration_tests");

            migrationBuilder.DropTable(
                name: "job_runs");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "link_clicks");

            migrationBuilder.DropTable(
                name: "marketing_channels");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "page_components");

            migrationBuilder.DropTable(
                name: "pdf_generations");

            migrationBuilder.DropTable(
                name: "pdf_test_sends");

            migrationBuilder.DropTable(
                name: "phonecalls");

            migrationBuilder.DropTable(
                name: "plan_group_plan");

            migrationBuilder.DropTable(
                name: "plan_terms");

            migrationBuilder.DropTable(
                name: "promo_codes");

            migrationBuilder.DropTable(
                name: "queue_logs");

            migrationBuilder.DropTable(
                name: "ranger_agents");

            migrationBuilder.DropTable(
                name: "rate_imports");

            migrationBuilder.DropTable(
                name: "rates");

            migrationBuilder.DropTable(
                name: "reference_rows");

            migrationBuilder.DropTable(
                name: "refunds");

            migrationBuilder.DropTable(
                name: "report_columns");

            migrationBuilder.DropTable(
                name: "report_runs");

            migrationBuilder.DropTable(
                name: "report_uploads");

            migrationBuilder.DropTable(
                name: "reward_rules");

            migrationBuilder.DropTable(
                name: "scheduled_payments");

            migrationBuilder.DropTable(
                name: "service_addresses");

            migrationBuilder.DropTable(
                name: "shop_orders");

            migrationBuilder.DropTable(
                name: "shop_products");

            migrationBuilder.DropTable(
                name: "signups");

            migrationBuilder.DropTable(
                name: "site_blocks");

            migrationBuilder.DropTable(
                name: "site_visits");

            migrationBuilder.DropTable(
                name: "star_entries");

            migrationBuilder.DropTable(
                name: "support_ticket_attachments");

            migrationBuilder.DropTable(
                name: "support_ticket_watchers");

            migrationBuilder.DropTable(
                name: "survey_questions");

            migrationBuilder.DropTable(
                name: "survey_responses");

            migrationBuilder.DropTable(
                name: "table_snapshots");

            migrationBuilder.DropTable(
                name: "tdsp_fees");

            migrationBuilder.DropTable(
                name: "term_discounts");

            migrationBuilder.DropTable(
                name: "term_etfs");

            migrationBuilder.DropTable(
                name: "work_items");

            migrationBuilder.DropTable(
                name: "zip_ranges");

            migrationBuilder.DropTable(
                name: "ai_agents");

            migrationBuilder.DropTable(
                name: "crm_leads");

            migrationBuilder.DropTable(
                name: "contact_logs");

            migrationBuilder.DropTable(
                name: "pdf_templates");

            migrationBuilder.DropTable(
                name: "saved_reports");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.DropTable(
                name: "pages");

            migrationBuilder.DropTable(
                name: "reward_offers");

            migrationBuilder.DropTable(
                name: "support_ticket_messages");

            migrationBuilder.DropTable(
                name: "survey_bank_questions");

            migrationBuilder.DropTable(
                name: "surveys");

            migrationBuilder.DropTable(
                name: "ai_models");

            migrationBuilder.DropTable(
                name: "crm_partners");

            migrationBuilder.DropTable(
                name: "campaigns");

            migrationBuilder.DropTable(
                name: "pdf_parts");

            migrationBuilder.DropTable(
                name: "content_blocks");

            migrationBuilder.DropTable(
                name: "plan_groups");

            migrationBuilder.DropTable(
                name: "templates");

            migrationBuilder.DropTable(
                name: "support_tickets");

            migrationBuilder.DropTable(
                name: "survey_answer_sets");

            migrationBuilder.DropTable(
                name: "survey_categories");

            migrationBuilder.DropTable(
                name: "email_templates");

            migrationBuilder.DropTable(
                name: "block_categories");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "email_categories");

            migrationBuilder.DropTable(
                name: "email_parts");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "markets");

            migrationBuilder.DropTable(
                name: "plans");
        }
    }
}
