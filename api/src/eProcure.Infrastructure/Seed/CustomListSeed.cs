using eProcure.Domain.Configuration;

namespace eProcure.Infrastructure.Seed;

/// <summary>
/// Seeds the standard system Custom Lists + their values (country/state/city/currency/payment-terms/
/// bank). Data-driven: values live in the DB and are admin-editable; nothing is hardcoded in a form.
/// Codes are the stored source of truth; labels are shown.
/// </summary>
public static class CustomListSeed
{
    public static IEnumerable<CustomList> All(DateTime now)
    {
        CustomList List(string code, string name, string? parent, params (string Code, string Label, string? Parent)[] values)
        {
            var list = new CustomList { Code = code, Name = name, ParentListCode = parent, IsSystem = true, CreatedUtc = now, UpdatedUtc = now };
            var sort = 0;
            foreach (var (c, l, p) in values)
                list.Values.Add(new CustomListValue { Code = c, Label = l, ParentValueCode = p, Sort = sort++, Active = true });
            return list;
        }

        yield return List("COUNTRY", "Country", null,
            ("MY", "Malaysia", null), ("SG", "Singapore", null), ("BN", "Brunei", null), ("ID", "Indonesia", null),
            ("TH", "Thailand", null), ("AE", "United Arab Emirates", null), ("GB", "United Kingdom", null),
            ("US", "United States", null), ("CN", "China", null), ("JP", "Japan", null), ("AU", "Australia", null), ("IN", "India", null));

        yield return List("STATE", "State / Region", "COUNTRY",
            ("JHR", "Johor", "MY"), ("KDH", "Kedah", "MY"), ("KTN", "Kelantan", "MY"), ("MLK", "Melaka", "MY"),
            ("NSN", "Negeri Sembilan", "MY"), ("PHG", "Pahang", "MY"), ("PNG", "Pulau Pinang", "MY"), ("PRK", "Perak", "MY"),
            ("PLS", "Perlis", "MY"), ("SGR", "Selangor", "MY"), ("TRG", "Terengganu", "MY"), ("SBH", "Sabah", "MY"),
            ("SWK", "Sarawak", "MY"), ("KUL", "W.P. Kuala Lumpur", "MY"), ("LBN", "W.P. Labuan", "MY"), ("PJY", "W.P. Putrajaya", "MY"));

        yield return List("CITY", "City", "STATE",
            Cities("SWK", "Kuching", "Miri", "Bintulu", "Sibu", "Samalaju", "Bau")
                .Concat(Cities("SGR", "Shah Alam", "Petaling Jaya", "Klang", "Subang Jaya", "Kajang", "Cyberjaya"))
                .Concat(Cities("KUL", "Kuala Lumpur"))
                .Concat(Cities("SBH", "Kota Kinabalu", "Sandakan", "Tawau", "Lahad Datu"))
                .Concat(Cities("JHR", "Johor Bahru", "Pasir Gudang", "Batu Pahat"))
                .Concat(Cities("PNG", "George Town", "Butterworth", "Bayan Lepas"))
                .Concat(Cities("PHG", "Kuantan", "Gebeng")).ToArray());

        yield return List("CURRENCY", "Currency", null,
            ("MYR", "Malaysian Ringgit (RM)", null), ("USD", "US Dollar (USD)", null), ("SGD", "Singapore Dollar (SGD)", null),
            ("EUR", "Euro (EUR)", null), ("GBP", "British Pound (GBP)", null), ("JPY", "Japanese Yen (JPY)", null),
            ("CNY", "Chinese Yuan (CNY)", null), ("AUD", "Australian Dollar (AUD)", null), ("AED", "UAE Dirham (AED)", null));

        yield return List("PAYMENT_TERMS", "Payment terms", null,
            ("COD", "Cash on delivery", null), ("ADV", "Advance payment", null), ("NET30", "30 days", null),
            ("NET45", "45 days", null), ("NET60", "60 days", null), ("NET90", "90 days", null), ("LC", "Letter of credit", null));

        yield return List("BANK", "Bank", null,
            ("MBB", "Maybank", null), ("CIMB", "CIMB Bank", null), ("PBB", "Public Bank", null), ("RHB", "RHB Bank", null),
            ("HLB", "Hong Leong Bank", null), ("AMB", "AmBank", null), ("BIMB", "Bank Islam", null), ("BKRM", "Bank Rakyat", null),
            ("OCBC", "OCBC Bank (M)", null), ("HSBC", "HSBC Bank (M)", null), ("SCB", "Standard Chartered (M)", null),
            ("UOB", "UOB (M)", null), ("AFFIN", "Affin Bank", null), ("ALB", "Alliance Bank", null));

        // RFQ governance reason codes (RFQ-LIFECYCLE-ADDENDUM §4) — SYSTEM lists validated server-side.
        yield return List("RFQ_DECLINE_REASON", "RFQ decline reason", null,
            ("CAPACITY", "No capacity in timeframe", null), ("OUT_OF_SCOPE", "Outside our scope / products", null),
            ("LEAD_TIME", "Cannot meet delivery", null), ("COMMERCIAL", "Commercial terms", null), ("OTHER", "Other — see note", null));

        yield return List("RFQ_RESCIND_REASON", "RFQ rescind reason", null,
            ("DUPLICATE", "Invited in error / duplicate", null), ("COMPLIANCE", "Compliance / eligibility issue", null),
            ("SCOPE_CHANGE", "Requirement changed", null), ("OTHER", "Other — see note", null));

        yield return List("RFQ_EXTENSION_REASON", "RFQ extension reason", null,
            ("VENDOR_REQUEST", "Vendor request", null), ("CLARIFICATION", "Late clarification issued", null),
            ("LOW_RESPONSE", "Insufficient responses", null), ("HOLIDAY", "Public holiday / shutdown", null), ("OTHER", "Other — see note", null));
    }

    private static IEnumerable<(string Code, string Label, string? Parent)> Cities(string state, params string[] cities) =>
        cities.Select(c => (c, c, (string?)state));   // city code = its name (readable, unique within the state)
}
