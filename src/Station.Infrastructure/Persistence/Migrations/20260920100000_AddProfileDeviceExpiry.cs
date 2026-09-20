using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Station.Infrastructure.Persistence;

#nullable disable

namespace Station.Infrastructure.Persistence.Migrations;

[DbContext(typeof(StationDbContext))]
[Migration("20260920100000_AddProfileDeviceExpiry")]
public partial class AddProfileDeviceExpiry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ExpiresAt",
            table: "ProfileDevices",
            type: "TEXT",
            nullable: false,
            defaultValue: DateTimeOffset.MaxValue);
        migrationBuilder.Sql("UPDATE ProfileDevices SET ExpiresAt = datetime(CreatedAt, '+90 days')");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "ExpiresAt", table: "ProfileDevices");
}
