using System.Text.Json;
using Station.Application.Common;
using Station.Application.Configuration;

namespace Station.Infrastructure.Configuration;

public sealed class JsonStationSettingsStore(string filePath) : IStationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<Result<StationOptions>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) return Result<StationOptions>.Success(new StationOptions());
        try
        {
            await using var stream = File.OpenRead(filePath);
            var options = await JsonSerializer.DeserializeAsync<StationOptions>(stream, JsonOptions, cancellationToken) ?? new StationOptions();
            var validation = StationOptionsValidator.Validate(options);
            return validation.IsSuccess ? validation : Result<StationOptions>.Failure(validation.Error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Result<StationOptions>.Failure(new Error("configuration.read_failed", "Local settings could not be read."));
        }
    }

    public async Task<Result<bool>> SaveAsync(StationOptions options, CancellationToken cancellationToken = default)
    {
        var validation = StationOptionsValidator.Validate(options);
        if (validation.IsFailure) return Result<bool>.Failure(validation.Error);
        try
        {
            var directory = Path.GetDirectoryName(filePath)!; Directory.CreateDirectory(directory);
            var temporary = filePath + ".tmp";
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(stream, options, JsonOptions, cancellationToken);
            File.Move(temporary, filePath, true);
            return Result<bool>.Success(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<bool>.Failure(new Error("configuration.write_failed", "Local settings could not be saved."));
        }
    }
}
