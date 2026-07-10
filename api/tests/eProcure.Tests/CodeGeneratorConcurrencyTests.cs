using eProcure.Infrastructure.Persistence;
using eProcure.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// PRG-2: N concurrent NextAsync calls must mint N distinct, gap-free codes. The row-lock guarantee
/// is a real PostgreSQL behaviour, so this runs against the local Docker DB (each task on its own
/// context for genuine concurrency) and cleans up its throwaway sequence row afterwards.
/// </summary>
public sealed class CodeGeneratorConcurrencyTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    [Fact]
    public async Task Concurrent_NextAsync_calls_produce_N_distinct_gap_free_codes()
    {
        await using (var probe = NewCtx())
            if (!await probe.Database.CanConnectAsync())
                throw new InvalidOperationException("CodeGenerator concurrency test requires the local Docker Postgres.");

        var clock = new FakeClock(new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));
        var prefix = "CCGEN" + Guid.NewGuid().ToString("N")[..6];   // unique prefix isolates this run
        const int n = 25;

        try
        {
            var tasks = Enumerable.Range(0, n).Select(async _ =>
            {
                await using var db = NewCtx();               // a distinct context per task = true concurrency
                return await new CodeGenerator(db, clock).NextAsync(prefix);
            });
            var codes = await Task.WhenAll(tasks);

            codes.Should().OnlyHaveUniqueItems("a row lock must serialise the counter so no code repeats");
            codes.Should().HaveCount(n);
            // Gap-free: the N codes are exactly 0001..000N in some order.
            var suffixes = codes.Select(c => int.Parse(c.Split('-')[^1])).OrderBy(x => x).ToArray();
            suffixes.Should().Equal(Enumerable.Range(1, n).ToArray());
        }
        finally
        {
            await using var cleanup = NewCtx();
            await cleanup.Database.ExecuteSqlInterpolatedAsync(
                $@"DELETE FROM ""NumberSequences"" WHERE ""Prefix"" = {prefix.ToUpperInvariant()} AND ""Year"" = 2026");
        }
    }
}
