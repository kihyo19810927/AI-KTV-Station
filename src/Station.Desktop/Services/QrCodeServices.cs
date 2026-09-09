using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using QRCoder;

namespace Station.Desktop.Services;

public interface IQrCodeRenderer { byte[] Render(string content); }
public sealed class QrCodeRenderer : IQrCodeRenderer
{
    public byte[] Render(string content)
    {
        using var data = QRCodeGenerator.GenerateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(8, [23, 19, 39], [255, 255, 255]);
    }
}

public interface ILanAddressProvider { string GetPreferredAddress(); }
public sealed class LanAddressProvider : ILanAddressProvider
{
    public string GetPreferredAddress()
    {
        var address = NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(x => x.GetIPProperties().UnicastAddresses)
            .Select(x => x.Address)
            .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork && IsPrivate(x));
        return address?.ToString() ?? "127.0.0.1";
    }
    private static bool IsPrivate(IPAddress address) { var bytes = address.GetAddressBytes(); return bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31; }
}
