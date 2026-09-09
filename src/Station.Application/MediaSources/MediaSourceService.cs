using Station.Application.Common;
using Station.Domain.Models;

namespace Station.Application.MediaSources;

public interface IMediaSourceService
{
    Task<Result<MediaSourceAdminDetails>> AddAsync(string name, string rootPath, CancellationToken cancellationToken = default);
    Task<Result<MediaSourceAdminDetails>> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaSourceSummary>> ListPublicAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MediaSourceAdminDetails>> ListAdminAsync(CancellationToken cancellationToken = default);
}

public sealed class MediaSourceService(IMediaSourceRepository repository, IMediaPathInspector pathInspector) : IMediaSourceService
{
    public async Task<Result<MediaSourceAdminDetails>> AddAsync(string name, string rootPath, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 200) return Failure("media_source.name_invalid", "Media source name must contain 1 to 200 characters.");
        var check = await pathInspector.InspectAsync(rootPath, cancellationToken);
        if (!check.Exists || !check.IsDirectory || !check.IsReadable)
            return Failure(check.ErrorCode ?? "media_source.path_unavailable", "Media source path is not an accessible directory.");
        if (await repository.FindByRootPathAsync(check.CanonicalPath, cancellationToken) is not null)
            return Failure("media_source.duplicate_path", "A media source already uses this directory.");
        var source = new MediaSource { Name = name, RootPath = check.CanonicalPath, IsEnabled = true, Availability = AvailabilityStatus.Available };
        await repository.AddAsync(source, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<MediaSourceAdminDetails>.Success(ToAdminDetails(source));
    }

    public async Task<Result<MediaSourceAdminDetails>> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var source = await repository.FindByIdAsync(id, cancellationToken);
        if (source is null) return Failure("media_source.not_found", "Media source was not found.");
        source.IsEnabled = enabled;
        await repository.SaveChangesAsync(cancellationToken);
        return Result<MediaSourceAdminDetails>.Success(ToAdminDetails(source));
    }

    public async Task<IReadOnlyList<MediaSourceSummary>> ListPublicAsync(CancellationToken cancellationToken = default) =>
        (await repository.ListAsync(cancellationToken)).Select(x => new MediaSourceSummary(x.Id, x.Name, x.IsEnabled, x.Availability)).ToArray();

    public async Task<IReadOnlyList<MediaSourceAdminDetails>> ListAdminAsync(CancellationToken cancellationToken = default) =>
        (await repository.ListAsync(cancellationToken)).Select(ToAdminDetails).ToArray();

    private static MediaSourceAdminDetails ToAdminDetails(MediaSource source) => new(source.Id, source.Name, source.RootPath, source.IsEnabled, source.Availability);
    private static Result<MediaSourceAdminDetails> Failure(string code, string message) => Result<MediaSourceAdminDetails>.Failure(new Error(code, message));
}
