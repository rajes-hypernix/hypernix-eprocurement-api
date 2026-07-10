using eProcure.Api.Middleware;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// T2 (RFQ-LIFECYCLE E11): the xmin row-version token turns a stale write into a
/// DbUpdateConcurrencyException, which the middleware surfaces as HTTP 409. The token is a real
/// PostgreSQL behaviour (the in-memory provider can't reproduce it), so the round-trip runs against
/// the local Docker DB on a throwaway RFQ that is deleted afterwards; the mapping is a pure unit test.
/// </summary>
public sealed class ConcurrencyTests
{
    private static string Conn =>
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev";

    private static AppDbContext NewCtx() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    private static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Concurrent_update_of_the_same_Rfq_raises_a_concurrency_conflict()
    {
        await using var setup = NewCtx();
        if (!await setup.Database.CanConnectAsync())
            throw new InvalidOperationException("Concurrency test requires the local Docker Postgres (docker compose up -d).");

        var rfq = new Rfq { Code = "RFQ-CCTEST-" + Guid.NewGuid().ToString("N")[..8], Title = "cc-test", CreatedUtc = Now, UpdatedUtc = Now, ClosesUtc = Now.AddDays(30) };
        setup.Rfqs.Add(rfq);
        await setup.SaveChangesAsync();
        var id = rfq.Id;

        try
        {
            await using var ctxA = NewCtx();
            await using var ctxB = NewCtx();
            var a = await ctxA.Rfqs.FirstAsync(r => r.Id == id);
            var b = await ctxB.Rfqs.FirstAsync(r => r.Id == id);   // both loaded the same xmin

            a.Title = "changed-by-A";
            await ctxA.SaveChangesAsync();                         // commits, PostgreSQL bumps xmin

            b.Title = "changed-by-B";
            var act = async () => await ctxB.SaveChangesAsync();   // stale xmin -> 0 rows match
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }
        finally
        {
            await using var cleanup = NewCtx();
            var stray = await cleanup.Rfqs.FirstOrDefaultAsync(r => r.Id == id);
            if (stray is not null) { cleanup.Rfqs.Remove(stray); await cleanup.SaveChangesAsync(); }
        }
    }

    [Fact]
    public async Task ExceptionMiddleware_maps_a_concurrency_conflict_to_409()
    {
        var ctx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var mw = new ExceptionMiddleware(
            _ => throw new DbUpdateConcurrencyException("stale row"),
            NullLogger<ExceptionMiddleware>.Instance);

        await mw.Invoke(ctx);

        ctx.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().Contain("Concurrent modification").And.Contain("Reload");
    }
}
