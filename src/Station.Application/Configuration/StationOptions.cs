namespace Station.Application.Configuration;

public sealed class StationOptions
{
    public const string SectionName = "Station";
    public ServerOptions Server { get; init; } = new();
    public StorageOptions Storage { get; init; } = new();
    public PlayerOptions Player { get; init; } = new();
}

public sealed class ServerOptions
{
    public string BindAddress { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 5090;
}

public sealed class StorageOptions
{
    public string DataDirectory { get; init; } = "data";
}

public sealed class PlayerOptions
{
    public string ExecutablePath { get; init; } = string.Empty;
    public int CommandTimeoutSeconds { get; init; } = 10;
}
