using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Station.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalizeArtists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "__ArtistCanonicalMap" (
                    "DuplicateId" TEXT NOT NULL PRIMARY KEY,
                    "CanonicalId" TEXT NOT NULL
                );

                INSERT INTO "__ArtistCanonicalMap" ("DuplicateId", "CanonicalId")
                SELECT duplicate."Id", MIN(canonical."Id")
                FROM "Artists" AS duplicate
                INNER JOIN "Artists" AS canonical
                    ON canonical."NormalizedName" = duplicate."NormalizedName"
                WHERE trim(duplicate."NormalizedName") <> ''
                GROUP BY duplicate."Id"
                HAVING duplicate."Id" <> MIN(canonical."Id");

                DELETE FROM "SongArtists"
                WHERE "ArtistId" IN (SELECT "DuplicateId" FROM "__ArtistCanonicalMap")
                  AND EXISTS (
                      SELECT 1
                      FROM "SongArtists" AS canonicalLink
                      INNER JOIN "__ArtistCanonicalMap" AS artistMap
                          ON artistMap."CanonicalId" = canonicalLink."ArtistId"
                      WHERE canonicalLink."SongId" = "SongArtists"."SongId"
                        AND artistMap."DuplicateId" = "SongArtists"."ArtistId"
                  );

                UPDATE "SongArtists"
                SET "ArtistId" = (
                    SELECT "CanonicalId"
                    FROM "__ArtistCanonicalMap"
                    WHERE "DuplicateId" = "SongArtists"."ArtistId"
                )
                WHERE "ArtistId" IN (SELECT "DuplicateId" FROM "__ArtistCanonicalMap");

                DELETE FROM "Artists"
                WHERE "Id" IN (SELECT "DuplicateId" FROM "__ArtistCanonicalMap");

                DROP TABLE "__ArtistCanonicalMap";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Artists_NormalizedName",
                table: "Artists");

            migrationBuilder.CreateIndex(
                name: "IX_Artists_NormalizedName",
                table: "Artists",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Artists_NormalizedName",
                table: "Artists");

            migrationBuilder.CreateIndex(
                name: "IX_Artists_NormalizedName",
                table: "Artists",
                column: "NormalizedName");
        }
    }
}
