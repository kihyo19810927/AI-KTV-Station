using Station.Server.Hosting;

var app = StationServerHost.Build(args);
await StationServerHost.InitializeAsync(app.Services);
await app.RunAsync();

public partial class Program;
