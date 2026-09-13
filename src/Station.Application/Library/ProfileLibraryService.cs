using Station.Application.Common;

namespace Station.Application.Library;

public sealed record HouseholdProfile(Guid Id, string DisplayName, string? AvatarUrl, bool RequiresPin, DateTimeOffset LastUsedAt);
public sealed record ProfilePlaylistSummary(Guid Id, string Name, string Kind, bool IsFamilyShared, int SongCount);
public sealed record ProfileLibrarySession(HouseholdProfile Profile, string DeviceToken);

public interface IProfileLibraryRepository
{
    Task<IReadOnlyList<HouseholdProfile>> ListProfilesAsync(CancellationToken cancellationToken = default);
    Task<ProfileLibrarySession> CreateAsync(string displayName, string? avatarUrl, CancellationToken cancellationToken = default);
    Task<ProfileLibrarySession?> ResolveAsync(string deviceToken, CancellationToken cancellationToken = default);
    Task<ProfileLibrarySession?> ActivateAsync(Guid profileId, string? pin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfilePlaylistSummary>> ListPlaylistsAsync(Guid profileId, CancellationToken cancellationToken = default);
    Task<Result<ProfilePlaylistSummary>> CreatePlaylistAsync(Guid profileId, string name, bool isFamilyShared, CancellationToken cancellationToken = default);
    Task<Result<bool>> SetFavoriteAsync(Guid profileId, Guid songId, bool favorite, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FavoriteSong>> ListFavoritesAsync(Guid profileId, CancellationToken cancellationToken = default);
}

/// <summary>Application boundary for durable household preferences; room guests remain short-lived.</summary>
public sealed class ProfileLibraryService(IProfileLibraryRepository repository)
{
    public Task<IReadOnlyList<HouseholdProfile>> ListProfilesAsync(CancellationToken cancellationToken = default) => repository.ListProfilesAsync(cancellationToken);

    public Task<ProfileLibrarySession> CreateAsync(string displayName, string? avatarUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 80)
            throw new ArgumentException("Profile display name must contain 1 to 80 characters.", nameof(displayName));
        return repository.CreateAsync(displayName.Trim(), avatarUrl?.Trim(), cancellationToken);
    }

    public Task<ProfileLibrarySession?> ResolveAsync(string deviceToken, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(deviceToken) ? Task.FromResult<ProfileLibrarySession?>(null) : repository.ResolveAsync(deviceToken, cancellationToken);

    public Task<ProfileLibrarySession?> ActivateAsync(Guid profileId, string? pin, CancellationToken cancellationToken = default) =>
        profileId == Guid.Empty ? Task.FromResult<ProfileLibrarySession?>(null) : repository.ActivateAsync(profileId, pin, cancellationToken);

    public Task<IReadOnlyList<ProfilePlaylistSummary>> ListPlaylistsAsync(Guid profileId, CancellationToken cancellationToken = default) => repository.ListPlaylistsAsync(profileId, cancellationToken);
    public Task<Result<ProfilePlaylistSummary>> CreatePlaylistAsync(Guid profileId, string name, bool isFamilyShared, CancellationToken cancellationToken = default) => repository.CreatePlaylistAsync(profileId, name, isFamilyShared, cancellationToken);
    public Task<Result<bool>> SetFavoriteAsync(Guid profileId, Guid songId, bool favorite, CancellationToken cancellationToken = default) => repository.SetFavoriteAsync(profileId, songId, favorite, cancellationToken);
    public Task<IReadOnlyList<FavoriteSong>> ListFavoritesAsync(Guid profileId, CancellationToken cancellationToken = default) => repository.ListFavoritesAsync(profileId, cancellationToken);
}
