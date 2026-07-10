namespace eProcure.Application.Dashboard;

/// <summary>A single dashboard metric tile. Tone drives the card colour; Link is a hash route.</summary>
public sealed record DashboardCard(string Label, string Value, string Sub, string Tone, string? Link);

public sealed record DashboardDto(string Title, string Subtitle, IReadOnlyList<DashboardCard> Cards);

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}
