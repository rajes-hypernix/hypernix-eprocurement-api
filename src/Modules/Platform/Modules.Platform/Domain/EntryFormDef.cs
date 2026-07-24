using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

/// <summary>Input shape for (re)building a form's layout — kept in the Domain namespace so
/// <see cref="EntryFormDef"/> doesn't need to depend on the Contracts DTOs (mirrors <c>SavedView</c>'s
/// own <c>SavedViewFilterInput</c>/<c>SavedViewColumnInput</c> pattern).</summary>
public sealed record EntryFormGroupInput(string Title, int Sort);

/// <summary><paramref name="GroupIndex"/> indexes into the groups list submitted in the same
/// <see cref="EntryFormDef.ReplaceLayout"/> call — groups are always freshly (re)created on
/// replace, so a field can't reference one by a not-yet-minted Guid.</summary>
public sealed record EntryFormFieldInput(string FieldKey, int GroupIndex, int Sort, bool RequiredOnForm, bool FullWidth);

/// <summary>
/// A form layout for one <see cref="PlatformRecordType"/> — a flat list of <see cref="EntryFormGroup"/>
/// sections, each holding one or more <see cref="EntryFormField"/> placements. No subtabs in this
/// slice (the old source's Subtab object is dropped — a simplification, not a data-model gap: every
/// field still belongs to a named group). Exactly one <see cref="IsSystem"/> form should exist per
/// record type — the fallback every role resolves to when <see cref="EntryFormRoleMap"/> has no
/// row for its (RecordType, Role).
/// </summary>
public sealed class EntryFormDef : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<EntryFormGroup> _groups = [];
    private readonly List<EntryFormField> _fields = [];

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public PlatformRecordType RecordType { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public IReadOnlyList<EntryFormGroup> Groups => _groups;
    public IReadOnlyList<EntryFormField> Fields => _fields;

    private EntryFormDef() { }

    public static EntryFormDef Create(string code, string name, PlatformRecordType recordType, bool isSystem = false, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new EntryFormDef
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            Name = name.Trim(),
            RecordType = recordType,
            IsSystem = isSystem,
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    public void UpdateDetails(string name, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Touch(modifiedBy);
    }

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        Touch(modifiedBy);
    }

    /// <summary>
    /// Replaces the entire groups+fields layout in one call — the buyer's admin screen sends the
    /// whole form back on every save, same pattern as <c>SavedView.UpdateDefinition</c> replacing
    /// its filters/columns, rather than many fine-grained add/remove/reorder endpoints.
    /// </summary>
    public void ReplaceLayout(IReadOnlyList<EntryFormGroupInput> groups, IReadOnlyList<EntryFormFieldInput> fields, string? modifiedBy = null)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(fields);

        var newGroups = groups.Select(g => EntryFormGroup.Create(Id, g.Title, g.Sort)).ToList();

        var duplicateKeys = fields.GroupBy(f => f.FieldKey, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicateKeys is not null)
        {
            throw new PlatformRuleException($"Field {duplicateKeys.Key} is placed more than once on this form.");
        }

        foreach (var field in fields)
        {
            if (field.GroupIndex < 0 || field.GroupIndex >= newGroups.Count)
            {
                throw new PlatformRuleException($"Field {field.FieldKey} references a group that isn't part of this layout.");
            }
        }

        _groups.Clear();
        _groups.AddRange(newGroups);
        _fields.Clear();
        _fields.AddRange(fields.Select(f => EntryFormField.Create(Id, newGroups[f.GroupIndex].Id, f.FieldKey.Trim(), f.Sort, f.RequiredOnForm, f.FullWidth)));
        Touch(modifiedBy);
    }

    private void Touch(string? modifiedBy)
    {
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}

/// <summary>A named section on a form body. Grain: one row per group.</summary>
public sealed class EntryFormGroup
{
    public Guid Id { get; private set; }
    public Guid EntryFormDefId { get; private set; }
    public string Title { get; private set; } = default!;
    public int Sort { get; private set; }

    private EntryFormGroup() { }

    internal static EntryFormGroup Create(Guid entryFormDefId, string title, int sort) => new()
    {
        Id = Guid.CreateVersion7(),
        EntryFormDefId = entryFormDefId,
        Title = title.Trim(),
        Sort = sort,
    };
}

/// <summary>
/// One field's placement on a form: either a native property key (the record type's own vocabulary,
/// e.g. "Memo") or a <see cref="CustomFieldDef.Code"/> — both live in the same flat <see cref="FieldKey"/>
/// string, service-validated for liveness rather than FK'd (mirrors the old source's own approach,
/// and the Saved Views <c>FieldRegistryEntry.FieldKey</c> convention already used elsewhere).
/// </summary>
public sealed class EntryFormField
{
    public Guid Id { get; private set; }
    public Guid EntryFormDefId { get; private set; }
    public Guid GroupId { get; private set; }
    public string FieldKey { get; private set; } = default!;
    public int Sort { get; private set; }
    public bool RequiredOnForm { get; private set; }
    public bool FullWidth { get; private set; }

    private EntryFormField() { }

    internal static EntryFormField Create(Guid entryFormDefId, Guid groupId, string fieldKey, int sort, bool requiredOnForm, bool fullWidth) => new()
    {
        Id = Guid.CreateVersion7(),
        EntryFormDefId = entryFormDefId,
        GroupId = groupId,
        FieldKey = fieldKey,
        Sort = sort,
        RequiredOnForm = requiredOnForm,
        FullWidth = fullWidth,
    };
}
