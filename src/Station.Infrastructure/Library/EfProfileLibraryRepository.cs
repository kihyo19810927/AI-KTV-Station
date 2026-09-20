using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Station.Application.Common;
using Station.Application.Library;
using Station.Domain.Models;
using Station.Infrastructure.Persistence;

namespace Station.Infrastructure.Library;

public sealed class EfProfileLibraryRepository(StationDbContext database, TimeProvider clock) : IProfileLibraryRepository
{
    private static readonly TimeSpan DeviceLifetime = TimeSpan.FromDays(90);
    private const int PinIterations = 120_000;
    public async Task<IReadOnlyList<HouseholdProfile>> ListProfilesAsync(CancellationToken cancellationToken = default) =>
        (await database.UserProfiles.AsNoTracking().Where(x => !x.IsArchived)
            .Select(x => new HouseholdProfile(x.Id, x.DisplayName, x.AvatarUrl, x.PinHash != null, x.LastUsedAt)).ToArrayAsync(cancellationToken))
        .OrderByDescending(x => x.LastUsedAt).ThenBy(x => x.DisplayName, StringComparer.Ordinal).ToArray();

    public async Task<ProfileLibrarySession> CreateAsync(string displayName, string? avatarUrl, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var profile = new UserProfile { DisplayName = displayName, AvatarUrl = avatarUrl, CreatedAt = now, LastUsedAt = now };
        database.UserProfiles.Add(profile);
        await EnsureSystemPlaylistsAsync(profile, now, cancellationToken);
        return await IssueDeviceAsync(profile, now, cancellationToken);
    }

    public async Task<ProfileLibrarySession?> ResolveAsync(string deviceToken, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var device = await database.ProfileDevices.Include(x => x.UserProfile).SingleOrDefaultAsync(x => x.TokenHash == Hash(deviceToken) && x.RevokedAt == null && !x.UserProfile.IsArchived, cancellationToken);
        if (device is null || device.ExpiresAt <= now) return null;
        device.LastUsedAt = device.UserProfile.LastUsedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        return new(Profile(device.UserProfile), deviceToken);
    }

    public async Task<ProfileLibrarySession?> ActivateAsync(Guid profileId, string? pin, CancellationToken cancellationToken = default)
    {
        var profile = await database.UserProfiles.Include(x => x.Playlists).SingleOrDefaultAsync(x => x.Id == profileId && !x.IsArchived, cancellationToken);
        if (profile is null || !VerifyPin(profile.PinHash, pin)) return null;
        var now = clock.GetUtcNow();
        await EnsureSystemPlaylistsAsync(profile, now, cancellationToken);
        return await IssueDeviceAsync(profile, now, cancellationToken);
    }

    public async Task<Result<bool>> SetPinAsync(Guid profileId, string? deviceToken, string? currentPin, string? newPin, CancellationToken cancellationToken = default)
    {
        if (profileId == Guid.Empty || string.IsNullOrWhiteSpace(deviceToken))
            return Result<bool>.Failure(new Error("profile.device_required", "An active profile device is required."));
        var device = await database.ProfileDevices.Include(x => x.UserProfile)
            .SingleOrDefaultAsync(x => x.UserProfileId == profileId && x.TokenHash == Hash(deviceToken) && x.RevokedAt == null && !x.UserProfile.IsArchived, cancellationToken);
        if (device is null || device.ExpiresAt <= clock.GetUtcNow()) return Result<bool>.Failure(new Error("profile.device_invalid", "The profile device is invalid or expired."));
        if (!VerifyPin(device.UserProfile.PinHash, currentPin))
            return Result<bool>.Failure(new Error("profile.pin_invalid", "The current PIN is incorrect."));
        if (newPin is not null && !IsValidPin(newPin))
            return Result<bool>.Failure(new Error("profile.pin_invalid", "PIN must contain 4 to 12 digits."));
        device.UserProfile.PinHash = newPin is null ? null : HashPin(newPin);
        device.UserProfile.LastUsedAt = device.LastUsedAt = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> RevokeDeviceAsync(Guid profileId, string? deviceToken, bool allDevices, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var devices = await database.ProfileDevices.Where(x => x.UserProfileId == profileId && x.RevokedAt == null).ToListAsync(cancellationToken);
        if (devices.Count == 0) return Result<bool>.Failure(new Error("profile.not_found", "No active profile devices were found."));
        if (!allDevices)
        {
            var current = devices.SingleOrDefault(x => x.TokenHash == Hash(deviceToken ?? string.Empty));
            if (current is null) return Result<bool>.Failure(new Error("profile.device_invalid", "The profile device is invalid."));
            current.RevokedAt = now;
        }
        else foreach (var device in devices) device.RevokedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<IReadOnlyList<ProfilePlaylistSummary>> ListPlaylistsAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var owned = database.ProfilePlaylists.AsNoTracking().Where(x => x.UserProfileId == profileId);
        var shared = database.ProfilePlaylists.AsNoTracking().Where(x => x.IsFamilyShared && x.UserProfileId != profileId);
        return await owned.Concat(shared).OrderBy(x => x.Kind).ThenBy(x => x.Name)
            .Select(x => new ProfilePlaylistSummary(x.Id, x.Name, x.Kind.ToString(), x.IsFamilyShared, x.Items.Count)).ToArrayAsync(cancellationToken);
    }

    public async Task<Result<ProfilePlaylistSummary>> CreatePlaylistAsync(Guid profileId, string name, bool isFamilyShared, CancellationToken cancellationToken = default)
    {
        var clean = name?.Trim();
        if (profileId == Guid.Empty || string.IsNullOrWhiteSpace(clean) || clean.Length > 120)
            return Result<ProfilePlaylistSummary>.Failure(new Error("profile.playlist_invalid", "Playlist name must contain 1 to 120 characters."));
        if (!await database.UserProfiles.AnyAsync(x => x.Id == profileId && !x.IsArchived, cancellationToken))
            return Result<ProfilePlaylistSummary>.Failure(new Error("profile.not_found", "Profile was not found."));
        if (await database.ProfilePlaylists.AnyAsync(x => x.UserProfileId == profileId && x.Name == clean, cancellationToken))
            return Result<ProfilePlaylistSummary>.Failure(new Error("profile.playlist_exists", "A playlist with this name already exists."));
        var now = clock.GetUtcNow();
        var playlist = new ProfilePlaylist { UserProfileId = profileId, Name = clean, Kind = ProfilePlaylistKind.Custom, IsFamilyShared = isFamilyShared, CreatedAt = now, UpdatedAt = now };
        database.ProfilePlaylists.Add(playlist);
        await database.SaveChangesAsync(cancellationToken);
        return Result<ProfilePlaylistSummary>.Success(new(playlist.Id, playlist.Name, playlist.Kind.ToString(), playlist.IsFamilyShared, 0));
    }

    public async Task<Result<bool>> SetFavoriteAsync(Guid profileId, Guid songId, bool favorite, CancellationToken cancellationToken = default)
    {
        var playlist = await database.ProfilePlaylists.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserProfileId == profileId && x.Kind == ProfilePlaylistKind.Favorites, cancellationToken);
        if (playlist is null || !await database.Songs.AnyAsync(x => x.Id == songId, cancellationToken))
            return Result<bool>.Failure(new Error("profile.song_not_found", "Song or profile was not found."));
        var item = playlist.Items.SingleOrDefault(x => x.SongId == songId);
        if (favorite && item is null) playlist.Items.Add(new ProfilePlaylistItem { SongId = songId, Position = playlist.Items.Count + 1, AddedAt = clock.GetUtcNow() });
        if (!favorite && item is not null) database.ProfilePlaylistItems.Remove(item);
        playlist.UpdatedAt = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(favorite);
    }

    public async Task<IReadOnlyList<FavoriteSong>> ListFavoritesAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var rows = await database.ProfilePlaylistItems.AsNoTracking()
            .Where(x => x.ProfilePlaylist.UserProfileId == profileId && x.ProfilePlaylist.Kind == ProfilePlaylistKind.Favorites)
            .Include(x => x.Song).ThenInclude(x => x.Artists).ThenInclude(x => x.Artist)
            .ToArrayAsync(cancellationToken);
        return rows.OrderByDescending(x => x.AddedAt).Select(x => new FavoriteSong(x.SongId, x.Song.Title,
            string.Join(" / ", x.Song.Artists.OrderBy(a => a.Order).Select(a => a.Artist.Name)), x.AddedAt)).ToArray();
    }

    private async Task<ProfileLibrarySession> IssueDeviceAsync(UserProfile profile, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        profile.LastUsedAt = now;
        database.ProfileDevices.Add(new ProfileDevice { UserProfileId = profile.Id, TokenHash = Hash(token), CreatedAt = now, ExpiresAt = now.Add(DeviceLifetime), LastUsedAt = now });
        await database.SaveChangesAsync(cancellationToken);
        return new(Profile(profile), token);
    }

    private async Task EnsureSystemPlaylistsAsync(UserProfile profile, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = profile.Playlists.Select(x => x.Kind).ToHashSet();
        if (!existing.Contains(ProfilePlaylistKind.Favorites)) database.ProfilePlaylists.Add(new ProfilePlaylist { UserProfileId = profile.Id, Name = "我的收藏", Kind = ProfilePlaylistKind.Favorites, CreatedAt = now, UpdatedAt = now });
        if (!existing.Contains(ProfilePlaylistKind.Frequent)) database.ProfilePlaylists.Add(new ProfilePlaylist { UserProfileId = profile.Id, Name = "我常唱的", Kind = ProfilePlaylistKind.Frequent, CreatedAt = now, UpdatedAt = now });
        await database.SaveChangesAsync(cancellationToken);
    }

    private static HouseholdProfile Profile(UserProfile profile) => new(profile.Id, profile.DisplayName, profile.AvatarUrl, profile.PinHash != null, profile.LastUsedAt);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool VerifyPin(string? pinHash, string? pin)
    {
        if (pinHash is null) return true;
        if (string.IsNullOrEmpty(pin)) return false;
        if (pinHash.StartsWith("PBKDF2$", StringComparison.Ordinal))
        {
            var parts = pinHash.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations)) return false;
            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException) { return false; }
        }
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pinHash), Encoding.UTF8.GetBytes(Hash(pin)));
    }

    private static string HashPin(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, PinIterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2${PinIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool IsValidPin(string pin) => pin.Length is >= 4 and <= 12 && pin.All(char.IsAsciiDigit);
}
