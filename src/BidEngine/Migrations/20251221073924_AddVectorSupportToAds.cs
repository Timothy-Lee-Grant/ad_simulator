using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BidEngine.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorSupportToAds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // If the tables already exist (created outside of EF migrations), avoid recreating them
            // and only add the missing columns we need for vector support.
            migrationBuilder.Sql(@"ALTER TABLE ads ADD COLUMN IF NOT EXISTS description text;");
            migrationBuilder.Sql(@"ALTER TABLE ads ADD COLUMN IF NOT EXISTS embedding jsonb;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert only the columns this migration adds, if they exist.
            migrationBuilder.Sql(@"ALTER TABLE ads DROP COLUMN IF EXISTS embedding;");
            migrationBuilder.Sql(@"ALTER TABLE ads DROP COLUMN IF EXISTS description;");
        }
    }
}
