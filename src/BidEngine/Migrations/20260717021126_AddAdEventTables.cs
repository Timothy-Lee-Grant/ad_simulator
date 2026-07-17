using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BidEngine.Migrations
{
    /// <inheritdoc />
    public partial class AddAdEventTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ad_event_aggregates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    impression_count = table.Column<long>(type: "bigint", nullable: false),
                    click_count = table.Column<long>(type: "bigint", nullable: false),
                    spend_total = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    experiment_id = table.Column<string>(type: "text", nullable: true),
                    variation_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_event_aggregates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ad_event_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    timestamp_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    placement_id = table.Column<string>(type: "text", nullable: false),
                    request_id = table.Column<string>(type: "text", nullable: false),
                    experiment_id = table.Column<string>(type: "text", nullable: true),
                    variation_id = table.Column<string>(type: "text", nullable: true),
                    payload_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_event_logs", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ad_event_aggregates");

            migrationBuilder.DropTable(
                name: "ad_event_logs");
        }
    }
}
