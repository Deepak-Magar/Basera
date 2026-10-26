using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Basera.Modules.Properties.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "properties");

            migrationBuilder.CreateTable(
                name: "property",
                schema: "properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ward = table.Column<int>(type: "integer", nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    rules = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    payment_qr_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    logo_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    reservation_days = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_property", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "floor",
                schema: "properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floor", x => x.id);
                    table.ForeignKey(
                        name: "fk_floor_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "properties",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rate_plan",
                schema: "properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    single_rate = table.Column<long>(type: "bigint", nullable: true),
                    double_rate = table.Column<long>(type: "bigint", nullable: true),
                    triple_rate = table.Column<long>(type: "bigint", nullable: true),
                    four_rate = table.Column<long>(type: "bigint", nullable: true),
                    dorm_rate = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_plan", x => x.id);
                    table.ForeignKey(
                        name: "fk_rate_plan_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "properties",
                        principalTable: "property",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "room",
                schema: "properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor_number = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    attributes = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "properties",
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bed",
                schema: "properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    reserved_until = table.Column<DateOnly>(type: "date", nullable: true),
                    reserved_for = table.Column<Guid>(type: "uuid", nullable: true),
                    rate_override = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bed", x => x.id);
                    table.ForeignKey(
                        name: "fk_bed_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "properties",
                        principalTable: "property",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_bed_room_room_id",
                        column: x => x.room_id,
                        principalSchema: "properties",
                        principalTable: "room",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bed_occupancy",
                schema: "properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bed_occupancy", x => x.id);
                    table.ForeignKey(
                        name: "fk_bed_occupancy_bed_bed_id",
                        column: x => x.bed_id,
                        principalSchema: "properties",
                        principalTable: "bed",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_bed_property_id",
                schema: "properties",
                table: "bed",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "ix_bed_room_id",
                schema: "properties",
                table: "bed",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_bed_tenant_id_property_id_code",
                schema: "properties",
                table: "bed",
                columns: new[] { "tenant_id", "property_id", "code" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bed_tenant_id_property_id_status",
                schema: "properties",
                table: "bed",
                columns: new[] { "tenant_id", "property_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_bed_occupancy_one_open_per_bed",
                schema: "properties",
                table: "bed_occupancy",
                column: "bed_id",
                unique: true,
                filter: "to_date IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bed_occupancy_tenant_id_bed_id_from_date",
                schema: "properties",
                table: "bed_occupancy",
                columns: new[] { "tenant_id", "bed_id", "from_date" });

            migrationBuilder.CreateIndex(
                name: "ix_bed_occupancy_tenant_id_stay_id",
                schema: "properties",
                table: "bed_occupancy",
                columns: new[] { "tenant_id", "stay_id" });

            migrationBuilder.CreateIndex(
                name: "ix_floor_property_id",
                schema: "properties",
                table: "floor",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_tenant_id_property_id_number",
                schema: "properties",
                table: "floor",
                columns: new[] { "tenant_id", "property_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_property_tenant_id_name",
                schema: "properties",
                table: "property",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_rate_plan_property_id",
                schema: "properties",
                table: "rate_plan",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "ix_rate_plan_tenant_id_property_id_effective_from",
                schema: "properties",
                table: "rate_plan",
                columns: new[] { "tenant_id", "property_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_property_id",
                schema: "properties",
                table: "room",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_tenant_id_property_id_number",
                schema: "properties",
                table: "room",
                columns: new[] { "tenant_id", "property_id", "number" },
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bed_occupancy",
                schema: "properties");

            migrationBuilder.DropTable(
                name: "floor",
                schema: "properties");

            migrationBuilder.DropTable(
                name: "rate_plan",
                schema: "properties");

            migrationBuilder.DropTable(
                name: "bed",
                schema: "properties");

            migrationBuilder.DropTable(
                name: "room",
                schema: "properties");

            migrationBuilder.DropTable(
                name: "property",
                schema: "properties");
        }
    }
}
