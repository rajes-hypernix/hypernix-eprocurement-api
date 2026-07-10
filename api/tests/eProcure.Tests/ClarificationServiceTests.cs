using eProcure.Application.Communication;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Services;
using FluentAssertions;

namespace eProcure.Tests;

public sealed class ClarificationServiceTests
{
    private static async Task<(TestContext C, Vendor A, Vendor B, Rfq Rfq)> SeedAsync()
    {
        var c = TestContext.New();
        var a = new Vendor { Code = "V-A", Name = "Alpha", RegisteredName = "Alpha" };
        var b = new Vendor { Code = "V-B", Name = "Beta", RegisteredName = "Beta" };
        c.Db.Vendors.AddRange(a, b);
        var rfq = new Rfq
        {
            Code = "RFQ-2026-0001", Title = "Pumps", Status = RfqStatus.Open, CreatedUtc = c.Clock.UtcNow, UpdatedUtc = c.Clock.UtcNow,
        };
        rfq.WithInvites(c.Clock.UtcNow, a.Id, b.Id);
        c.Db.Rfqs.Add(rfq);
        await c.Db.SaveChangesAsync();
        return (c, a, b, rfq);
    }

    private static ClarificationService Svc(TestContext c) => new(c.Db, c.User, c.Clock);

    [Fact]
    public async Task Vendor_sees_only_their_own_threads()
    {
        var (c, a, b, _) = await SeedAsync();
        // Buyer messages both vendors on general threads.
        c.User.VendorId = null;
        await Svc(c).SendAsync(new SendClarificationRequest("general", a.Id, "hi A", false));
        await Svc(c).SendAsync(new SendClarificationRequest("general", b.Id, "hi B", false));

        // Vendor A only sees its own thread.
        c.User.VendorId = a.Id;
        var threads = await Svc(c).ListThreadsAsync();
        threads.Should().ContainSingle().Which.VendorId.Should().Be(a.Id);
    }

    [Fact]
    public async Task Vendor_cannot_read_another_vendors_thread()
    {
        var (c, a, b, _) = await SeedAsync();
        c.User.VendorId = null;
        await Svc(c).SendAsync(new SendClarificationRequest("general", b.Id, "hi B", false));

        c.User.VendorId = a.Id;
        var act = async () => await Svc(c).GetThreadAsync("general", b.Id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Published_buyer_answer_fans_out_to_all_invited_bidders()
    {
        var (c, a, b, rfq) = await SeedAsync();
        c.User.VendorId = null;  // buyer
        await Svc(c).SendAsync(new SendClarificationRequest(rfq.Code, a.Id, "closes 23 June", true));

        // Vendor B (also invited) received the published answer as an unread message.
        c.User.VendorId = b.Id;
        var threads = await Svc(c).ListThreadsAsync();
        var t = threads.Should().ContainSingle(x => x.Scope == rfq.Code).Subject;
        t.Unread.Should().Be(1);
        var detail = await Svc(c).GetThreadAsync(rfq.Code, b.Id);
        detail!.Messages.Should().ContainSingle().Which.Published.Should().BeTrue();
    }

    [Fact]
    public async Task Opening_a_thread_clears_unread_for_that_side()
    {
        var (c, a, _, _) = await SeedAsync();
        c.User.VendorId = null;
        await Svc(c).SendAsync(new SendClarificationRequest("general", a.Id, "ping", false));

        c.User.VendorId = a.Id;
        (await Svc(c).ListThreadsAsync()).Single().Unread.Should().Be(1);
        await Svc(c).GetThreadAsync("general", a.Id);              // open → marks read
        (await Svc(c).ListThreadsAsync()).Single().Unread.Should().Be(0);
    }
}
