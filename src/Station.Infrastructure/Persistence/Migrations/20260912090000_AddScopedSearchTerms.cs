using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Persistence.Migrations;

[DbContext(typeof(StationDbContext))]
[Migration("20260912090000_AddScopedSearchTerms")]
public partial class AddScopedSearchTerms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE SongSearchDocuments ADD COLUMN TitleTerms TEXT NOT NULL DEFAULT ''; ALTER TABLE SongSearchDocuments ADD COLUMN ArtistTerms TEXT NOT NULL DEFAULT '';");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS SongSearchDocuments_au; DROP TRIGGER IF EXISTS SongSearchDocuments_ad; DROP TRIGGER IF EXISTS SongSearchDocuments_ai; DROP TABLE IF EXISTS SongSearchFts;");
        migrationBuilder.Sql("CREATE VIRTUAL TABLE SongSearchFts USING fts5(TitleTerms, ArtistTerms, content='SongSearchDocuments', content_rowid='rowid', tokenize='unicode61 remove_diacritics 2');");
        migrationBuilder.Sql("CREATE TRIGGER SongSearchDocuments_ai AFTER INSERT ON SongSearchDocuments BEGIN INSERT INTO SongSearchFts(rowid, TitleTerms, ArtistTerms) VALUES (new.rowid, new.TitleTerms, new.ArtistTerms); END;");
        migrationBuilder.Sql("CREATE TRIGGER SongSearchDocuments_ad AFTER DELETE ON SongSearchDocuments BEGIN INSERT INTO SongSearchFts(SongSearchFts, rowid, TitleTerms, ArtistTerms) VALUES ('delete', old.rowid, old.TitleTerms, old.ArtistTerms); END;");
        migrationBuilder.Sql("CREATE TRIGGER SongSearchDocuments_au AFTER UPDATE ON SongSearchDocuments BEGIN INSERT INTO SongSearchFts(SongSearchFts, rowid, TitleTerms, ArtistTerms) VALUES ('delete', old.rowid, old.TitleTerms, old.ArtistTerms); INSERT INTO SongSearchFts(rowid, TitleTerms, ArtistTerms) VALUES (new.rowid, new.TitleTerms, new.ArtistTerms); END;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS SongSearchDocuments_au; DROP TRIGGER IF EXISTS SongSearchDocuments_ad; DROP TRIGGER IF EXISTS SongSearchDocuments_ai; DROP TABLE IF EXISTS SongSearchFts;");
        migrationBuilder.Sql("ALTER TABLE SongSearchDocuments DROP COLUMN TitleTerms; ALTER TABLE SongSearchDocuments DROP COLUMN ArtistTerms;");
        migrationBuilder.Sql("CREATE VIRTUAL TABLE SongSearchFts USING fts5(Terms, content='SongSearchDocuments', content_rowid='rowid', tokenize='unicode61 remove_diacritics 2');");
        migrationBuilder.Sql("CREATE TRIGGER SongSearchDocuments_ai AFTER INSERT ON SongSearchDocuments BEGIN INSERT INTO SongSearchFts(rowid, Terms) VALUES (new.rowid, new.Terms); END;");
        migrationBuilder.Sql("CREATE TRIGGER SongSearchDocuments_ad AFTER DELETE ON SongSearchDocuments BEGIN INSERT INTO SongSearchFts(SongSearchFts, rowid, Terms) VALUES ('delete', old.rowid, old.Terms); END;");
        migrationBuilder.Sql("CREATE TRIGGER SongSearchDocuments_au AFTER UPDATE ON SongSearchDocuments BEGIN INSERT INTO SongSearchFts(SongSearchFts, rowid, Terms) VALUES ('delete', old.rowid, old.Terms); INSERT INTO SongSearchFts(rowid, Terms) VALUES (new.rowid, new.Terms); END;");
    }
}
