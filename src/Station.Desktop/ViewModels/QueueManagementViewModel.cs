using System.Collections.ObjectModel;
using System.Windows.Input;
using Station.Application.Common;
using Station.Application.Queue;
using Station.Desktop.Services;

namespace Station.Desktop.ViewModels;

public sealed class QueueManagementViewModel : ObservableObject
{
    private readonly IRoomQueueService queue;
    private readonly HostRoomContext roomContext;
    private string statusMessage = "请先在“房间与二维码”中开启房间";
    public ObservableCollection<QueueEntry> Items { get; } = [];
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public ICommand RefreshCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand MoveTopCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }

    public QueueManagementViewModel(IRoomQueueService queue, HostRoomContext roomContext)
    {
        this.queue = queue;
        this.roomContext = roomContext;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        RemoveCommand = new AsyncRelayCommand<QueueEntry>(item => MutateAsync(() => queue.RemoveAsync(RequireIdentity(), item.Id)));
        MoveTopCommand = new AsyncRelayCommand<QueueEntry>(MoveTopAsync);
        MoveUpCommand = new AsyncRelayCommand<QueueEntry>(MoveUpAsync);
        MoveDownCommand = new AsyncRelayCommand<QueueEntry>(MoveDownAsync);
    }

    public async Task RefreshAsync()
    {
        try
        {
            if (roomContext.Identity is null) { Items.Clear(); StatusMessage = "请先在“房间与二维码”中开启房间"; return; }
            var result = await queue.ListAsync(roomContext.Identity);
            if (result.IsFailure) { ShowError(result.Error); return; }
            Replace(result.Value);
        }
        catch (Exception)
        {
            ShowUnexpectedError();
        }
    }

    public Task MoveTopAsync(QueueEntry item) => MutateAsync(() => queue.MoveToTopAsync(RequireIdentity(), item.Id));

    private async Task MoveUpAsync(QueueEntry item)
    {
        var index = Items.IndexOf(item); if (index <= 0) return;
        await ReorderAsync(item.Id, Items[index - 1].Id);
    }

    private async Task MoveDownAsync(QueueEntry item)
    {
        var index = Items.IndexOf(item); if (index < 0 || index == Items.Count - 1) return;
        var before = index + 2 < Items.Count ? Items[index + 2].Id : (Guid?)null;
        await ReorderAsync(item.Id, before);
    }

    private async Task ReorderAsync(Guid itemId, Guid? beforeId)
    {
        try
        {
            var result = await queue.ReorderBeforeAsync(RequireIdentity(), itemId, beforeId);
            if (result.IsFailure) { ShowError(result.Error); return; }
            Replace(result.Value);
        }
        catch (Exception)
        {
            ShowUnexpectedError();
        }
    }

    public Task MoveBeforeAsync(QueueEntry item, QueueEntry? before) => ReorderAsync(item.Id, before?.Id);

    private async Task MutateAsync<T>(Func<Task<Result<T>>> operation)
    {
        try
        {
            var result = await operation();
            if (result.IsFailure)
            {
                ShowError(result.Error);
                return;
            }
            await RefreshAsync();
        }
        catch (Exception)
        {
            ShowUnexpectedError();
        }
    }

    private Station.Application.Rooms.RoomIdentity RequireIdentity() => roomContext.Identity ?? throw new InvalidOperationException("No active host room.");
    private void Replace(IEnumerable<QueueEntry> items) { Items.Clear(); foreach (var item in items) Items.Add(item); StatusMessage = Items.Count == 0 ? "队列为空" : $"等待队列：{Items.Count} 首"; }
    private void ShowError(Error error) => StatusMessage = $"操作未完成：{error.Message}";
    private void ShowUnexpectedError() => StatusMessage = "操作未完成：队列状态已变化，请刷新后重试。";
}
