using Station.Application.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddOptions<StationOptions>()
    .BindConfiguration(StationOptions.SectionName)
    .Validate(options => StationOptionsValidator.Validate(options).IsSuccess, "Station configuration is invalid.")
    .ValidateOnStart();
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();

public partial class Program;
