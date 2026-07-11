using eProcure.Domain.Sourcing;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// Slice H T4 pin: a DateOnly must cross the real API pipeline as a BARE ISO date string
/// (yyyy-MM-dd) — no time component, no T00:00:00, no timezone suffix. A T00:00:00Z sneaking into
/// the wire format would render as the previous day in any UTC+8 client that parses it as an instant,
/// silently breaking the "byte-identical on screen" guarantee. This assertion locks the contract.
/// </summary>
public sealed class TypedDateContractTests
{
    [Fact]
    public async Task DateOnly_serialises_over_the_api_as_a_bare_iso_date()
    {
        await using var factory = new TestWebAppFactory();
        var now = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var prId = Guid.Empty;
        await factory.SeedAsync(db =>
        {
            var pr = new PurchaseRequisition
            {
                Code = "PR-DATE-CONTRACT-1",
                RaisedOn = new DateOnly(2026, 8, 15),
                RequiredOn = new DateOnly(2026, 9, 20),
                CreatedUtc = now, UpdatedUtc = now,
            };
            db.PurchaseRequisitions.Add(pr);
            prId = pr.Id;
            return Task.CompletedTask;
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Demo-User", "u_faridah");   // buyer

        var resp = await client.GetAsync($"/api/requisitions/{prId}");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();

        json.Should().Contain("\"raisedDate\":\"2026-08-15\"", "a DateOnly must serialise as bare yyyy-MM-dd");
        json.Should().Contain("\"requiredDate\":\"2026-09-20\"");
        json.Should().NotContain("2026-08-15T", "no time component may leak onto a date");
        json.Should().NotContain("T00:00:00");
        json.Should().NotContain("2026-08-15Z");
    }
}
