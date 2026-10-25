using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Basera.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffInvitation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_invitation",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_invitation", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_invitation_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "identity",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_staff_invitation_tenant_id_phone",
                schema: "identity",
                table: "staff_invitation",
                columns: new[] { "tenant_id", "phone" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_invitation_tenant_id_token_hash",
                schema: "identity",
                table: "staff_invitation",
                columns: new[] { "tenant_id", "token_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_invitation",
                schema: "identity");
        }
    }
}
