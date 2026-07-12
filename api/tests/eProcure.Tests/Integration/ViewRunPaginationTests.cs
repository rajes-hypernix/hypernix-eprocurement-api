using System.Net.Http.Json;
using eProcure.Application.Views;
using eProcure.Domain.Procurement;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// D7.5 task 2 pins: /run pages (default 50, cap 200) with the slice AFTER filter+sort —
/// page 2 is disjoint from page 1, Total is the full filtered count, and vendor scoping
/// holds on EVERY page (the vendor's total is its own row count, no page leaks a foreign
/// row). Also: page/size out of range clamp instead of erroring.
/// </summary>
public sealed class ViewRunPaginationFixture : IAsyncLifetime
{
    public TestWebAppFactory Factory { get; } = new();
    public Guid VendorAId { get; private set; }

    public async Task InitializeAsync()
    {
        var now = DateTime.UtcNow;
        await Factory.SeedAsync(db =>
        {
            var a = new Vendor { Code = "V-PGA", Name = "Pager Alpha", RegisteredName = "Pager Alpha Sdn Bhd" };
            var b = new Vendor { Code = "V-PGB", Name = "Pager Beta", RegisteredName = "Pager Beta Sdn Bhd" };
            db.Vendors.AddRange(a, b);
            db.VendorUsers.Add(new VendorUser("VU-PGA", a.Id, "Pager A", "pga@vendor.test"));
            VendorAId = a.Id;
            for (var i = 1; i <= 7; i++)
            {
                db.PurchaseOrders.Add(new PurchaseOrder
                {
                    Code = $"PO-2026-77{i:D2}", VendorId = i <= 4 ? a.Id : b.Id, CreatedUtc = now, UpdatedUtc = now,
                    Lines = { new PoLine { ItemCode = $"P{i}", Description = "x", Qty = 1, UnitPrice = 100 * i, Uom = "Unit" } },
                });
            }
            db.FieldRegistry.AddRange(FieldRegistrySeed.ToEntities());
            return Task.CompletedTask;
        });
    }

    public Task DisposeAsync() { Factory.Dispose(); return Task.CompletedTask; }

    public HttpClient ClientAs(string demoUser)
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Demo-User", demoUser);
        return client;
    }

    public async Task<Guid> CreateAllPosView()
    {
        var resp = await ClientAs("u_faridah").PostAsJsonAsync("/api/views", new SaveViewRequest(
            $"pager {Guid.NewGuid().ToString("N")[..6]}", "PurchaseOrder", [],
            [new SavedViewColumnDto("Code", null, "Asc")]));
        return (await resp.Content.ReadFromJsonAsync<SavedViewDto>())!.Id;
    }
}

public sealed class ViewRunPaginationTests(ViewRunPaginationFixture fx) : IClassFixture<ViewRunPaginationFixture>
{
    private static string Code(Dictionary<string, object?> row) => row["Code"]!.ToString()!;

    [Fact]
    public async Task Pages_are_disjoint_windows_and_total_is_the_full_filtered_count()
    {
        var viewId = await fx.CreateAllPosView();
        var buyer = fx.ClientAs("u_faridah");
        var p1 = (await buyer.GetFromJsonAsync<ViewRunResult>($"/api/views/{viewId}/run?page=1&size=3"))!;
        var p2 = (await buyer.GetFromJsonAsync<ViewRunResult>($"/api/views/{viewId}/run?page=2&size=3"))!;
        var p3 = (await buyer.GetFromJsonAsync<ViewRunResult>($"/api/views/{viewId}/run?page=3&size=3"))!;

        p1.Total.Should().Be(7);
        p1.Rows.Should().HaveCount(3);
        p2.Rows.Should().HaveCount(3);
        p3.Rows.Should().HaveCount(1, "7 rows / size 3 → 3+3+1");
        var codes1 = p1.Rows.Select(Code).ToHashSet();
        p2.Rows.Select(Code).Should().NotIntersectWith(codes1, "page 2 differs from page 1");
        p1.Rows.Concat(p2.Rows).Concat(p3.Rows).Select(Code).Distinct().Should().HaveCount(7, "the pages tile the whole set");

        var deflt = (await buyer.GetFromJsonAsync<ViewRunResult>($"/api/views/{viewId}/run"))!;
        deflt.Page.Should().Be(1);
        deflt.Size.Should().Be(50);
        deflt.Rows.Should().HaveCount(7, "under one page → everything, exactly as before D7.5");

        var clamped = (await buyer.GetFromJsonAsync<ViewRunResult>($"/api/views/{viewId}/run?page=0&size=9999"))!;
        clamped.Page.Should().Be(1);
        clamped.Size.Should().Be(200, "cap 200, floor page 1 — clamp, never error");
    }

    [Fact]
    public async Task Vendor_scoping_holds_on_every_page()
    {
        var viewId = await fx.CreateAllPosView();
        // Share it so the vendor can run the SAME view the buyer built.
        await fx.ClientAs("u_faridah").PostAsJsonAsync($"/api/views/{viewId}/share", new ShareViewRequest(true));

        var vendor = fx.ClientAs("VU-PGA");
        var p1 = (await vendor.GetFromJsonAsync<ViewRunResult>($"/api/views/{viewId}/run?page=1&size=3"))!;
        var p2 = (await vendor.GetFromJsonAsync<ViewRunResult>($"/api/views/{viewId}/run?page=2&size=3"))!;
        p1.Total.Should().Be(4, "vendor A owns 4 of the 7 — Total is the SCOPED count");
        var all = p1.Rows.Concat(p2.Rows).Select(Code).ToList();
        all.Should().HaveCount(4);
        all.Should().OnlyContain(c => new[] { "PO-2026-7701", "PO-2026-7702", "PO-2026-7703", "PO-2026-7704" }.Contains(c),
            "no page leaks another vendor's row — the slice sits above the scoped source");
    }
}
