using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Station.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AvatarUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    PinHash = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfileDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileDevices_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfilePlaylists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    IsFamilyShared = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilePlaylists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfilePlaylists_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfilePlaylistItems",
                columns: table => new
                {
                    ProfilePlaylistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SongId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<long>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilePlaylistItems", x => new { x.ProfilePlaylistId, x.SongId });
                    table.ForeignKey(
                        name: "FK_ProfilePlaylistItems_ProfilePlaylists_ProfilePlaylistId",
                        column: x => x.ProfilePlaylistId,
                        principalTable: "ProfilePlaylists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfilePlaylistItems_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileDevices_TokenHash",
                table: "ProfileDevices",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileDevices_UserProfileId",
                table: "ProfileDevices",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePlaylistItems_ProfilePlaylistId_Position",
                table: "ProfilePlaylistItems",
                columns: new[] { "ProfilePlaylistId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePlaylistItems_SongId",
                table: "ProfilePlaylistItems",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePlaylists_UserProfileId_Kind_Name",
                table: "ProfilePlaylists",
                columns: new[] { "UserProfileId", "Kind", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_IsArchived_LastUsedAt",
                table: "UserProfiles",
                columns: new[] { "IsArchived", "LastUsedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileDevices");

            migrationBuilder.DropTable(
                name: "ProfilePlaylistItems");

            migrationBuilder.DropTable(
                name: "ProfilePlaylists");

            migrationBuilder.DropTable(
                name: "UserProfiles");
        }
    }
}
