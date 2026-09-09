using System.Net;

namespace Station.Server.Security;

public static class LocalRequestPolicy
{
    public static bool IsLocal(IPAddress? remoteAddress) => remoteAddress is null || IPAddress.IsLoopback(remoteAddress);
}
