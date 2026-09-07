using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Station.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSongSearchFts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE SongSearchDocuments (
                    SongId TEXT NOT NULL PRIMARY KEY,
                    Title TEXT NOT NULL,
                    NormalizedTitle TEXT NOT NULL,
                    Artists TEXT NOT NULL,
                    Language TEXT NULL,
                    Category TEXT NULL,
                    Quality TEXT NULL,
                    Year INTEGER NULL,
                    Availability TEXT NOT NULL,
                    Terms TEXT NOT NULL
                );
                CREATE INDEX IX_SongSearchDocuments_Filters ON SongSearchDocuments(Language, Category, Quality, Year);
                CREATE VIRTUAL TABLE SongSearchFts USING fts5(
                    Terms,
                    content='SongSearchDocuments',
                    content_rowid='rowid',
                    tokenize='unicode61 remove_diacritics 2'
                );
                CREATE TRIGGER SongSearchDocuments_ai AFTER INSERT ON SongSearchDocuments BEGIN
                    INSERT INTO SongSearchFts(rowid, Terms) VALUES (new.rowid, new.Terms);
                END;
                CREATE TRIGGER SongSearchDocuments_ad AFTER DELETE ON SongSearchDocuments BEGIN
                    INSERT INTO SongSearchFts(SongSearchFts, rowid, Terms) VALUES ('delete', old.rowid, old.Terms);
                END;
                CREATE TRIGGER SongSearchDocuments_au AFTER UPDATE ON SongSearchDocuments BEGIN
                    INSERT INTO SongSearchFts(SongSearchFts, rowid, Terms) VALUES ('delete', old.rowid, old.Terms);
                    INSERT INTO SongSearchFts(rowid, Terms) VALUES (new.rowid, new.Terms);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS SongSearchDocuments_au;
                DROP TRIGGER IF EXISTS SongSearchDocuments_ad;
                DROP TRIGGER IF EXISTS SongSearchDocuments_ai;
                DROP TABLE IF EXISTS SongSearchFts;
                DROP TABLE IF EXISTS SongSearchDocuments;
                """);
        }
    }
}
