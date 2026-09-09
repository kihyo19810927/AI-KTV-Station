using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Station.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleOpenRoomConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OpenSlot",
                table: "RoomSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomSessions_OpenSlot",
                table: "RoomSessions",
                column: "OpenSlot",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoomSessions_OpenSlot",
                table: "RoomSessions");

            migrationBuilder.DropColumn(
                name: "OpenSlot",
                table: "RoomSessions");
        }
    }
}
