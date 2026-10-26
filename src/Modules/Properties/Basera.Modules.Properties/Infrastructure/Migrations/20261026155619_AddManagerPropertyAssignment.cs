using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Basera.Modules.Properties.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerPropertyAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manager_property_assignment",
                schema: "properties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    property_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_manager_property_assignment", x => x.id);
                    table.ForeignKey(
                        name: "fk_manager_property_assignment_property_property_id",
                        column: x => x.property_id,
                        principalSchema: "properties",
                        principalTable: "property",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_manager_property_assignment_property_id",
                schema: "properties",
                table: "manager_property_assignment",
                column: "property_id");

            migrationBuilder.CreateIndex(
                name: "ix_manager_property_assignment_tenant_id_membership_id_propert",
                schema: "properties",
                table: "manager_property_assignment",
                columns: new[] { "tenant_id", "membership_id", "property_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manager_property_assignment",
                schema: "properties");
        }
    }
}
