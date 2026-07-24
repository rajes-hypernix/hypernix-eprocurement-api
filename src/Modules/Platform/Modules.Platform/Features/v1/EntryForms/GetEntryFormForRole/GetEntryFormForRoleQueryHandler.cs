using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.EntryForms.GetEntryFormForRole;

/// <summary>
/// 2-level resolution: a role-specific mapping if one exists for (RecordType, Role), else the
/// record type's IsSystem default form. Every field placement comes back with its registry
/// metadata folded in (native vocabulary entry, or the backing CustomFieldDef) — the caller
/// shouldn't need a second round-trip to know how to render each field.
/// </summary>
public sealed class GetEntryFormForRoleQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<GetEntryFormForRoleQuery, ResolvedEntryFormDto>
{
    public async ValueTask<ResolvedEntryFormDto> Handle(GetEntryFormForRoleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(query.RecordType, "record type");

        var map = await dbContext.EntryFormRoleMaps
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.RecordType == recordType && m.Role == query.Role, cancellationToken)
            .ConfigureAwait(false);

        var form = map is not null
            ? await dbContext.EntryFormDefs.AsNoTracking().Include(f => f.Groups).Include(f => f.Fields)
                .FirstOrDefaultAsync(f => f.Id == map.EntryFormDefId, cancellationToken).ConfigureAwait(false)
            : await dbContext.EntryFormDefs.AsNoTracking().Include(f => f.Groups).Include(f => f.Fields)
                .FirstOrDefaultAsync(f => f.RecordType == recordType && f.IsSystem, cancellationToken).ConfigureAwait(false);

        if (form is null)
        {
            throw new NotFoundException($"No entry form is configured for {recordType} (role '{query.Role}' has no mapping and no system default exists).");
        }

        var customFieldCodes = form.Fields
            .Where(f => EntryFormVocabulary.Find(recordType, f.FieldKey) is null)
            .Select(f => f.FieldKey)
            .ToList();

        var customDefs = customFieldCodes.Count > 0
            ? await dbContext.CustomFieldDefs.AsNoTracking()
                .Where(d => customFieldCodes.Contains(d.Code))
                .ToDictionaryAsync(d => d.Code, StringComparer.OrdinalIgnoreCase, cancellationToken)
                .ConfigureAwait(false)
            : [];

        var groupsByIndex = form.Groups.OrderBy(g => g.Sort).ToList();
        var fields = new List<ResolvedFormFieldDto>();
        foreach (var field in form.Fields.OrderBy(f => f.Sort))
        {
            var group = groupsByIndex.FirstOrDefault(g => g.Id == field.GroupId);
            var native = EntryFormVocabulary.Find(recordType, field.FieldKey);

            if (native is not null)
            {
                fields.Add(new ResolvedFormFieldDto(
                    field.FieldKey, group?.Title ?? string.Empty, group?.Sort ?? 0, field.Sort,
                    field.RequiredOnForm, field.FullWidth, native.Label, native.DataType,
                    null, null, IsCustomField: false, CustomFieldDefId: null));
            }
            else if (customDefs.TryGetValue(field.FieldKey, out var def))
            {
                fields.Add(new ResolvedFormFieldDto(
                    field.FieldKey, group?.Title ?? string.Empty, group?.Sort ?? 0, field.Sort,
                    field.RequiredOnForm || def.IsRequired, field.FullWidth, def.Label, def.DataType.ToString(),
                    def.ListKey, def.RefEntity?.ToString(), IsCustomField: true, CustomFieldDefId: def.Id));
            }
            // A field placement whose key no longer resolves (native vocabulary changed, or the
            // custom field was deleted) is silently skipped rather than erroring the whole form —
            // the admin screen re-validates on save, so a stale placement can only appear here if
            // something else deleted the field def out from under it.
        }

        return new ResolvedEntryFormDto(form.Id, form.Code, form.Name, fields);
    }
}
