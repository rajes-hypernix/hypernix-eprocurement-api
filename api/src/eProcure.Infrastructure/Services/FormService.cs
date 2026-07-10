using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Sourcing;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class FormService(AppDbContext db, IClock clock, ICodeGenerator codes) : IFormService
{
    public async Task<IReadOnlyList<FormTemplateDto>> ListAsync(CancellationToken ct = default)
    {
        var forms = await db.FormTemplates.AsNoTracking().OrderBy(f => f.Name).ToListAsync(ct);
        return forms.Select(SourcingMapping.ToDto).ToList();
    }

    public async Task<FormTemplateDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var f = await db.FormTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return f is null ? null : SourcingMapping.ToDto(f);
    }

    public async Task<FormTemplateDto> CreateAsync(SaveFormTemplateRequest req, CancellationToken ct = default)
    {
        RfqService.ValidateForm(req.Items);
        var now = clock.UtcNow;
        var f = new FormTemplate
        {
            Code = await codes.NextAsync("FORM", ct),
            Name = req.Name,
            Purpose = ParsePurpose(req.Purpose),
            Version = 1,
            Items = req.Items.Select((d, i) => SourcingMapping.ToEntity(d, i)).ToList(),
            TechnicalSections = req.TechnicalSections.ToList(),
            CommercialSections = req.CommercialSections.ToList(),
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        db.FormTemplates.Add(f);
        await db.SaveChangesAsync(ct);
        return SourcingMapping.ToDto(f);
    }

    public async Task<FormTemplateDto> UpdateAsync(Guid id, SaveFormTemplateRequest req, CancellationToken ct = default)
    {
        RfqService.ValidateForm(req.Items);
        var f = await db.FormTemplates.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Form {id} not found.");
        f.Name = req.Name;
        f.Purpose = ParsePurpose(req.Purpose);
        f.Version += 1;
        f.Items = req.Items.Select((d, i) => SourcingMapping.ToEntity(d, i)).ToList();
        f.TechnicalSections = req.TechnicalSections.ToList();
        f.CommercialSections = req.CommercialSections.ToList();
        f.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return SourcingMapping.ToDto(f);
    }

    // Existing RFQ forms (no purpose sent) default to Rfq — keeps current behaviour (SPEC §5).
    private static FormPurpose ParsePurpose(string? purpose) =>
        Enum.TryParse<FormPurpose>(purpose, ignoreCase: true, out var p) ? p : FormPurpose.Rfq;

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var f = await db.FormTemplates.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Form {id} not found.");
        db.FormTemplates.Remove(f);
        await db.SaveChangesAsync(ct);
    }
}
