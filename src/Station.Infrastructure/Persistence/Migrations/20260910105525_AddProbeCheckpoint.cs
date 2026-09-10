using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Station.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProbeCheckpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CachedFiles",
                table: "ScanRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "IndexedFiles",
                table: "ScanRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Phase",
                table: "ScanRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "ProbeAttempts",
                table: "ScanRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<double>(
                name: "ProbeMilliseconds",
                table: "ScanRuns",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<long>(
                name: "ProbedFiles",
                table: "ScanRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ProbeFingerprint",
                table: "MediaFiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CachedFiles",
                table: "ScanRuns");

            migrationBuilder.DropColumn(
                name: "IndexedFiles",
                table: "ScanRuns");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "ScanRuns");

            migrationBuilder.DropColumn(
                name: "ProbeAttempts",
                table: "ScanRuns");

            migrationBuilder.DropColumn(
                name: "ProbeMilliseconds",
                table: "ScanRuns");

            migrationBuilder.DropColumn(
                name: "ProbedFiles",
                table: "ScanRuns");

            migrationBuilder.DropColumn(
                name: "ProbeFingerprint",
                table: "MediaFiles");
        }
    }
}
