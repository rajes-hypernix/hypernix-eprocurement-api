using System.Text.Json;
using eProcure.Domain.Sourcing;

namespace eProcure.Infrastructure.Seed;

/// <summary>
/// The starter onboarding question packs (VENDOR-ONBOARDING-SPEC §5), seeded as
/// <see cref="FormTemplate"/>s with <see cref="FormPurpose.Onboarding"/> using the EXISTING
/// <see cref="FormItem"/> engine (each maps to a real <see cref="FormItemVocab"/> type — no second
/// question system). The invite step offers these live; buyers prune/extend them on the Forms page.
/// </summary>
public static class OnboardingSeed
{
    private static string Cfg(object o) => JsonSerializer.Serialize(o);

    private static FormItem Q(string section, string label, string type, bool required, object? cfg = null) => new()
    {
        Kind = "question", Group = "technical", Section = section, Label = label, Type = type,
        Required = required, ConfigJson = cfg is null ? "{}" : Cfg(cfg),
    };

    private static FormTemplate Pack(string code, string name, List<FormItem> items, DateTime now)
    {
        for (var i = 0; i < items.Count; i++) items[i].Order = i;
        return new FormTemplate
        {
            Code = code, Name = name, Purpose = FormPurpose.Onboarding, Version = 1,
            Items = items, TechnicalSections = [items[0].Section], CommercialSections = [],
            CreatedUtc = now, UpdatedUtc = now,
        };
    }

    /// <summary>The four Vendor-Prequalification packs, codes FORM-2026-0003…0006 (0001/0002 are RFQ forms).</summary>
    public static List<FormTemplate> FormTemplates(DateTime now) =>
    [
        Pack("FORM-2026-0003", "Health, Safety & Environment (HSE)",
        [
            Q("HSE", "Do you have a documented HSE policy?", "yesno", true),
            Q("HSE", "Upload your HSE policy", "attachment", false, new { filetypes = "PDF" }),
            Q("HSE", "HSE certification held", "list", false, new { options = new[] { "ISO 45001", "OHSAS 18001", "None" } }),
            Q("HSE", "Lost Time Injuries (LTI) in the last 3 years", "number", false),
            Q("HSE", "LTIFR last year", "number", false),
            Q("HSE", "Emergency response procedures — brief description", "long_text", false),
        ], now),

        Pack("FORM-2026-0004", "Quality & Certification",
        [
            Q("Quality & Certification", "ISO 9001:2015 certified?", "yesno", true),
            Q("Quality & Certification", "Upload ISO 9001 certificate", "attachment", false, new { filetypes = "PDF" }),
            Q("Quality & Certification", "CIDB grade", "list", false, new { options = new[] { "G1", "G2", "G3", "G4", "G5", "G6", "G7", "Not applicable" } }),
            Q("Quality & Certification", "Other certifications", "multi", false, new { options = new[] { "API", "ASME", "PETRONAS licence", "MS ISO", "Other" } }),
            Q("Quality & Certification", "Documented QA/QC procedure in place?", "yesno", false),
            Q("Quality & Certification", "Primary certificate expiry date", "date", false),
        ], now),

        Pack("FORM-2026-0005", "Capability & Capacity",
        [
            Q("Capability & Capacity", "Years in operation", "number", true),
            Q("Capability & Capacity", "Full-time technical personnel", "number", false),
            Q("Capability & Capacity", "Annual turnover band", "list", false, new { options = new[] { "< RM 1M", "RM 1–5M", "RM 5–20M", "RM 20–50M", "> RM 50M" } }),
            Q("Capability & Capacity", "Key equipment / facilities", "long_text", false),
            Q("Capability & Capacity", "Geographic coverage", "multi", false, new { options = new[] { "Sarawak", "Sabah", "Peninsular", "Regional" } }),
            Q("Capability & Capacity", "OEM / principal authorisations held", "long_text", false),
        ], now),

        Pack("FORM-2026-0006", "Compliance & Declarations",
        [
            Q("Compliance & Declarations", "Conflict of interest with SPSB staff/directors?", "yesno", true),
            Q("Compliance & Declarations", "Ongoing litigation/arbitration affecting delivery?", "yesno", false),
            Q("Compliance & Declarations", "Ever blacklisted/suspended by a client?", "yesno", false),
            Q("Compliance & Declarations", "If yes, explain", "long_text", false),
            Q("Compliance & Declarations", "Agree to SPSB Anti-Bribery & Code of Conduct?", "yesno", true),
            Q("Compliance & Declarations", "Authorised signatory name & designation", "short_text", false),
            Q("Compliance & Declarations", "Declaration date", "date", false),
        ], now),
    ];
}
