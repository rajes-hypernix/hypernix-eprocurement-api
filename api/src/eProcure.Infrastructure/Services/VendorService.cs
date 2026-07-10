using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Suppliers;
using eProcure.Domain.Identity;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class VendorService(
    AppDbContext db,
    IClock clock,
    ICodeGenerator codes,
    IAuditLog audit,
    ICurrentUser user) : IVendorService
{
    // Full bank account numbers / SWIFT are visible only to Buyer/Admin on the vendor DETAIL endpoint
    // (SEC-2). Everyone else — and every list/search DTO — gets last-4 only.
    private bool CanSeeBankDetails => user.Roles.Contains(Roles.Buyer) || user.Roles.Contains(Roles.Admin);

    private static string MaskTail(string? value)
    {
        var v = (value ?? "").Trim();
        if (v.Length == 0) return "";
        return v.Length <= 4 ? new string('•', v.Length) : new string('•', v.Length - 4) + v[^4..];
    }
    public static string TypeDisplay(VendorType t) => t == VendorType.Swec ? "SWEC" : "Non-SWEC";
    private static VendorType ParseType(string s) =>
        s is "SWEC" or "Swec" ? VendorType.Swec : VendorType.NonSwec;

    public async Task<IReadOnlyList<VendorListItem>> ListAsync(VendorFilter filter, CancellationToken ct = default)
    {
        var vendors = await db.Vendors.AsNoTracking().OrderBy(v => v.Name).ToListAsync(ct);

        IEnumerable<Vendor> q = vendors;
        if (!string.IsNullOrWhiteSpace(filter.Type) && filter.Type != "all")
            q = q.Where(v => TypeDisplay(v.Type) == filter.Type);
        if (!string.IsNullOrWhiteSpace(filter.Region) && filter.Region != "all")
            q = q.Where(v => v.Region == filter.Region);
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim().ToLowerInvariant();
            q = q.Where(v => v.Name.ToLowerInvariant().Contains(term)
                          || v.Code.ToLowerInvariant().Contains(term));
        }

        return q.Select(v => new VendorListItem(
            v.Id, v.Code, v.Name, TypeDisplay(v.Type), v.Categories,
            v.Region, v.State, v.Rating, v.Performance.Otd, v.Status.ToString())).ToList();
    }

    public async Task<VendorDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var v = await db.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return v is null ? null : Map(v);
    }

    public async Task<VendorDetail> CreateAsync(CreateVendorRequest req, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var v = new Vendor
        {
            Code = await codes.NextAsync("SWK-V", ct),
            Name = req.Name,
            RegisteredName = req.Name,
            // Status defaults to Pending (no explicit set — the setter is now private).
            Type = VendorType.NonSwec,
            Region = req.Region ?? "Peninsular",
            State = req.State ?? "—",
            City = req.City ?? "—",
            CreatedUtc = now,
            UpdatedUtc = now,
            Currencies = [new VendorCurrency { Code = "MYR", IsPrimary = true }],
        };
        db.Vendors.Add(v);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Vendor", v.Code, "Created", after: v.Name, ct: ct);
        return Map(v);
    }

    public async Task<ManualVendorResult> CreateManualAsync(CreateManualVendorRequest req, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        // Duplicate check (C2) — surface a warning, do not silently create twice.
        var dup = await db.Vendors.AsNoTracking().FirstOrDefaultAsync(v =>
            (req.RegistrationNo != "" && v.RegistrationNo == req.RegistrationNo) || v.Name == req.Name, ct);
        var warning = dup is null ? null
            : $"A vendor with this {(dup.RegistrationNo == req.RegistrationNo ? "registration number" : "name")} already exists ({dup.Code}).";

        var type = ParseType(req.Type);
        var v = new Vendor
        {
            Code = await codes.NextAsync("SWK-V", ct),
            Name = req.Name,
            RegisteredName = string.IsNullOrWhiteSpace(req.RegisteredName) ? req.Name : req.RegisteredName!,
            RegistrationNo = string.IsNullOrWhiteSpace(req.RegistrationNo) ? "—" : req.RegistrationNo,
            TaxId = string.IsNullOrWhiteSpace(req.TaxId) ? "—" : req.TaxId!,
            Type = type,
            // Conformed dimensions stored as seeded lookup CODES (§4).
            Country = string.IsNullOrWhiteSpace(req.Country) ? "MY" : req.Country!,
            Region = req.Region ?? "", State = req.State ?? "", City = req.City ?? "",
            PaymentTerms = string.IsNullOrWhiteSpace(req.PaymentTerms) ? "NET30" : req.PaymentTerms!,
            Categories = req.Categories is null ? [] : [.. req.Categories],
            CreatedUtc = now, UpdatedUtc = now,
            Currencies = [new VendorCurrency { Code = string.IsNullOrWhiteSpace(req.Currency) ? "MYR" : req.Currency!, IsPrimary = true }],
        };
        v.Register();   // manual entry is registered straight away (C1) — no approval
        if (!string.IsNullOrWhiteSpace(req.Bank))
            v.BankAccounts.Add(new VendorBankAccount { Bank = req.Bank!, AccountNo = req.AccountNo ?? "", Swift = req.Swift ?? "", IsPrimary = true });
        if (!string.IsNullOrWhiteSpace(req.ContactName) || !string.IsNullOrWhiteSpace(req.ContactEmail))
            v.Contacts.Add(new VendorContact { Name = req.ContactName ?? req.Name, Email = req.ContactEmail ?? "", IsPrimary = true });
        if (!string.IsNullOrWhiteSpace(req.AddressLine) || !string.IsNullOrWhiteSpace(req.City))
            v.Addresses.Add(new VendorAddress { Type = "Registered", Line = req.AddressLine ?? "", City = req.City ?? "", State = req.State ?? "", Country = string.IsNullOrWhiteSpace(req.Country) ? "MY" : req.Country!, IsPrimary = true });
        db.Vendors.Add(v);
        await db.SaveChangesAsync(ct);
        // WORKFLOW-SEAM: manual entry runs a 0-step review; a future configurable engine can insert steps.
        await audit.WriteAsync("Vendor", v.Code, "Vendor created (manual entry)", after: v.Name, ct: ct);
        return new ManualVendorResult(Map(v), warning);
    }

    public async Task<VendorDetail> UpdateAsync(Guid id, UpdateVendorRequest req, CancellationToken ct = default)
    {
        var v = await Load(id, ct);
        v.Name = req.Name;
        v.RegisteredName = req.RegisteredName;
        v.RegistrationNo = req.RegistrationNo;
        v.TaxId = req.TaxId;
        v.Type = ParseType(req.Type);
        v.LlrcTier = req.LlrcTier;
        v.Region = req.Region;
        v.State = req.State;
        v.City = req.City;
        v.PaymentTerms = req.PaymentTerms;
        v.CreditLimit = req.CreditLimit;
        v.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Vendor", v.Code, "Updated", after: v.Name, ct: ct);
        return Map(v);
    }

    public async Task<VendorDetail> SetCategoriesAsync(Guid id, SetCategoriesRequest req, CancellationToken ct = default)
    {
        var v = await Load(id, ct);
        var before = string.Join(", ", v.Categories);
        v.Categories = req.Categories.Distinct().ToList();
        v.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Vendor", v.Code, "Categories updated",
            before: before, after: string.Join(", ", v.Categories), ct: ct);
        return Map(v);
    }

    public async Task<VendorDetail> ToggleStatusAsync(Guid id, CancellationToken ct = default)
    {
        var v = await Load(id, ct);
        var before = v.Status.ToString();
        v.ToggleActive();
        v.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Vendor", v.Code, "Status changed", before: before, after: v.Status.ToString(), ct: ct);
        return Map(v);
    }

    private async Task<Vendor> Load(Guid id, CancellationToken ct) =>
        await db.Vendors.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException($"Vendor {id} not found.");

    private VendorDetail Map(Vendor v)
    {
        var showBank = CanSeeBankDetails;
        return new(
            v.Id, v.Code, v.Name, v.RegisteredName, v.RegistrationNo, v.TaxId,
            TypeDisplay(v.Type), v.LlrcTier, v.Status.ToString(), v.Region, v.State, v.City, v.Country,
            v.Rating, v.PaymentTerms, v.CreditLimit, v.Categories,
            new PerformanceDto(v.Performance.Otd, v.Performance.Quality, v.Performance.Breaches,
                v.Performance.Lead, v.Performance.Response, v.Performance.WinRate, v.Performance.SpendYtd, v.Performance.Pos),
            v.Contacts.Select(c => new ContactDto(c.Name, c.Role, c.Email, c.Phone, c.IsPrimary)).ToList(),
            v.Addresses.Select(a => new AddressDto(a.Type, a.Line, a.City, a.State, a.Country, a.Postcode, a.IsPrimary)).ToList(),
            v.BankAccounts.Select(a => new BankAccountDto(
                a.Bank,
                showBank ? a.AccountNo : MaskTail(a.AccountNo),
                showBank ? a.Swift : MaskTail(a.Swift),
                a.Currency, a.IsPrimary)).ToList(),
            v.Certifications.Select(c => new CertificationDto(c.Name, c.Number, c.ValidTo, c.Status)).ToList(),
            v.Currencies.Select(c => new CurrencyDto(c.Code, c.IsPrimary)).ToList(),
            v.CreatedUtc, v.UpdatedUtc);
    }
}
