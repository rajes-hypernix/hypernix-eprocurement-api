using System.Reflection;
using System.Text.RegularExpressions;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// T4 — the golden constraint made executable (ARC-1). These fail the build if the "status changes
/// only through guarded domain methods" rule is reopened: a public Status setter on an aggregate
/// root, a raw `.Status =` write in a service, or a service reaching for the test/seed-only
/// SeededAs backdoor. This test — not the 17 fixes — is what stops the drift.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Type[] AggregateRoots =
    [
        typeof(Rfq), typeof(PurchaseOrder), typeof(Invoice), typeof(Asn),
        typeof(Award), typeof(Vendor), typeof(PurchaseRequisition),
    ];

    [Fact]
    public void No_aggregate_root_exposes_a_public_Status_setter()
    {
        var offenders = AggregateRoots
            .Select(t => t.GetProperty("Status"))
            .Where(p => p is { SetMethod.IsPublic: true })
            .Select(p => $"{p!.DeclaringType!.Name}.Status")
            .ToList();

        offenders.Should().BeEmpty("aggregate-root Status transitions must go through guarded domain methods");
    }

    [Fact]
    public void No_domain_type_exposes_a_public_enum_Status_setter()
    {
        // Drift guard for the whole domain assembly: any lifecycle enum named "Status" must be
        // encapsulated. Value objects use string fields or differently-named properties, so this is
        // precise to aggregate lifecycle state.
        var offenders = typeof(Rfq).Assembly.GetTypes()
            .Where(t => t.IsClass)
            .Select(t => t.GetProperty("Status"))
            .Where(p => p is not null && p.PropertyType.IsEnum && p.SetMethod is { IsPublic: true })
            .Select(p => $"{p!.DeclaringType!.Name}.Status")
            .ToList();

        offenders.Should().BeEmpty("a new aggregate must not reintroduce a public enum Status setter");
    }

    [Fact]
    public void No_service_assigns_Status_or_uses_the_seed_only_backdoor()
    {
        var servicesDir = ServicesDir();
        var files = Directory.GetFiles(servicesDir, "*.cs")
            .Where(f => Path.GetFileName(f) != "DevelopmentDataSeeder.cs")   // the one legitimate SeededAs user
            .ToList();
        files.Should().NotBeEmpty("the source scan must find the service files");

        var statusWrites = new List<string>();
        var backdoorCalls = new List<string>();
        var writeRx = new Regex(@"\.Status\s*=(?!=)");        // matches `.Status =`, not `.Status ==`/`!=`

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = StripComment(lines[i]);
                if (writeRx.IsMatch(code)) statusWrites.Add($"{Path.GetFileName(file)}:{i + 1}");
                if (code.Contains(".SeededAs(")) backdoorCalls.Add($"{Path.GetFileName(file)}:{i + 1}");
            }
        }

        statusWrites.Should().BeEmpty("services must transition status through guarded domain methods, not raw `.Status =`");
        backdoorCalls.Should().BeEmpty("SeededAs is test/seed-only — a service using it reopens the hole T3 closed");
    }

    private static string StripComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? line[..idx] : line;
    }

    private static string ServicesDir()
    {
        const string rel = "src/eProcure.Infrastructure/Services";
        var relParts = rel.Split('/');
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(new[] { dir.FullName }.Concat(relParts).ToArray())))
            dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException($"Could not locate '{rel}' above {AppContext.BaseDirectory} for the source scan.");
        return Path.Combine(new[] { dir.FullName }.Concat(relParts).ToArray());
    }
}
