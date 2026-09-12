using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Station.Infrastructure.Persistence.Migrations;

[DbContext(typeof(StationDbContext))]
[Migration("20260911150500_UpgradeLegacyQueueLimit")]
public sealed class UpgradeLegacyQueueLimit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("UPDATE RoomSessions SET MaxQueuedSongsPerGuest = 100 WHERE MaxQueuedSongsPerGuest = 10;");

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A user may intentionally choose 100 after upgrade; never rewrite that preference on rollback.
    }
}
