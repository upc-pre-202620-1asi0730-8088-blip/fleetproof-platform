using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetProof.Api.Migrations
{
    /// <inheritdoc />
    public partial class CompleteContextFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "session_version",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "monitoring_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitoring_alerts", x => x.id);
                    table.ForeignKey(
                        name: "FK_monitoring_alerts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resolution_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resolution_cases", x => x.id);
                    table.ForeignKey(
                        name: "FK_resolution_cases_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    plan = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    payment_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_monitorings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_monitorings", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehicle_monitorings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vehicle_monitorings_vehicles_user_id_vehicle_id",
                        columns: x => new { x.user_id, x.vehicle_id },
                        principalTable: "vehicles",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehicle_reports_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_alerts_user_id",
                table: "monitoring_alerts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_resolution_cases_user_id",
                table: "resolution_cases",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_user_id_service",
                table: "subscriptions",
                columns: new[] { "user_id", "service" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_monitorings_user_id",
                table: "vehicle_monitorings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_monitorings_user_id_vehicle_id",
                table: "vehicle_monitorings",
                columns: new[] { "user_id", "vehicle_id" },
                unique: true,
                filter: "active");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_reports_user_id",
                table: "vehicle_reports",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "monitoring_alerts");

            migrationBuilder.DropTable(
                name: "resolution_cases");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "vehicle_monitorings");

            migrationBuilder.DropTable(
                name: "vehicle_reports");

            migrationBuilder.DropColumn(
                name: "session_version",
                table: "users");
        }
    }
}
