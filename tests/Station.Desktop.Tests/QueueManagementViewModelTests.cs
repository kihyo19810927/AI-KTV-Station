using Station.Application.Common;
using Station.Application.Queue;
using Station.Application.Rooms;
using Station.Desktop.Services;
using Station.Desktop.ViewModels;
using Station.Domain.Models;

namespace Station.Desktop.Tests;

public sealed class QueueManagementViewModelTests
{
    [Fact]
    public async Task Refresh_without_room_is_safe_and_empty()
    {
        var viewModel = new QueueManagementViewModel(new FakeQueue(), new HostRoomContext());
        await viewModel.RefreshAsync();
        Assert.Empty(viewModel.Items);
        Assert.Contains("开启房间", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Refresh_projects_active_room_queue_without_paths()
    {
        var identity = new RoomIdentity(Guid.NewGuid(), Guid.NewGuid(), "主持人", RoomRole.Host, DateTimeOffset.UtcNow.AddHours(1));
        var context = new HostRoomContext { Identity = identity };
        var service = new FakeQueue { Entries = [Entry("第一首", 1024), Entry("第二首", 2048)] };
        var viewModel = new QueueManagementViewModel(service, context);
        await viewModel.RefreshAsync();
        Assert.Equal(["第一首", "第二首"], viewModel.Items.Select(x => x.Title));
        Assert.DoesNotContain(typeof(QueueEntry).GetProperties(), x => x.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    private static QueueEntry Entry(string title, long position) => new(Guid.NewGuid(), Guid.NewGuid(), title, Guid.NewGuid(), "访客", position, QueueItemStatus.Waiting, DateTimeOffset.UtcNow);
    private sealed class FakeQueue : IRoomQueueService
    {
        public IReadOnlyList<QueueEntry> Entries { get; set; } = [];
        public Task<Result<IReadOnlyList<QueueEntry>>> ListAsync(RoomIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(Result<IReadOnlyList<QueueEntry>>.Success(Entries));
        public Task<Result<bool>> RemoveAsync(RoomIdentity identity, Guid itemId, CancellationToken cancellationToken = default) => Task.FromResult(Result<bool>.Success(true));
        public Task<Result<QueueEntry>> MoveToTopAsync(RoomIdentity identity, Guid itemId, CancellationToken cancellationToken = default) => Task.FromResult(Result<QueueEntry>.Success(Entries.Single(x => x.Id == itemId)));
        public Task<Result<QueueEntry>> InsertNextAsync(RoomIdentity identity, Guid itemId, CancellationToken cancellationToken = default) => Task.FromResult(Result<QueueEntry>.Success(Entries.Single(x => x.Id == itemId)));
        public Task<Result<IReadOnlyList<QueueEntry>>> ReorderBeforeAsync(RoomIdentity identity, Guid itemId, Guid? beforeItemId, CancellationToken cancellationToken = default) => Task.FromResult(Result<IReadOnlyList<QueueEntry>>.Success(Entries));
    }
}
