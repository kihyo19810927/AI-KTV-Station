using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Station.Infrastructure.Persistence;
using Station.Application.Catalog;
using Station.Application.Common;
using Station.Application.Health;
using Station.Application.Queue;
using Station.Application.Rooms;
using Station.Application.Search;

namespace Station.Desktop.Services;

/// <summary>Runs an entire database use case off the Dispatcher with its own scoped context.</summary>
public sealed class DesktopDataFacade(Func<IServiceProvider> server) :
    IRoomQueueService, IRoomLifecycleService, IRoomHostAdministration,
    ISongSearchIndex, IArtistBrowseService, ICatalogAdminService, ICatalogJsonImportService, IStationHealthService
{
    private Task<T> Run<TService, T>(Func<TService, Task<T>> operation, CancellationToken token, bool readOnly = false) where TService : notnull =>
        Task.Run(async () =>
        {
            await using var scope = server().CreateAsyncScope();
            if (readOnly) scope.ServiceProvider.GetRequiredService<StationDbContext>().ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return await operation(scope.ServiceProvider.GetRequiredService<TService>()).ConfigureAwait(false);
        }, token);

    private Task Run<TService>(Func<TService, Task> operation, CancellationToken token) where TService : notnull =>
        Task.Run(async () =>
        {
            await using var scope = server().CreateAsyncScope();
            await operation(scope.ServiceProvider.GetRequiredService<TService>()).ConfigureAwait(false);
        }, token);

    public Task<Result<QueueEntry>> RequestAsync(RoomIdentity identity, Guid songId, CancellationToken cancellationToken = default) =>
        Run((RoomQueueService x) => x.RequestAsync(identity, songId, cancellationToken), cancellationToken);
    public Task<Result<IReadOnlyList<QueueEntry>>> ListAsync(RoomIdentity identity, CancellationToken cancellationToken = default) =>
        Run((RoomQueueService x) => x.ListAsync(identity, cancellationToken), cancellationToken, readOnly: true);
    public Task<Result<bool>> RemoveAsync(RoomIdentity identity, Guid itemId, CancellationToken cancellationToken = default) =>
        Run((RoomQueueService x) => x.RemoveAsync(identity, itemId, cancellationToken), cancellationToken);
    public Task<Result<QueueEntry>> MoveToTopAsync(RoomIdentity identity, Guid itemId, CancellationToken cancellationToken = default) =>
        Run((RoomQueueService x) => x.MoveToTopAsync(identity, itemId, cancellationToken), cancellationToken);
    public Task<Result<QueueEntry>> InsertNextAsync(RoomIdentity identity, Guid itemId, CancellationToken cancellationToken = default) =>
        Run((RoomQueueService x) => x.InsertNextAsync(identity, itemId, cancellationToken), cancellationToken);
    public Task<Result<IReadOnlyList<QueueEntry>>> ReorderBeforeAsync(RoomIdentity identity, Guid itemId, Guid? beforeItemId, CancellationToken cancellationToken = default) =>
        Run((RoomQueueService x) => x.ReorderBeforeAsync(identity, itemId, beforeItemId, cancellationToken), cancellationToken);
    public Task<Result<RoomAdminDetails>> CreateAsync(int maxQueuedSongsPerGuest = 100, CancellationToken cancellationToken = default) =>
        Run((RoomLifecycleService x) => x.CreateAsync(maxQueuedSongsPerGuest, cancellationToken), cancellationToken);
    public Task<Result<RoomAdminDetails>> CloseAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        Run((RoomLifecycleService x) => x.CloseAsync(roomId, cancellationToken), cancellationToken);
    public Task<Result<RoomAdminDetails?>> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        Run((RoomLifecycleService x) => x.GetCurrentAsync(cancellationToken), cancellationToken, readOnly: true);
    public Task<Result<RoomAdminDetails>> SetQueueLimitAsync(Guid roomId, int maxQueuedSongsPerGuest, CancellationToken cancellationToken = default) =>
        Run((RoomLifecycleService x) => x.SetQueueLimitAsync(roomId, maxQueuedSongsPerGuest, cancellationToken), cancellationToken);
    public Task<Result<IssuedRoomToken>> IssueHostAsync(Guid roomId, string nickname, CancellationToken cancellationToken = default) =>
        Run((RoomAuthenticationService x) => x.IssueHostAsync(roomId, nickname, cancellationToken), cancellationToken);
    public Task<Result<bool>> RevokeAsync(Guid guestId, CancellationToken cancellationToken = default) =>
        Run((RoomAuthenticationService x) => x.RevokeAsync(guestId, cancellationToken), cancellationToken);
    public Task<Result<IReadOnlyList<RoomGuestAdminDetails>>> ListGuestsAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        Run((RoomAuthenticationService x) => x.ListGuestsAsync(roomId, cancellationToken), cancellationToken, readOnly: true);
    public Task<Result<SongSearchPage>> SearchAsync(SongSearchQuery query, CancellationToken cancellationToken = default) =>
        Run((ISongSearchIndex x) => x.SearchAsync(query, cancellationToken), cancellationToken, readOnly: true);
    public Task RebuildAsync(CancellationToken cancellationToken = default) =>
        Run((ISongSearchIndex x) => x.RebuildAsync(cancellationToken), cancellationToken);
    public Task UpsertAsync(IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken = default) =>
        Run((ISongSearchIndex x) => x.UpsertAsync(songIds, cancellationToken), cancellationToken);
    public Task<IReadOnlyList<ArtistBrowseItem>> ListAsync(string? artistGroup, int limit = 200, CancellationToken cancellationToken = default) =>
        Run((IArtistBrowseService x) => x.ListAsync(artistGroup, limit, cancellationToken), cancellationToken, readOnly: true);
    public Task<Result<SongAdminDetails>> GetAsync(Guid songId, CancellationToken cancellationToken = default) =>
        Run((ICatalogAdminService x) => x.GetAsync(songId, cancellationToken), cancellationToken, readOnly: true);
    public Task<Result<SongAdminDetails>> UpdateAsync(Guid songId, SongMetadataUpdate update, CancellationToken cancellationToken = default) =>
        Run((ICatalogAdminService x) => x.UpdateAsync(songId, update, cancellationToken), cancellationToken);
    public Task<Result<CatalogImportResult>> ImportAsync(string indexPath, string mountRoot, IProgress<CatalogImportProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Run((ICatalogJsonImportService x) => x.ImportAsync(indexPath, mountRoot, progress, cancellationToken), cancellationToken);
    public Task<StationHealthSnapshot> CheckAsync(CancellationToken cancellationToken = default) =>
        Run((IStationHealthService x) => x.CheckAsync(cancellationToken), cancellationToken, readOnly: true);
}
