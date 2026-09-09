namespace Station.Application.Health;

public interface IStationHealthService
{
    Task<StationHealthSnapshot> CheckAsync(CancellationToken cancellationToken = default);
}

public enum HealthLevel { Healthy, Warning, Unavailable }

public sealed record HealthComponent(string Name, HealthLevel Level, string Summary);

public sealed record StationHealthSnapshot(DateTimeOffset CheckedAt, IReadOnlyList<HealthComponent> Components)
{
    public HealthLevel Overall => Components.Any(x => x.Level == HealthLevel.Unavailable)
        ? HealthLevel.Unavailable
        : Components.Any(x => x.Level == HealthLevel.Warning) ? HealthLevel.Warning : HealthLevel.Healthy;
}
