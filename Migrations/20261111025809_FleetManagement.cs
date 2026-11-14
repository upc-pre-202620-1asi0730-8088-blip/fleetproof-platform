using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetProof.Api.Migrations
{
    /// <inheritdoc />
    public partial class FleetManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_vehicles_user_id_id",
                table: "vehicles",
                columns: new[] { "user_id", "id" });

            migrationBuilder.CreateTable(
                name: "fleets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fleets", x => x.id);
                    table.UniqueConstraint("AK_fleets_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_fleets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fleet_vehicle_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fleet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsible = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fleet_vehicle_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_fleet_vehicle_assignments_fleets_user_id_fleet_id",
                        columns: x => new { x.user_id, x.fleet_id },
                        principalTable: "fleets",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fleet_vehicle_assignments_vehicles_user_id_vehicle_id",
                        columns: x => new { x.user_id, x.vehicle_id },
                        principalTable: "vehicles",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fleet_vehicle_assignments_user_id_fleet_id",
                table: "fleet_vehicle_assignments",
                columns: new[] { "user_id", "fleet_id" });

            migrationBuilder.CreateIndex(
                name: "IX_fleet_vehicle_assignments_user_id_vehicle_id",
                table: "fleet_vehicle_assignments",
                columns: new[] { "user_id", "vehicle_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fleet_vehicle_assignments");

            migrationBuilder.DropTable(
                name: "fleets");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_vehicles_user_id_id",
                table: "vehicles");
        }
    }
}
