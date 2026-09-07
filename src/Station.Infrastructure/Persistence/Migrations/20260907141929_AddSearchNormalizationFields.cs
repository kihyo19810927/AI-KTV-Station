using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Station.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchNormalizationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompactTitle",
                table: "Songs",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SimplifiedTitle",
                table: "Songs",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TitleInitials",
                table: "Songs",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TitlePinyin",
                table: "Songs",
                type: "TEXT",
                maxLength: 1200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TraditionalTitle",
                table: "Songs",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompactName",
                table: "Artists",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SimplifiedName",
                table: "Artists",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TraditionalName",
                table: "Artists",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompactTitle",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "SimplifiedTitle",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "TitleInitials",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "TitlePinyin",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "TraditionalTitle",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "CompactName",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "SimplifiedName",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "TraditionalName",
                table: "Artists");
        }
    }
}
