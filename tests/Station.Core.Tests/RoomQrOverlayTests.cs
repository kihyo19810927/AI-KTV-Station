using QRCoder;
using Station.Server.Playback;

namespace Station.Core.Tests;

public sealed class RoomQrOverlayTests
{
    [Fact]
    public void Overlay_has_one_quiet_zone_transparent_corners_and_fifteen_second_lifetime()
    {
        const string content = "http://192.168.1.20:5090/join?code=ABC123";
        var overlay = RoomQrOverlay.Create(content);
        using var data = QRCodeGenerator.GenerateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        Assert.Equal((data.ModuleMatrix.Count + 8) * 5, overlay.Width);
        Assert.Equal(TimeSpan.FromSeconds(15), overlay.Duration);
        Assert.Equal(0, overlay.Bgra[3]);
        Assert.Equal(255, overlay.Bgra[(overlay.Width * (overlay.Height / 2)) * 4 + 3]);
        // Every code module remains intact after adding the standard four-module quiet zone.
        for (var y = 0; y < data.ModuleMatrix.Count; y++)
        {
            for (var x = 0; x < data.ModuleMatrix.Count; x++)
            {
                var offset = (((y + 4) * 5 + 2) * overlay.Width + (x + 4) * 5 + 2) * 4;
                Assert.Equal(data.ModuleMatrix[y][x] ? 23 : 255, overlay.Bgra[offset]);
                Assert.Equal(255, overlay.Bgra[offset + 3]);
            }
        }
    }
}
