using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Station.Infrastructure.Persistence.Migrations;

[DbContext(typeof(StationDbContext))]
[Migration("20260910073000_AddLyricsSidecarMetadata")]
public partial class AddLyricsSidecarMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "LyricsFormat", table: "MediaFiles", type: "TEXT", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>(name: "LyricsRelativePath", table: "MediaFiles", type: "TEXT", maxLength: 1024, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LyricsFormat", table: "MediaFiles");
        migrationBuilder.DropColumn(name: "LyricsRelativePath", table: "MediaFiles");
    }
}
