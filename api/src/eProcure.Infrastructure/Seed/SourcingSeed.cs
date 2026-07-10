using System.Text.Json;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Services;

namespace eProcure.Infrastructure.Seed;

/// <summary>
/// Dummy requisitions, the 2 reusable library forms, and the 4 demo RFQs (one per
/// state) from SEED-DATA.md §4–6 / the prototype. No NetSuite.
/// </summary>
public static class SourcingSeed
{
    private static string Cfg(object o) => JsonSerializer.Serialize(o);

    private static FormItem Item(string kind, string group, string section, string label,
        string type, bool required, object? cfg = null) => new()
    {
        Kind = kind, Group = group, Section = section, Label = label, Type = type,
        Required = required, ConfigJson = cfg is null ? "{}" : Cfg(cfg),
    };

    // ---- The two library forms (prototype defaultForm / finForm) ----
    public static (List<FormItem> Items, List<string> Tech, List<string> Comm) StandardForm()
    {
        var items = new List<FormItem>
        {
            Item("terms", "commercial", "", "Standard RFQ terms and conditions apply. Vendor must accept to submit a commercial bid.", "", true),
            Item("question", "commercial", "Commercial terms", "Proposed delivery lead time (weeks)", "number", true, new { unit = "weeks" }),
            Item("question", "commercial", "Commercial terms", "Validity of quotation (days)", "number", true, new { unit = "days" }),
            Item("instruction", "technical", "", "Provide accurate company information. Attachments are uploaded in the portal, not in the Excel template.", "", false),
            Item("question", "technical", "Company & experience", "Company profile and registration summary", "long_text", true),
            Item("question", "technical", "Company & experience", "Years of experience supplying similar equipment", "number", true, new { unit = "years" }),
            Item("question", "technical", "Company & experience", "ISO certifications held", "multi", false, new { options = new[] { "ISO 9001", "ISO 14001", "ISO 45001" } }),
            Item("question", "technical", "Company & experience", "Upload ISO / quality certificates", "attachment", true, new { filetypes = "PDF", multiple = true }),
        };
        return (items, ["Company & experience"], ["Commercial terms"]);
    }

    public static (List<FormItem> Items, List<string> Tech, List<string> Comm) FinancialForm()
    {
        var items = new List<FormItem>
        {
            Item("terms", "commercial", "", "Vendor accepts the financial disclosure terms and confirms the information provided is accurate.", "", true),
            Item("question", "commercial", "Financial standing", "Audited financials — last 3 years", "table", true,
                new { columns = new[] { "FY2023", "FY2024", "FY2025" }, rows = new[] { "Revenue", "Net profit", "Total assets", "Current ratio" }, cellType = "money" }),
            Item("question", "commercial", "Financial standing", "Upload audited financial statements", "attachment", true, new { filetypes = "PDF", multiple = true }),
            Item("question", "technical", "Company information", "Company registration number", "short_text", true),
            Item("question", "technical", "Company information", "Years in operation", "number", true, new { unit = "years" }),
            Item("question", "technical", "Company information", "Reference projects (up to 3)", "group", false,
                new { fields = new[] { new { label = "Project", type = "short_text" }, new { label = "Client", type = "short_text" }, new { label = "Value", type = "money" }, new { label = "Year", type = "number" } }, max = 3 }),
        };
        return (items, ["Company information"], ["Financial standing"]);
    }

    public static List<FormTemplate> FormTemplates(DateTime now)
    {
        var std = StandardForm();
        var fin = FinancialForm();
        return
        [
            new FormTemplate { Code = "FORM-2026-0001", Name = "Standard Technical Questionnaire", Version = 1, Items = Ordered(std.Items), TechnicalSections = std.Tech, CommercialSections = std.Comm, CreatedUtc = now, UpdatedUtc = now },
            new FormTemplate { Code = "FORM-2026-0002", Name = "Financial Pre-Qualification", Version = 1, Items = Ordered(fin.Items), TechnicalSections = fin.Tech, CommercialSections = fin.Comm, CreatedUtc = now, UpdatedUtc = now },
        ];
    }

    private static List<FormItem> Ordered(List<FormItem> items)
    {
        for (var i = 0; i < items.Count; i++) items[i].Order = i;
        return items;
    }

    private static PrLine L(string code, string desc, decimal qty, string uom, decimal rate, string status = "available", string? @ref = null)
        => PrLine.Create(code, desc, qty, uom, rate, SourcingMapping.FromLegacyToken(status), @ref);

    // Controlled analytics codes use the shared SourcingMapping.DimCode (conformed dimension, §4).
    private static string DimCode(string display) => SourcingMapping.DimCode(display);

    private static DateOnly Day(string ddMMyyyy) =>
        DateOnly.ParseExact(ddMMyyyy, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    public static List<PurchaseRequisition> Requisitions(DateTime now)
    {
        PurchaseRequisition Pr(string code, string requestor, string dept, string loc, string memo, string job,
            string cat, string raised, string need, decimal value, List<PrLine> lines, string costCentre, string? project)
        {
            var pr = new PurchaseRequisition
            {
                Code = code, Requestor = requestor, Department = dept, Location = loc, Memo = memo, Job = job,
                Category = cat, RaisedDate = raised, RequiredDate = need, Value = value, Lines = lines,
                CostCentre = costCentre, Project = project, Status = "Approved", Submitted = true,
                Currency = "MYR",
                DepartmentCode = DimCode(dept), LocationCode = DimCode(loc),
                CategoryCode = DimCode(cat), JobCode = DimCode(job),
                RaisedOn = Day(raised), RequiredOn = Day(need),
                CreatedUtc = now, UpdatedUtc = now,
            };
            pr.RecomputeHeaderStatus();   // derive header from line states (§2.5)
            return pr;
        }

        return
        [
            Pr("PR-2026-0412", "Aishah Karim", "Facilities", "Bintulu Plant", "Cooling water pump set replacement", "JOB-CWS-014", "Rotating Equipment", "02/06/2026", "30/07/2026", 184000m,
                [L("MEP-PUMP-075", "Centrifugal Pump, 75 kW, end-suction", 4, "Unit", 38500m), L("ELE-VFD-075", "VFD Drive, 75 kW, IP55", 4, "Unit", 7500m)], "CC-MAINT-01", "TA-2026 Turnaround"),
            Pr("PR-2026-0415", "Ramesh Nair", "Production", "Samalaju Terminal", "Valve replacement, Train 2 shutdown", "JOB-SD2-009", "Valves & Fittings", "04/06/2026", "25/07/2026", 96600m,
                [L("VLV-GAT-150", "Gate Valve, DN150, PN16, CS", 24, "Unit", 2900m, "rfq", "RFQ-2026-031"), L("VLV-GLB-100", "Globe Valve, DN100, PN16", 12, "Unit", 2250m)], "CC-OPS-02", null),
            Pr("PR-2026-0418", "Lim Chee Keong", "Maintenance", "Bintulu Plant", "Tank farm piping, corrosion replacement", "JOB-TF-031", "Piping", "05/06/2026", "12/08/2026", 141600m,
                [L("PIP-CS6-SCH40", "Carbon Steel Pipe, 6\", SCH40, 6m", 120, "Length", 850m), L("PIP-ELB-6", "Pipe Elbow 90°, 6\", SCH40", 80, "Unit", 180m), L("PIP-FLG-6", "Weld Neck Flange, 6\", PN16", 60, "Unit", 420m)], "CC-PROJ-07", "Facilities Upgrade"),
            Pr("PR-2026-0421", "Siti Aminah", "HSE", "Kuching HQ", "PPE restock, working at height", "OPEX-2026", "Safety / PPE", "06/06/2026", "05/08/2026", 38400m,
                [L("HSE-HARN-2L", "Safety Harness, full body, double lanyard", 60, "Set", 320m), L("HSE-GAS-4", "Gas Detector, 4-gas, portable", 15, "Unit", 1280m, "awarded", "PO-2026-1185")], "CC-HSE-03", "HSE Refresh"),
            Pr("PR-2026-0423", "Ramesh Nair", "Production", "Samalaju Terminal", "Rotating spares restock", "JOB-RS-077", "Rotating Equipment", "09/06/2026", "20/08/2026", 71000m,
                [L("ELE-MOT-037", "Electric Motor, 37 kW, TEFC, IE3", 6, "Unit", 8500m), L("MEC-BRG-22220", "Bearing, spherical roller, 22220", 40, "Unit", 500m)], "CC-OPS-02", null),
            Pr("PR-2026-0425", "Tan Wei Loon", "Instrumentation", "Bintulu Plant", "Pressure transmitter calibration spares", "JOB-INST-022", "Instrumentation", "11/06/2026", "18/08/2026", 42000m,
                [L("INS-PT-300", "Pressure Transmitter, 0 to 300 bar", 20, "Unit", 1450m), L("INS-CAL-KIT", "Calibration Kit, field", 2, "Set", 6500m)], "CC-OPS-02", null),
            Pr("PR-2026-0428", "Nurul Izzah", "Electrical", "Samalaju Terminal", "Switchgear maintenance spares", "JOB-EL-040", "Electrical", "13/06/2026", "28/08/2026", 27650m,
                [L("ELE-CON-040", "Contactor, 3-pole, 40 A, AC-3", 30, "Unit", 680m), L("ELE-RLY-4PDT", "Control Relay, 4PDT, 24 VDC", 50, "Unit", 145m)], "CC-PROJ-07", "Facilities Upgrade"),
        ];
    }

    public sealed record RfqSeedSpec(
        string Code, string Title, RfqEnvelope Envelope, RfqStatus Status, int CloseOffsetDays,
        string[] PrRefs, string[] InvitedSlugs, string[] TechEvaluators, string[] CommEvaluators,
        (string Code, string Desc, decimal Qty, string Uom)[] Lines, string Form, string Owner);

    public static readonly RfqSeedSpec[] RfqSpecs =
    [
        new("RFQ-2026-0079", "Pump & VFD Package — Facilities Upgrade", RfqEnvelope.Dual, RfqStatus.Evaluation, -5,
            ["PR-2026-0412"], ["sentausa", "hidro", "megatech"], ["u_hafiz", "u_nur", "u_raj"], ["u_tan"],
            [("MEP-PUMP-075", "Centrifugal Pump, 75 kW, end-suction", 4, "Unit"), ("ELE-VFD-075", "VFD Drive, 75 kW, IP55", 4, "Unit")], "standard", "u_faridah"),
        new("RFQ-2026-0083", "Valve Package DN100–DN150", RfqEnvelope.Dual, RfqStatus.Closed, -1,
            ["PR-2026-0415"], ["pantai", "borneo", "delta"], ["u_hafiz", "u_nur"], ["u_tan"],
            [("VLV-GAT-150", "Gate Valve, DN150, PN16, CS", 24, "Unit"), ("VLV-GLB-100", "Globe Valve, DN100, PN16", 12, "Unit")], "financial", "u_lim"),
        new("RFQ-2026-0087", "Piping Materials — 6\" CS Line", RfqEnvelope.Single, RfqStatus.Open, 5,
            ["PR-2026-0418"], ["borneo", "delta"], [], [],
            [("PIP-CS6-SCH40", "Carbon Steel Pipe, 6\", SCH40, 6m", 120, "Length"), ("PIP-ELB-6", "Pipe Elbow 90°, 6\", SCH40", 80, "Unit"), ("PIP-FLG-6", "Weld Neck Flange, 6\", PN16", 60, "Unit")], "standard", "u_faridah"),
        new("RFQ-2026-0074", "HSE Equipment Refresh", RfqEnvelope.Single, RfqStatus.Awarded, -14,
            ["PR-2026-0421"], ["mutiara", "klind"], [], [],
            [("HSE-HARN-2L", "Safety Harness, full body, double lanyard", 60, "Set"), ("HSE-GAS-4", "Gas Detector, 4-gas, portable", 15, "Unit")], "standard", "u_lim"),
    ];

    // ---- Bids (SEED-DATA §7): vary prices so there is a clear low bidder per line ----
    public sealed record BidSeedSpec(
        string RfqCode, string VendorSlug, bool Submitted,
        (string Code, bool Bid, decimal Price)[] Lines, int Lead, int Warranty, string File);

    public static readonly BidSeedSpec[] BidSpecs =
    [
        // RFQ-0079 (dual, evaluation) — all 3 invited submitted
        new("RFQ-2026-0079", "sentausa", true, [("MEP-PUMP-075", true, 24500m), ("ELE-VFD-075", true, 9800m)], 8, 24, "Sentausa_TechCompliance.pdf"),
        new("RFQ-2026-0079", "hidro", true, [("MEP-PUMP-075", true, 22900m), ("ELE-VFD-075", true, 10500m)], 10, 18, "Hidro_Compliance.pdf"),
        new("RFQ-2026-0079", "megatech", true, [("MEP-PUMP-075", false, 0m), ("ELE-VFD-075", true, 9200m)], 7, 24, "MegaTech_VFD_Spec.pdf"),
        // RFQ-0083 (dual, closed) — pantai, borneo, delta submitted
        new("RFQ-2026-0083", "pantai", true, [("VLV-GAT-150", true, 920m), ("VLV-GLB-100", true, 2100m)], 6, 24, "Pantai_EN10204.pdf"),
        new("RFQ-2026-0083", "borneo", true, [("VLV-GAT-150", true, 980m), ("VLV-GLB-100", true, 2250m)], 7, 18, "Borneo_MillCert.pdf"),
        new("RFQ-2026-0083", "delta", true, [("VLV-GAT-150", true, 1010m), ("VLV-GLB-100", true, 2180m)], 8, 12, "Delta_Compliance.pdf"),
        // RFQ-0087 (single, open) — borneo saved a draft; delta has no bid (live demo)
        new("RFQ-2026-0087", "borneo", false, [("PIP-CS6-SCH40", true, 165m), ("PIP-ELB-6", true, 175m), ("PIP-FLG-6", true, 410m)], 5, 12, "Borneo_PipeQuote.pdf"),
        // RFQ-0074 (single, awarded) — mutiara + klind submitted (mutiara wins)
        new("RFQ-2026-0074", "mutiara", true, [("HSE-HARN-2L", true, 230m), ("HSE-GAS-4", true, 3050m)], 5, 24, "Mutiara_Quote.pdf"),
        new("RFQ-2026-0074", "klind", true, [("HSE-HARN-2L", true, 245m), ("HSE-GAS-4", true, 3100m)], 7, 12, "Klind_Quote.pdf"),
    ];

    // A plausible per-vendor answer for each questionnaire item, varied by lead/warranty
    // so the evaluation/comparison response tables read realistically.
    private static string SeedAnswer(FormItem i, BidSeedSpec s) => i.Type switch
    {
        "yesno" => s.Lead % 2 == 0 ? "Yes" : "No",
        "number" => $"{8 + s.Lead}",
        "money" => $"{(s.Lead + 4) * 1500}",
        "percent" => $"{92 + (s.Lead % 6)}",
        "date" => "2026-08-15",
        "list" => "ISO 9001:2015",
        "multi" => "ISO 9001:2015, ISO 14001",
        "long_text" => $"{s.Warranty}-month warranty; {s.Lead + 8} years supplying comparable equipment to O&G operators, full MTC traceability provided.",
        "attachment" or "table" or "group" => "",
        _ => "Confirmed — compliant with specification.",
    };

    public static Bid ToBid(BidSeedSpec s, Guid rfqId, Guid vendorId, Rfq rfq, DateTime now)
    {
        var lineQty = rfq.Lines.ToDictionary(l => l.ItemCode, l => l.Qty);
        return new Bid
        {
            Code = $"BID-{s.RfqCode[^4..]}-{s.VendorSlug}",
            RfqId = rfqId,
            VendorId = vendorId,
            Submitted = s.Submitted,
            SavedDraft = !s.Submitted,
            SubmittedUtc = s.Submitted ? (rfq.ClosesUtc ?? now).AddDays(-1) : null,
            Lead = s.Lead,
            Warranty = s.Warranty,
            Lines = s.Lines.Select(l => new BidLine
            {
                ItemCode = l.Code, Bidding = l.Bid, Price = l.Price,
                Qty = lineQty.GetValueOrDefault(l.Code, 0), Partial = false,
            }).ToList(),
            Answers = rfq.FormItems.Where(i => i.Kind == "question").OrderBy(i => i.Order)
                .Select(i => new BidAnswer { QuestionOrder = i.Order, Value = SeedAnswer(i, s) }).ToList(),
            Files = [new BidAttachment { FileName = s.File }],
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    public static Rfq ToRfq(RfqSeedSpec s, DateTime now, Func<string, string?> slugToVendorId)
    {
        var (items, tech, comm) = s.Form == "financial" ? FinancialForm() : StandardForm();
        var closes = now.AddDays(s.CloseOffsetDays);
        var created = closes.AddDays(-14);
        var rfq = new Rfq
        {
            Code = s.Code,
            Title = s.Title,
            Envelope = s.Envelope,
            Status = s.Status,
            Currency = "MYR",
            OwnerUserId = s.Owner,
            OpensUtc = created,
            ClosesUtc = closes,
            OriginalClosesUtc = s.Status == RfqStatus.Draft ? null : closes,   // set at release for non-drafts (§1.3)
            PrRefs = [.. s.PrRefs],
            TechnicalEvaluatorIds = [.. s.TechEvaluators],
            CommercialEvaluatorIds = [.. s.CommEvaluators],
            TechFinalized = s.Status == RfqStatus.Awarded,
            CommercialOpened = s.Status == RfqStatus.Awarded,
            Lines = s.Lines.Select(l => new RfqLine { LineCode = l.Code, ItemCode = l.Code, Description = l.Desc, Qty = l.Qty, Uom = l.Uom, PrRef = s.PrRefs.FirstOrDefault() }).ToList(),
            FormItems = Ordered(items),
            TechnicalSections = tech,
            CommercialSections = comm,
            CreatedUtc = created,
            UpdatedUtc = now,
        };
        // Invited vendors are now RfqInvitation rows. Seeded at Invited; the data seeder promotes to
        // BidSubmitted where a submitted bid exists (mirrors the migration backfill).
        foreach (var slug in s.InvitedSlugs)
            if (slugToVendorId(slug) is { } vidStr && Guid.TryParse(vidStr, out var vid))
                rfq.Invitations.Add(RfqInvitation.Seed(rfq.Id, vid, RfqInvitationStatus.Invited, created));
        return rfq;
    }
}
