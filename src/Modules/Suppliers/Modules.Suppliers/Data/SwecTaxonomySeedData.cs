using FSH.Modules.Suppliers.Domain;

namespace FSH.Modules.Suppliers.Data;

/// <summary>
/// The PETRONAS-style SWEC taxonomy, mirroring the old eProcure prototype's SWEC_TREE
/// (Discipline -&gt; Group -&gt; Item). Flattened into <see cref="SwecCategory"/> rows.
/// </summary>
public static class SwecTaxonomySeedData
{
    private sealed record Node(string Code, string Name, Node[]? Children = null);

    private static readonly Node[] Tree =
    [
        new("40", "Mechanical", [
            new("40.10", "Rotating equipment", [
                new("40101800P", "Centrifugal pumps"), new("40101900P", "Compressors"), new("40102000P", "Gas turbines")]),
            new("40.14", "Valves & actuators", [
                new("40142300P", "Industrial valves"), new("40142400P", "Control valves"), new("40142700P", "Actuators")]),
            new("40.15", "Static equipment", [
                new("40151500P", "Heat exchangers"), new("40151600P", "Pressure vessels")]),
            new("31.17", "Mechanical spares", [
                new("31171500P", "Bearings"), new("31171600P", "Mechanical seals")]),
        ]),
        new("39", "Electrical", [
            new("39.12", "Power distribution", [
                new("39121000P", "Switchgear"), new("39121400P", "Transformers"), new("39122000P", "UPS systems")]),
            new("40.14E", "Drives & motors", [
                new("40141700P", "Variable speed drives"), new("26111700P", "Electric motors")]),
            new("26.12", "Cabling", [
                new("26121600P", "Power cables"), new("26121800P", "Instrument cables")]),
        ]),
        new("41", "Instrumentation", [
            new("41.11", "Field instruments", [
                new("41112400P", "Gas detection"), new("41112500P", "Pressure transmitters"), new("41112600P", "Flow meters")]),
            new("41.12", "Control systems", [
                new("41120000P", "DCS / PLC"), new("41121800P", "Analyzers")]),
        ]),
        new("30", "Piping", [
            new("30.17", "Pipes & fittings", [
                new("30171500P", "Flanges"), new("30171600P", "Pipes & tubes"), new("30171700P", "Fittings")]),
            new("31.19", "Gaskets & sealing", [
                new("31191500P", "Gaskets")]),
        ]),
        new("72", "Civil & structural", [
            new("30.10", "Structural steel", [
                new("30103600P", "Steel fabrication")]),
            new("72.10", "Civil works", [
                new("72101500P", "Construction services"), new("72102900P", "Earthworks")]),
        ]),
        new("46", "HSE", [
            new("46.18", "PPE", [
                new("46181500P", "Safety equipment"), new("46181600P", "Protective apparel")]),
            new("46.19", "Fire & safety", [
                new("46191600P", "Fire protection")]),
        ]),
        new("81", "Inspection & services", [
            new("81.11", "Inspection", [
                new("81111800P", "NDT & inspection"), new("81112200P", "Certification services")]),
            new("72.15", "Maintenance", [
                new("72151500P", "Maintenance services"), new("72154000P", "Turnaround services")]),
            new("81.10", "Engineering", [
                new("81101500P", "Design & consultancy")]),
        ]),
        new("44", "General & MRO", [
            new("40.00", "MRO consumables", [
                new("40000000P", "General consumables")]),
            new("44.10", "Office & general", [
                new("44103100P", "Office supplies")]),
        ]),
    ];

    public static List<SwecCategory> Build()
    {
        var rows = new List<SwecCategory>();
        void Walk(Node[] nodes, string? parent, List<string> path)
        {
            foreach (var n in nodes)
            {
                var p = new List<string>(path) { n.Name };
                bool isLeaf = n.Children is null or { Length: 0 };
                rows.Add(SwecCategory.Create(n.Code, n.Name, parent, p.Count, isLeaf, string.Join(" › ", p)));
                if (!isLeaf)
                {
                    Walk(n.Children!, n.Code, p);
                }
            }
        }

        Walk(Tree, null, []);
        return rows;
    }
}
