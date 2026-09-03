using Station.Domain.Models;

namespace Station.Application.MediaSources;

public sealed record MediaSourceSummary(Guid Id, string Name, bool IsEnabled, AvailabilityStatus Availability);
public sealed record MediaSourceAdminDetails(Guid Id, string Name, string RootPath, bool IsEnabled, AvailabilityStatus Availability);
