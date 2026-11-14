using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetProof.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionAudience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "audience",
                table: "subscriptions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "business");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "audience",
                table: "subscriptions");
        }
    }
}
