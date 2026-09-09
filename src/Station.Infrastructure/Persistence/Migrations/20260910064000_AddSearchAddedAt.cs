using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Station.Infrastructure.Persistence.Migrations;

[DbContext(typeof(StationDbContext))]
[Migration("20260910064000_AddSearchAddedAt")]
public partial class AddSearchAddedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER TABLE SongSearchDocuments ADD COLUMN AddedAt TEXT NULL;");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER TABLE SongSearchDocuments DROP COLUMN AddedAt;");
}
