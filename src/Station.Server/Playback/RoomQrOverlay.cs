using QRCoder;
using Station.Application.Configuration;
using Station.Application.Playback;
using Station.Application.Rooms;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Station.Server.Playback;

public sealed class RoomQrOverlay(IPlayerAdapter player, RoomLifecycleService rooms,
    StationOptions options, ILogger<RoomQrOverlay> logger) : IPlaybackStartedObserver
{
    public static PlayerOverlayRequest Create(string content)
    {
        using var data = QRCodeGenerator.GenerateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var matrix = data.ModuleMatrix;
        const int scale = 5;
        const int quietZone = 4;
        const int radius = 12;
        var size = (matrix.Count + quietZone * 2) * scale;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var cornerX = Math.Max(radius - x, x - (size - 1 - radius));
                var cornerY = Math.Max(radius - y, y - (size - 1 - radius));
                if (cornerX > 0 && cornerY > 0 && cornerX * cornerX + cornerY * cornerY > radius * radius)
                    continue;

                var moduleX = x / scale - quietZone;
                var moduleY = y / scale - quietZone;
                var dark = moduleX >= 0 && moduleX < matrix.Count && moduleY >= 0 && moduleY < matrix.Count && matrix[moduleY][moduleX];
                var border = x < scale || y < scale || x >= size - scale || y >= size - scale;
                var color = dark ? (byte)23 : border ? (byte)105 : (byte)255;
                var offset = (y * size + x) * 4;
                pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = color;
                pixels[offset + 3] = 255;
            }
        }
        return new PlayerOverlayRequest(7, 24, 24, size, size, size * 4, 280, 280, pixels, TimeSpan.FromSeconds(15));
    }

    public async Task OnStartedAsync(Guid playbackId, CancellationToken cancellationToken)
    {
        try
        {
            var room = await rooms.GetCurrentAsync(cancellationToken);
            if (room.IsFailure || room.Value is null) return;
            var address = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(u => u.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && IsPrivate(a.GetAddressBytes()));
            if (address is null) return;
            var content = $"http://{address}:{options.Server.Port}/join?code={Uri.EscapeDataString(room.Value.JoinCode)}";
            var result = await player.ShowOverlayAsync(Create(content), cancellationToken);
            if (result.IsFailure) logger.LogWarning("QR overlay failed for playback {PlaybackId}: {Code}", playbackId, result.Error.Code);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) { logger.LogWarning(error, "QR overlay failed for playback {PlaybackId}", playbackId); }
    }

    private static bool IsPrivate(byte[] bytes) => bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31;
}
