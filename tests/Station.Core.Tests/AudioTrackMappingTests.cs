using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Playback;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Playback;

namespace Station.Core.Tests;

public sealed class AudioTrackMappingTests
{
    private static readonly MediaTrack[] TitledTracks =
    [
        new() { StreamId = 1, Type = MediaTrackType.Audio, Title = "伴奏" },
        new() { StreamId = 2, Type = MediaTrackType.Audio, Title = "原唱" },
        new() { StreamId = 3, Type = MediaTrackType.Subtitle, Title = "字幕" },
    ];

    [Fact]
    public void Classifier_recognizes_chinese_and_english_track_titles()
    {
        var tracks = TitledTracks.Concat([
            new MediaTrack { StreamId = 4, Type = MediaTrackType.Audio, Title = "Instrumental" },
            new MediaTrack { StreamId = 5, Type = MediaTrackType.Audio, Title = "Lead Vocal" },
        ]).ToArray();
        var candidates = new AudioTrackClassifier().Classify(tracks);
        Assert.Contains(candidates, x => x.StreamId == 1 && x.Purpose == TrackPurpose.Backing && x.Confidence == 100);
        Assert.Contains(candidates, x => x.StreamId == 2 && x.Purpose == TrackPurpose.Vocal && x.Confidence == 100);
        Assert.Contains(candidates, x => x.StreamId == 4 && x.Purpose == TrackPurpose.Backing);
        Assert.Contains(candidates, x => x.StreamId == 5 && x.Purpose == TrackPurpose.Vocal);
    }

    [Fact]
    public async Task Automatic_mapping_persists_only_high_confidence_candidates()
    {
        var repository = new MemoryRepository();
        var service = new AudioTrackMappingService(repository, new AudioTrackClassifier());
        var result = await service.ResolveAsync(Guid.NewGuid(), TitledTracks);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.BackingTrackId);
        Assert.Equal(2, result.Value.VocalTrackId);
        Assert.Equal(TrackMappingSource.Automatic, result.Value.Source);
        Assert.False(repository.Stored!.IsManualOverride);

        var ambiguous = TitledTracks.Take(2).Select((_, index) => new MediaTrack { StreamId = index + 10, Type = MediaTrackType.Audio }).ToArray();
        var ambiguousResult = await service.ResolveAsync(Guid.NewGuid(), ambiguous);
        Assert.Null(ambiguousResult.Value.BackingTrackId);
        Assert.Null(ambiguousResult.Value.VocalTrackId);
        Assert.All(ambiguousResult.Value.Candidates, x => Assert.Equal(25, x.Confidence));
    }

    [Fact]
    public async Task Manual_override_has_priority_over_conflicting_titles()
    {
        var mediaFileId = Guid.NewGuid();
        var repository = new MemoryRepository();
        var service = new AudioTrackMappingService(repository, new AudioTrackClassifier());
        Assert.True((await service.SaveManualOverrideAsync(mediaFileId, TitledTracks, 2, 1)).IsSuccess);
        var resolved = await service.ResolveAsync(mediaFileId, TitledTracks);
        Assert.Equal(TrackMappingSource.Manual, resolved.Value.Source);
        Assert.Equal(2, resolved.Value.BackingTrackId);
        Assert.Equal(1, resolved.Value.VocalTrackId);
    }

    [Theory]
    [InlineData(1, 1, "track_mapping.duplicate")]
    [InlineData(99, 2, "track_mapping.audio_track_not_found")]
    public async Task Invalid_manual_override_is_not_saved(int backing, int vocal, string code)
    {
        var repository = new MemoryRepository();
        var result = await new AudioTrackMappingService(repository, new AudioTrackClassifier())
            .SaveManualOverrideAsync(Guid.NewGuid(), TitledTracks, backing, vocal);
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Ef_repository_round_trips_manual_override()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, contextOwnsConnection: true).Options);
        await database.Database.EnsureCreatedAsync();
        var source = new MediaSource { Name = "fixture", RootPath = "fixture" };
        var song = new Song { Title = "test" };
        var file = new MediaFile { Song = song, MediaSource = source, RelativePath = "test.mkv" };
        database.MediaFiles.Add(file);
        await database.SaveChangesAsync();
        var repository = new EfTrackMappingRepository(database);
        await repository.SaveAsync(new TrackMapping { MediaFileId = file.Id, BackingTrackId = 2, VocalTrackId = 1, IsManualOverride = true });
        database.ChangeTracker.Clear();
        var stored = await repository.FindAsync(file.Id);
        Assert.NotNull(stored);
        Assert.True(stored.IsManualOverride);
        Assert.Equal(2, stored.BackingTrackId);
        Assert.Equal(1, stored.VocalTrackId);
    }

    private sealed class MemoryRepository : ITrackMappingRepository
    {
        public TrackMapping? Stored { get; private set; }
        public int SaveCount { get; private set; }
        public Task<TrackMapping?> FindAsync(Guid mediaFileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored?.MediaFileId == mediaFileId ? Stored : null);
        public Task SaveAsync(TrackMapping mapping, CancellationToken cancellationToken = default)
        {
            Stored = mapping;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
