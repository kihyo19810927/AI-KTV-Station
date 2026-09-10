namespace Station.Application.Configuration;

public sealed class StationOptions
{
    public const string SectionName = "Station";
    public ServerOptions Server { get; init; } = new();
    public StorageOptions Storage { get; init; } = new();
    public PlayerOptions Player { get; init; } = new();
    public ScanOptions Scanning { get; init; } = new();
}

public sealed class ScanOptions
{
    public bool BasicIndexOnly { get; set; }
    public bool ReadNfo { get; set; }
    public int ProbeConcurrency { get; set; } = 1;
}

public sealed class ServerOptions
{
    public string BindAddress { get; init; } = "0.0.0.0";
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
