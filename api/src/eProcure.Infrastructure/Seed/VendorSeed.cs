using eProcure.Domain.Suppliers;

namespace eProcure.Infrastructure.Seed;

/// <summary>
/// The 10 dummy vendors from the prototype's DATA.vendors, plus the prototype's
/// enrichVendors() logic to populate full master sub-records (contacts, addresses,
/// banking, certs, currencies) so every Vendor Master tab is populated for the demo.
/// </summary>
public static class VendorSeed
{
    public sealed record Row(
        string Slug, string Name, string Code, string Ssm, VendorType Type, string? Llrc,
        VendorStatus Status, string Region, string State, string City, decimal Rating, string[] Swec,
        int Otd, int Quality, int Breaches, int Lead, int Response, int WinRate, decimal SpendYtd, int Pos);

    public static readonly Row[] Rows =
    [
        new("sentausa", "Sentausa Engineering Sdn Bhd", "SWK-V-10293", "200801023391", VendorType.Swec, "Licensed", VendorStatus.Registered, "Peninsular", "Selangor", "Shah Alam", 4.6m, ["40101800P", "40141700P", "31171500P"], 96, 98, 0, 6, 97, 34, 4180000m, 42),
        new("pantai", "Pantai Valve & Fitting", "SWK-V-10410", "200501008822", VendorType.Swec, "Licensed", VendorStatus.Registered, "Peninsular", "Selangor", "Klang", 4.7m, ["40142300P", "40142400P", "30171500P"], 98, 99, 0, 5, 99, 41, 5260000m, 51),
        new("megatech", "MegaTech Resources", "SWK-V-11002", "201101099210", VendorType.Swec, "Registered", VendorStatus.Registered, "Peninsular", "Perak", "Ipoh", 4.1m, ["39121000P", "40141700P", "26121600P"], 88, 92, 2, 9, 84, 19, 1480000m, 17),
        new("tenaga", "Tenaga Switchgear Sdn Bhd", "SWK-V-11890", "201301044120", VendorType.Swec, "Licensed", VendorStatus.Registered, "Peninsular", "Selangor", "Shah Alam", 4.4m, ["39121000P", "39121400P", "40141700P"], 94, 96, 0, 7, 93, 28, 2960000m, 31),
        new("hidro", "Hidro Systems Sdn Bhd", "SWK-V-11150", "201001033441", VendorType.NonSwec, null, VendorStatus.Provisional, "Peninsular", "Negeri Sembilan", "Seremban", 4.4m, ["40101800P", "40142700P"], 90, 94, 1, 8, 88, 22, 980000m, 12),
        new("klind", "KL Industrial Supplies", "SWK-V-10781", "200701018830", VendorType.NonSwec, null, VendorStatus.Registered, "Peninsular", "Selangor", "Petaling Jaya", 4.3m, ["40000000P", "40142300P", "46181500P"], 91, 95, 1, 7, 90, 24, 1320000m, 22),
        new("delta", "Delta Gas Detection", "SWK-V-11320", "201201040021", VendorType.NonSwec, null, VendorStatus.Provisional, "Peninsular", "Pahang", "Kuantan", 4.1m, ["41112400P", "39121000P"], 89, 95, 0, 10, 86, 18, 740000m, 9),
        new("mutiara", "Mutiara Pipe & Steel", "SWK-V-10744", "200601012044", VendorType.Swec, "Registered", VendorStatus.Registered, "Peninsular", "Johor", "Pasir Gudang", 4.3m, ["30171500P", "30171600P", "30103600P", "72101500P"], 92, 94, 1, 9, 91, 26, 2110000m, 24),
        new("borneo", "Borneo Inspection Services", "SWK-V-12210", "201401055120", VendorType.Swec, "Registered", VendorStatus.Registered, "Sarawak", "Sarawak", "Bintulu", 4.5m, ["81111800P", "72151500P", "72154000P"], 95, 97, 0, 4, 96, 37, 1890000m, 19),
        new("sabah", "Sabah Mechanical Works", "SWK-V-11770", "201301041770", VendorType.Swec, "Registered", VendorStatus.Registered, "Sabah", "Sabah", "Kota Kinabalu", 4.3m, ["40101800P", "30171500P", "31171500P"], 91, 95, 0, 11, 89, 23, 1240000m, 14),
    ];

    private static readonly DateTime Created = new(2024, 3, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Updated = new(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc);

    public static Vendor ToVendor(Row r)
    {
        var tail = new string(r.Code.Where(char.IsDigit).ToArray());
        tail = tail.Length >= 4 ? tail[^4..] : tail.PadLeft(4, '0');

        var currencies = new List<VendorCurrency> { new() { Code = "MYR", IsPrimary = true } };
        if (r.Slug is "pantai" or "mutiara" or "borneo" or "megatech" or "tenaga")
            currencies.Add(new VendorCurrency { Code = "USD", IsPrimary = false });
        if (r.Slug == "megatech")
            currencies.Add(new VendorCurrency { Code = "SGD", IsPrimary = false });

        var banking = new List<VendorBankAccount>
        {
            new() { Bank = "Malayan Banking Berhad", AccountNo = $"5141-{tail}-22", Swift = "MBBEMYKL", Currency = "MYR", IsPrimary = true },
        };
        if (currencies.Any(c => c.Code == "USD"))
            banking.Add(new VendorBankAccount { Bank = "HSBC Bank Malaysia", AccountNo = $"201-{tail}-880", Swift = "HBMBMYKL", Currency = "USD", IsPrimary = false });

        var certs = new List<VendorCertification>
        {
            new() { Name = "SSM registration (CCM)", Number = r.Ssm, ValidTo = "—", Status = "Valid" },
            new() { Name = "ISO 9001:2015", Number = $"QMS-{tail}", ValidTo = "2027", Status = "Valid" },
        };
        if (r.Type == VendorType.Swec)
            certs.Add(new VendorCertification { Name = "PETRONAS SWEC registration", Number = r.Code, ValidTo = "2027", Status = "Valid" });
        if (r.Slug is "sentausa" or "mutiara" or "sabah" or "borneo")
            certs.Add(new VendorCertification { Name = "CIDB grade G7", Number = $"CIDB-{tail}", ValidTo = "2026", Status = "Valid" });

        var firstWord = r.Name.Split(' ')[0];
        return new Vendor
        {
            Code = r.Code,
            Name = r.Name,
            RegisteredName = r.Name,
            RegistrationNo = r.Ssm,
            TaxId = $"C{new string(r.Code.Where(char.IsDigit).ToArray())}02000",
            Type = r.Type,
            LlrcTier = r.Llrc,
            Region = r.Region,
            State = r.State,
            City = r.City,
            Country = "Malaysia",
            Rating = r.Rating,
            PaymentTerms = "30 days nett",
            CreditLimit = r.Type == VendorType.Swec ? 1_000_000m : 500_000m,
            Categories = [.. r.Swec],
            Performance = new VendorPerformance
            {
                Otd = r.Otd, Quality = r.Quality, Breaches = r.Breaches, Lead = r.Lead,
                Response = r.Response, WinRate = r.WinRate, SpendYtd = r.SpendYtd, Pos = r.Pos,
            },
            Contacts =
            [
                new() { Name = "Sales & Commercial Desk", Role = "Sales / Commercial", Email = $"sales@{r.Slug}.com.my", Phone = $"+60 3-5512 {tail}", IsPrimary = true },
                new() { Name = "Accounts Payable", Role = "Finance / AP", Email = $"finance@{r.Slug}.com.my", Phone = $"+60 3-5512 9{tail[^3..]}", IsPrimary = false },
            ],
            Addresses =
            [
                new() { Type = "Registered", Line = $"Lot {tail}, Jalan Perusahaan 2", City = r.City, State = r.State, Country = "Malaysia", Postcode = $"4{tail}", IsPrimary = true },
                new() { Type = "Billing", Line = $"Level 3, Wisma {firstWord}", City = r.City, State = r.State, Country = "Malaysia", Postcode = $"4{tail}", IsPrimary = false },
                new() { Type = "Shipping", Line = $"Warehouse {tail[^2..]}, Kawasan Perindustrian", City = r.City, State = r.State, Country = "Malaysia", Postcode = $"4{tail}", IsPrimary = false },
            ],
            BankAccounts = banking,
            Certifications = certs,
            Currencies = currencies,
            CreatedUtc = Created,
            UpdatedUtc = Updated,
        }.SeededAs(r.Status);
    }
}
