using Station.Application.Configuration;

namespace Station.Core.Tests;

internal static class TestData
{
    public static StationOptions ValidOptions(int port = 5090) => new()
    {
        Server = new ServerOptions { BindAddress = "127.0.0.1", Port = port },
        Storage = new StorageOptions { DataDirectory = "test-data" },
        Player = new PlayerOptions { CommandTimeoutSeconds = 10 },
    };
}
