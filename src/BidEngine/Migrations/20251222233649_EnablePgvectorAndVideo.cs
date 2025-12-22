using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace BidEngine.Migrations
{
    /// <inheritdoc />
    public partial class EnablePgvectorAndVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure the pgvector extension exists before attempting vector operations
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS vector;");

            // Alter ads.embedding to vector(384) only if it is not already that type.
            migrationBuilder.Sql(@"
DO $$
BEGIN
  IF (SELECT format_type(a.atttypid, a.atttypmod)
      FROM pg_attribute a
      WHERE a.attrelid = 'ads'::regclass AND a.attname = 'embedding') != 'vector(384)'
  THEN
    -- Attempt to change type; if the current data is jsonb array of numbers this should be adapted before running in prod.
    ALTER TABLE ads ALTER COLUMN embedding TYPE vector(384) USING (embedding::vector);
  END IF;
END
$$;
");

            // Create videos table only if it doesn't already exist (idempotent)
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS videos (
    id uuid NOT NULL,
    title text NOT NULL,
    description text,
    embedding vector(384),
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT ""PK_videos"" PRIMARY KEY (id)
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
                        // Drop videos table if it exists
                        migrationBuilder.Sql("DROP TABLE IF EXISTS videos;");

                        // Revert ads.embedding back to jsonb only if it is currently a vector
                        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF (SELECT format_type(a.atttypid, a.atttypmod)
            FROM pg_attribute a
            WHERE a.attrelid = 'ads'::regclass AND a.attname = 'embedding') = 'vector(384)'
    THEN
        ALTER TABLE ads ALTER COLUMN embedding TYPE jsonb USING (embedding::jsonb);
    END IF;
END
$$;
");
        }
    }
}
