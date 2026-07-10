using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using eProcure.Domain.Files;
using eProcure.Domain.Identity;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>Seeds two vendors + their data once, then asserts resource scoping over the live host.</summary>
public sealed class VendorScopingFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new();   // Testing env, demo enabled
    public Guid VendorAId { get; private set; }
    public Guid VendorBId { get; private set; }
    public Guid FileAId { get; private set; }

    public async Task InitializeAsync()
    {
        var now = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        await Factory.SeedAsync(db =>
        {
            var a = new Vendor { Code = "V-A", Name = "Alpha", RegisteredName = "Alpha Sdn Bhd" };
            var b = new Vendor
            {
                Code = "V-B", Name = "Beta", RegisteredName = "Beta Sdn Bhd",
                BankAccounts = { new VendorBankAccount { Bank = "Maybank", AccountNo = "1234567890", Swift = "MBBEMYKL", Currency = "MYR", IsPrimary = true } },
            };
            db.Vendors.AddRange(a, b);
            db.VendorUsers.AddRange(
                new VendorUser("VU-A", a.Id, "Alpha User", "a@vendor.test"),
                new VendorUser("VU-B", b.Id, "Beta User", "b@vendor.test"));

            // A file A owns — now a typed ownership fact (T5) rather than inferred from the bid answer.
            var file = new StoredFile { Name = "bidA.pdf", ContentType = "application/pdf", Content = [1, 2, 3], Size = 3, CreatedUtc = now, OwnerKind = FileOwnerKind.Bid, OwnerVendorId = a.Id };
            db.StoredFiles.Add(file);

            var rfq1 = new Rfq { Code = "RFQ-2026-9001", Title = "Invited-to-A", ClosesUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            rfq1.Invitations.Add(RfqInvitation.Seed(rfq1.Id, a.Id, RfqInvitationStatus.Invited, now));
            var rfq2 = new Rfq { Code = "RFQ-2026-9002", Title = "Invited-to-B-only", ClosesUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedUtc = now, UpdatedUtc = now }.SeededAs(RfqStatus.Open);
            rfq2.Invitations.Add(RfqInvitation.Seed(rfq2.Id, b.Id, RfqInvitationStatus.Invited, now));
            db.Rfqs.AddRange(rfq1, rfq2);

            db.Bids.Add(new Bid
            {
                Code = "BID-A", RfqId = rfq1.Id, VendorId = a.Id, Submitted = true, CreatedUtc = now, UpdatedUtc = now,
                Answers = { new BidAnswer { QuestionOrder = 0, Value = $"{file.Id}::bidA.pdf" } },
            });

            VendorAId = a.Id;
            VendorBId = b.Id;
            FileAId = file.Id;
            return Task.CompletedTask;
        });
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }

    public HttpClient ClientAs(string demoUser)
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Demo-User", demoUser);
        return client;
    }
}

public sealed class VendorScopingTests(VendorScopingFixture fx) : IClassFixture<VendorScopingFixture>
{
    [Fact]
    public async Task Vendor_can_download_own_file_but_not_another_vendors()
    {
        (await fx.ClientAs("VU-A").GetAsync($"/api/files/{fx.FileAId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "vendor A owns the file");

        (await fx.ClientAs("VU-B").GetAsync($"/api/files/{fx.FileAId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "vendor B does not own the file — deny on uncertainty");
    }

    [Fact]
    public async Task Buyer_can_download_any_file()
    {
        (await fx.ClientAs("u_faridah").GetAsync($"/api/files/{fx.FileAId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "internal reviewers may read any file");
    }

    [Fact]
    public async Task Rfq_list_shows_a_vendor_only_their_invited_rfqs()
    {
        var codes = (await fx.ClientAs("VU-A").GetFromJsonAsync<List<JsonElement>>("/api/rfqs"))!
            .Select(e => e.GetProperty("code").GetString()).ToList();

        codes.Should().Contain("RFQ-2026-9001");        // A is invited
        codes.Should().NotContain("RFQ-2026-9002");     // invited to B only — must not leak
    }

    [Fact]
    public async Task Bank_details_are_masked_for_a_vendor_principal_but_full_for_a_buyer()
    {
        var asVendor = await fx.ClientAs("VU-A").GetFromJsonAsync<JsonElement>($"/api/vendors/{fx.VendorBId}");
        var maskedAcct = asVendor.GetProperty("bankAccounts")[0].GetProperty("accountNo").GetString();
        maskedAcct.Should().NotBe("1234567890");
        maskedAcct.Should().EndWith("7890").And.Contain("•");   // last-4 convention

        var asBuyer = await fx.ClientAs("u_faridah").GetFromJsonAsync<JsonElement>($"/api/vendors/{fx.VendorBId}");
        asBuyer.GetProperty("bankAccounts")[0].GetProperty("accountNo").GetString()
            .Should().Be("1234567890", "Buyer sees full bank details on the detail endpoint");
    }
}
