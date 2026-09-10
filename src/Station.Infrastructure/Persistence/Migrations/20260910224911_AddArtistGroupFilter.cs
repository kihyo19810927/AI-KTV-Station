using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Station.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArtistGroupFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArtistGroup",
                table: "Songs",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "其他");
            migrationBuilder.Sql("ALTER TABLE SongSearchDocuments ADD COLUMN ArtistGroup TEXT NOT NULL DEFAULT '其他';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE SongSearchDocuments DROP COLUMN ArtistGroup;");
            migrationBuilder.DropColumn(
                name: "ArtistGroup",
                table: "Songs");
        }
    }
}
