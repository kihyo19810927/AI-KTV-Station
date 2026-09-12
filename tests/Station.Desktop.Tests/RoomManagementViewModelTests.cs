using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Station.Application.Configuration;
using Station.Application.Rooms;
using Station.Desktop.Services;
using Station.Desktop.ViewModels;
using Station.Infrastructure.Persistence;
using Station.Infrastructure.Rooms;

namespace Station.Desktop.Tests;

public sealed class RoomManagementViewModelTests
{
    [Fact]
    public async Task Creating_room_builds_token_free_lan_qr_and_host_context()
    {
        var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        await using var database = new StationDbContext(new DbContextOptionsBuilder<StationDbContext>().UseSqlite(connection, true).Options); await database.Database.EnsureCreatedAsync();
        var context = new HostRoomContext(); var qr = new RecordingQr();
        var viewModel = new RoomManagementViewModel(
            new RoomLifecycleService(new EfRoomRepository(database), new FixedCode()),
            new RoomAuthenticationService(new EfRoomIdentityRepository(database), new Sha256RoomTokenProtector(), TimeProvider.System),
            context, qr, new FixedAddress(), new StationOptions { Server = new ServerOptions { Port = 5090 } });

        await viewModel.CreateRoomAsync();

        Assert.Equal("ABC234", viewModel.JoinCode); Assert.Equal("http://192.168.1.20:5090/join?code=ABC234", viewModel.JoinUrl);
        Assert.Equal(viewModel.JoinUrl, qr.Content); Assert.NotNull(context.Identity); Assert.Equal(RoomRole.Host, context.Identity.Role);
        Assert.DoesNotContain("token", viewModel.JoinUrl, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.CreateCommand.CanExecute(null));
        Assert.True(viewModel.CloseCommand.CanExecute(null));
    }

    [Fact]
    public void Qr_renderer_produces_png_bytes()
    {
        var bytes = new QrCodeRenderer().Render("http://192.168.1.20:5090/?room=ABC234");
        Assert.True(bytes.Length > 100); Assert.Equal(new byte[] { 137, 80, 78, 71 }, bytes[..4]);
    }

    private sealed class FixedCode : IRoomJoinCodeGenerator { public string Create() => "ABC234"; }
    private sealed class FixedAddress : ILanAddressProvider { public string GetPreferredAddress() => "192.168.1.20"; }
    private sealed class RecordingQr : IQrCodeRenderer { public string? Content { get; private set; } public byte[] Render(string content) { Content = content; return [1, 2, 3]; } }
}
