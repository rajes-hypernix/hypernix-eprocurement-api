namespace eProcure.Application.Abstractions;

/// <summary>
/// Idempotent Development-only seeding hook, run after migrations. Slice 0 wires
/// the hook; it is populated with dummy demo data from Slice 1 on (SEED-DATA.md).
/// </summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
