using eProcure.Application.Dashboards;
using eProcure.Application.Views;
using eProcure.Domain.Dashboards;
using eProcure.Domain.Identity;
using eProcure.Domain.Views;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// D4 drift-proofs over the seed: every seeded portlet's ConfigJson validates against its
/// type's schema; every metric id a seed references exists in MetricIds; every internal role
/// (plus Vendor) has exactly one role-default dashboard; the Recent-POs view's columns are
/// real PurchaseOrder registry keys. A seed that drifts from the schemas fails here, not in
/// a browser.
/// </summary>
public sealed class DashboardSeedDriftTests
{
    private static readonly HashSet<string> KnownMetricIds = typeof(MetricIds)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Select(f => (string)f.GetValue(null)!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Every_seeded_portlet_config_validates_against_its_type_schema()
    {
        foreach (var dash in DashboardSeed.Rows)
            foreach (var p in dash.Portlets)
            {
                var act = () => PortletConfigs.Validate(p.Type, p.ConfigJson, p.SavedViewId);
                act.Should().NotThrow($"{dash.Code} / '{p.Title}' must carry a valid {p.Type} config");
            }
    }

    [Fact]
    public void Every_referenced_metric_id_exists()
    {
        var referenced = new List<string>();
        foreach (var p in DashboardSeed.Rows.SelectMany(d => d.Portlets))
        {
            if (p.Type == PortletType.KpiMeter)
            {
                var c = System.Text.Json.JsonSerializer.Deserialize<PortletConfigs.KpiMeterConfig>(p.ConfigJson, PortletConfigs.Json)!;
                if (c.MetricId is not null) referenced.Add(c.MetricId);
            }
            if (p.Type == PortletType.KpiScorecard)
                referenced.AddRange(System.Text.Json.JsonSerializer.Deserialize<PortletConfigs.KpiScorecardConfig>(p.ConfigJson, PortletConfigs.Json)!.Items.Select(i => i.MetricId));
            if (p.Type == PortletType.Chart)
                referenced.AddRange(System.Text.Json.JsonSerializer.Deserialize<PortletConfigs.ChartConfig>(p.ConfigJson, PortletConfigs.Json)!.SeriesIds);
        }
        referenced.Should().OnlyContain(id => KnownMetricIds.Contains(id));
    }

    [Fact]
    public void Exactly_one_role_default_per_role_including_commevaluator_and_vendor()
    {
        var roles = DashboardSeed.Rows.Select(r => r.OwnerRole).ToList();
        roles.Should().OnlyHaveUniqueItems();
        roles.Should().BeEquivalentTo(Roles.All, "every role incl. CommEvaluator and Vendor gets a default (ruled)");
    }

    [Fact]
    public void Recent_pos_view_columns_are_real_purchase_order_registry_keys()
    {
        var poKeys = FieldRegistrySeed.Rows.Where(r => r.RecordType == RecordType.PurchaseOrder)
            .Select(r => r.FieldKey).ToHashSet();
        new[] { "Code", "VendorName", "RfqCode", "Total", "Status" }
            .Should().OnlyContain(k => poKeys.Contains(k));
    }

    [Fact]
    public void MyInvitations_is_seeded_only_into_the_vendor_default()
    {
        foreach (var dash in DashboardSeed.Rows)
        {
            var has = dash.Portlets.Any(p => p.Type == PortletType.MyInvitations);
            (dash.OwnerRole == Roles.Vendor).Should().Be(has,
                "OD-D4-2: MyInvitations is the vendor's work surface, seeded only there");
        }
    }
}
