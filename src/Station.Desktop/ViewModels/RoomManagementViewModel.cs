using System.Collections.ObjectModel;
using System.Windows.Input;
using Station.Application.Configuration;
using Station.Application.Rooms;
using Station.Desktop.Services;

namespace Station.Desktop.ViewModels;

public sealed class RoomManagementViewModel : ObservableObject
{
    private readonly RoomLifecycleService rooms;
    private readonly RoomAuthenticationService authentication;
    private readonly HostRoomContext context;
    private readonly IQrCodeRenderer qrCodes;
    private readonly ILanAddressProvider addresses;
    private readonly int port;
    private RoomAdminDetails? room;
    private string statusMessage = "尚未开启房间";
    private string joinUrl = string.Empty;
    private byte[]? qrCodePng;
    private int queueLimit = 100;
    private readonly AsyncRelayCommand createCommand;
    private readonly AsyncRelayCommand closeCommand;

    public RoomManagementViewModel(RoomLifecycleService rooms, RoomAuthenticationService authentication, HostRoomContext context, IQrCodeRenderer qrCodes, ILanAddressProvider addresses, StationOptions options)
    {
        this.rooms = rooms; this.authentication = authentication; this.context = context; this.qrCodes = qrCodes; this.addresses = addresses; port = options.Server.Port;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync); createCommand = new AsyncRelayCommand(CreateRoomAsync, () => Room is null); closeCommand = new AsyncRelayCommand(CloseAsync, () => Room is not null); CreateCommand = createCommand; CloseCommand = closeCommand; SaveRulesCommand = new AsyncRelayCommand(SaveRulesAsync);
        RevokeGuestCommand = new AsyncRelayCommand<RoomGuestAdminDetails>(RevokeAsync, guest => guest.Role == RoomRole.Guest && !guest.IsRevoked);
    }

    public ObservableCollection<RoomGuestAdminDetails> Guests { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand SaveRulesCommand { get; }
    public ICommand RevokeGuestCommand { get; }
    public RoomAdminDetails? Room { get => room; private set => SetProperty(ref room, value); }
    public string JoinCode => Room?.JoinCode ?? "------";
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public string JoinUrl { get => joinUrl; private set => SetProperty(ref joinUrl, value); }
    public byte[]? QrCodePng { get => qrCodePng; private set => SetProperty(ref qrCodePng, value); }
    public int QueueLimit { get => queueLimit; set => SetProperty(ref queueLimit, value); }

    public async Task RefreshAsync()
    {
        var current = await rooms.GetCurrentAsync();
        if (current.IsFailure) { StatusMessage = current.Error.Message; return; }
        if (current.Value is null) { ClearRoom(); return; }
        await AdoptAsync(current.Value);
    }

    public async Task CreateRoomAsync()
    {
        var created = await rooms.CreateAsync(QueueLimit);
        if (created.IsFailure) { StatusMessage = created.Error.Message; return; }
        await AdoptAsync(created.Value);
    }

    private async Task AdoptAsync(RoomAdminDetails value)
    {
        Room = value; QueueLimit = value.MaxQueuedSongsPerGuest; createCommand.NotifyCanExecuteChanged(); closeCommand.NotifyCanExecuteChanged();
        if (context.Identity is null || context.Identity.RoomId != value.Id || context.Identity.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            var host = await authentication.IssueHostAsync(value.Id, "主持人");
            if (host.IsFailure) { StatusMessage = host.Error.Message; return; }
            context.Identity = new(host.Value.RoomId, host.Value.GuestId, host.Value.Nickname, host.Value.Role, host.Value.ExpiresAt);
        }
        JoinUrl = $"http://{addresses.GetPreferredAddress()}:{port}/join?code={Uri.EscapeDataString(value.JoinCode)}";
        QrCodePng = qrCodes.Render(JoinUrl); RaisePropertyChanged(nameof(JoinCode));
        await RefreshGuestsAsync(); StatusMessage = "房间已开启，可扫码点歌";
    }

    private async Task CloseAsync()
    {
        if (Room is null) return;
        var result = await rooms.CloseAsync(Room.Id);
        if (result.IsFailure) { StatusMessage = result.Error.Message; return; }
        ClearRoom(); StatusMessage = "房间已关闭";
    }

    private async Task SaveRulesAsync()
    {
        if (Room is null) { StatusMessage = "请先开启房间"; return; }
        var result = await rooms.SetQueueLimitAsync(Room.Id, QueueLimit);
        if (result.IsFailure) { StatusMessage = result.Error.Message; return; }
        Room = result.Value; StatusMessage = "点歌规则已保存";
    }

    private async Task RevokeAsync(RoomGuestAdminDetails guest)
    {
        var result = await authentication.RevokeAsync(guest.Id);
        if (result.IsFailure) { StatusMessage = result.Error.Message; return; }
        await RefreshGuestsAsync(); StatusMessage = "访客已移除";
    }

    private async Task RefreshGuestsAsync()
    {
        if (Room is null) return;
        var result = await authentication.ListGuestsAsync(Room.Id);
        if (result.IsFailure) { StatusMessage = result.Error.Message; return; }
        Guests.Clear(); foreach (var guest in result.Value.Where(x => x.Role == RoomRole.Guest)) Guests.Add(guest);
    }

    private void ClearRoom() { Room = null; context.Identity = null; Guests.Clear(); JoinUrl = string.Empty; QrCodePng = null; RaisePropertyChanged(nameof(JoinCode)); createCommand.NotifyCanExecuteChanged(); closeCommand.NotifyCanExecuteChanged(); StatusMessage = "尚未开启房间"; }
}
