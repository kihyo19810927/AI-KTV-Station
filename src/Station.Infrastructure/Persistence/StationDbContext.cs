using Microsoft.EntityFrameworkCore;
using Station.Domain.Models;

namespace Station.Infrastructure.Persistence;

public sealed class StationDbContext(DbContextOptions<StationDbContext> options) : DbContext(options)
{
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<SongArtist> SongArtists => Set<SongArtist>();
    public DbSet<MediaSource> MediaSources => Set<MediaSource>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<MediaTrack> MediaTracks => Set<MediaTrack>();
    public DbSet<TrackMapping> TrackMappings => Set<TrackMapping>();
    public DbSet<RoomSession> RoomSessions => Set<RoomSession>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<QueueItem> QueueItems => Set<QueueItem>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<PlayHistory> PlayHistory => Set<PlayHistory>();
    public DbSet<ScanRun> ScanRuns => Set<ScanRun>();
    public DbSet<PlaybackError> PlaybackErrors => Set<PlaybackError>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Song>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.NormalizedTitle).HasMaxLength(300);
            e.Property(x => x.SimplifiedTitle).HasMaxLength(300);
            e.Property(x => x.TraditionalTitle).HasMaxLength(300);
            e.Property(x => x.TitlePinyin).HasMaxLength(1200);
            e.Property(x => x.TitleInitials).HasMaxLength(300);
            e.Property(x => x.CompactTitle).HasMaxLength(300);
            e.Property(x => x.Availability).HasConversion<string>();
            e.HasIndex(x => x.NormalizedTitle);
        });
        model.Entity<Artist>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.NormalizedName).HasMaxLength(200);
            e.Property(x => x.SimplifiedName).HasMaxLength(200);
            e.Property(x => x.TraditionalName).HasMaxLength(200);
            e.Property(x => x.Pinyin).HasMaxLength(800);
            e.Property(x => x.Initials).HasMaxLength(200);
            e.Property(x => x.CompactName).HasMaxLength(200);
            e.HasIndex(x => x.NormalizedName);
        });
        model.Entity<SongArtist>(e => { e.HasKey(x => new { x.SongId, x.ArtistId }); e.HasIndex(x => new { x.SongId, x.Order }).IsUnique(); });
        model.Entity<MediaSource>(e => { e.Property(x => x.Name).HasMaxLength(200); e.Property(x => x.Availability).HasConversion<string>(); });
        model.Entity<MediaFile>(e => { e.Property(x => x.RelativePath).HasMaxLength(1024); e.Property(x => x.Availability).HasConversion<string>(); e.HasIndex(x => new { x.MediaSourceId, x.RelativePath }).IsUnique(); });
        model.Entity<MediaTrack>(e => { e.Property(x => x.Type).HasConversion<string>(); e.HasIndex(x => new { x.MediaFileId, x.StreamId, x.Type }).IsUnique(); });
        model.Entity<TrackMapping>().HasKey(x => x.MediaFileId);
        model.Entity<RoomSession>(e => { e.Property(x => x.Status).HasConversion<string>(); e.HasIndex(x => x.JoinCode).IsUnique(); });
        model.Entity<Guest>(e => { e.Property(x => x.TokenHash).HasMaxLength(128); e.HasIndex(x => x.TokenHash).IsUnique(); });
        model.Entity<QueueItem>(e => { e.Property(x => x.Status).HasConversion<string>(); e.HasIndex(x => new { x.RoomSessionId, x.Position }).IsUnique(); });
        model.Entity<Favorite>().HasKey(x => new { x.GuestId, x.SongId });
        model.Entity<PlayHistory>().Property(x => x.Outcome).HasConversion<string>();
        model.Entity<ScanRun>().Property(x => x.Status).HasConversion<string>();
        model.Entity<PlaybackError>().Property(x => x.ErrorCode).HasMaxLength(100);
    }
}
