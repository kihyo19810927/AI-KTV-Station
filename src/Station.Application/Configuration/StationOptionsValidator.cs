using Station.Application.Common;
using System.Net;

namespace Station.Application.Configuration;

public static class StationOptionsValidator
{
    public static Result<StationOptions> Validate(StationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Server.BindAddress) || !IPAddress.TryParse(options.Server.BindAddress, out _))
            return Invalid("server bind address must be an IP address");
        if (options.Server.Port is < 1 or > 65535) return Invalid("server port must be between 1 and 65535");
        if (string.IsNullOrWhiteSpace(options.Storage.DataDirectory)) return Invalid("data directory is required");
        if (options.Player.CommandTimeoutSeconds is < 1 or > 120) return Invalid("player timeout must be between 1 and 120 seconds");
        return Result<StationOptions>.Success(options);
    }

    private static Result<StationOptions> Invalid(string message) =>
        Result<StationOptions>.Failure(new Error("configuration.invalid", message));
}
