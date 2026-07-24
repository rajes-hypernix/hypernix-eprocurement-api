using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

/// <summary>
/// A custom field definition. <see cref="Code"/>, <see cref="DataType"/>, <see cref="RefEntity"/>
/// and <see cref="ListKey"/> are immutable after creation — changing type under existing values
/// would corrupt them. Authored once, then applied to one or more <see cref="PlatformRecordType"/>s
/// via <see cref="CustomFieldDefApplication"/> — the same field definition can back a column on
/// Requisition, PurchaseOrder and Vendor at once.
/// </summary>
public sealed class CustomFieldDef : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<CustomFieldDefApplication> _applications = [];

    public string Code { get; private set; } = default!;
    public string Label { get; private set; } = default!;
    public CustomFieldDataType DataType { get; private set; }
    public CustomFieldRefEntity? RefEntity { get; private set; }

    /// <summary>Only set when <see cref="DataType"/> is ListValue — the <c>CustomList.Key</c> it draws values from.</summary>
    public string? ListKey { get; private set; }

    public CustomFieldScope Scope { get; private set; }
    public CustomFieldDisplayType DisplayType { get; private set; }
    public bool ShowInList { get; private set; }
    public bool IsRequired { get; private set; }
    public string? HelpText { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public IReadOnlyList<CustomFieldDefApplication> Applications => _applications;

    private CustomFieldDef() { }

    public static CustomFieldDef Create(
        string code,
        string label,
        CustomFieldDataType dataType,
        CustomFieldScope scope,
        CustomFieldRefEntity? refEntity = null,
        string? listKey = null,
        CustomFieldDisplayType displayType = CustomFieldDisplayType.Normal,
        bool showInList = false,
        bool isRequired = false,
        string? helpText = null,
        string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ValidateTypeShape(dataType, refEntity, listKey);

        return new CustomFieldDef
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            Label = label.Trim(),
            DataType = dataType,
            RefEntity = dataType == CustomFieldDataType.RecordRef ? refEntity : null,
            ListKey = dataType == CustomFieldDataType.ListValue ? listKey?.Trim() : null,
            Scope = scope,
            DisplayType = displayType,
            ShowInList = showInList,
            IsRequired = isRequired,
            HelpText = helpText,
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
    }

    public void UpdateDetails(
        string label, CustomFieldDisplayType displayType, bool showInList, bool isRequired,
        string? helpText, string? modifiedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Label = label.Trim();
        DisplayType = displayType;
        ShowInList = showInList;
        IsRequired = isRequired;
        HelpText = helpText;
        Touch(modifiedBy);
    }

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        Touch(modifiedBy);
    }

    public CustomFieldDefApplication ApplyTo(PlatformRecordType recordType, string? modifiedBy = null)
    {
        if (_applications.Any(a => a.RecordType == recordType))
        {
            throw new PlatformRuleException($"{Code} already applies to {recordType}.");
        }

        var application = CustomFieldDefApplication.Create(Id, recordType);
        _applications.Add(application);
        Touch(modifiedBy);
        return application;
    }

    public void RemoveApplication(PlatformRecordType recordType, string? modifiedBy = null)
    {
        var application = _applications.FirstOrDefault(a => a.RecordType == recordType)
            ?? throw new PlatformRuleException($"{Code} does not apply to {recordType}.");
        _applications.Remove(application);
        Touch(modifiedBy);
    }

    private static void ValidateTypeShape(CustomFieldDataType dataType, CustomFieldRefEntity? refEntity, string? listKey)
    {
        if (dataType == CustomFieldDataType.RecordRef && refEntity is null)
        {
            throw new PlatformRuleException("A RecordRef field requires a reference entity.");
        }

        if (dataType == CustomFieldDataType.ListValue && string.IsNullOrWhiteSpace(listKey))
        {
            throw new PlatformRuleException("A ListValue field requires a custom list key.");
        }
    }

    private void Touch(string? modifiedBy)
    {
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}

/// <summary>One row per (field def, record type) it applies to — a field is authored once and can back multiple record types.</summary>
public sealed class CustomFieldDefApplication
{
    public Guid Id { get; private set; }
    public Guid CustomFieldDefId { get; private set; }
    public PlatformRecordType RecordType { get; private set; }

    private CustomFieldDefApplication() { }

    internal static CustomFieldDefApplication Create(Guid customFieldDefId, PlatformRecordType recordType) => new()
    {
        Id = Guid.CreateVersion7(),
        CustomFieldDefId = customFieldDefId,
        RecordType = recordType,
    };
}
