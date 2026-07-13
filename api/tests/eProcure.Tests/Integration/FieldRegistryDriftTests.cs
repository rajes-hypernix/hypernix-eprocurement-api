using eProcure.Application.Onboarding;
using eProcure.Application.Procurement;
using eProcure.Application.Sourcing;
using eProcure.Application.Suppliers;
using eProcure.Application.Views;
using eProcure.Domain.Views;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests.Integration;

/// <summary>
/// D3 drift-proof: every FieldRegistrySeed row's FieldKey is a real property on its record
/// type's LIST DTO and its DataType matches the property's CLR type. A DTO rename or retype
/// without a registry update — or a registry row pointing at nothing — fails here, so a
/// saved view can never reference a field the run endpoint cannot shape.
/// </summary>
public sealed class FieldRegistryDriftTests
{
    private static readonly IReadOnlyDictionary<RecordType, Type> DtoFor = new Dictionary<RecordType, Type>
    {
        [RecordType.Requisition] = typeof(RequisitionDto),
        [RecordType.Rfq] = typeof(RfqListItem),
        [RecordType.PurchaseOrder] = typeof(PoListItem),
        [RecordType.Invoice] = typeof(InvoiceListDto),
        [RecordType.Asn] = typeof(AsnListDto),
        [RecordType.Vendor] = typeof(VendorListItem),
        [RecordType.Onboarding] = typeof(OnboardingQueueItemDto),
        [RecordType.Grn] = typeof(GrnDetailDto),   // CF-FIX4-T2: no list DTO yet — detail is the contract
    };

    private static bool TypeMatches(FieldDataType dataType, Type clr)
    {
        var t = Nullable.GetUnderlyingType(clr) ?? clr;
        return dataType switch
        {
            FieldDataType.Code or FieldDataType.Text or FieldDataType.Enum => t == typeof(string),
            FieldDataType.Date => t == typeof(DateOnly),
            FieldDataType.Instant => t == typeof(DateTime),
            FieldDataType.Money => t == typeof(decimal),
            FieldDataType.Number => t == typeof(int) || t == typeof(decimal),
            FieldDataType.Bool => t == typeof(bool),
            FieldDataType.Tags => typeof(IEnumerable<string>).IsAssignableFrom(t),
            _ => false,
        };
    }

    [Fact]
    public void Every_registry_row_matches_a_list_dto_property_of_the_declared_type()
    {
        var failures = new List<string>();
        foreach (var row in FieldRegistrySeed.Rows)
        {
            var dto = DtoFor[row.RecordType];
            var prop = dto.GetProperty(row.FieldKey);
            if (prop is null)
                failures.Add($"{row.RecordType}.{row.FieldKey}: no such property on {dto.Name}");
            else if (!TypeMatches(row.DataType, prop.PropertyType))
                failures.Add($"{row.RecordType}.{row.FieldKey}: DataType {row.DataType} does not match {dto.Name}.{prop.Name} ({prop.PropertyType.Name})");
        }
        failures.Should().BeEmpty("the registry and the list DTOs must not drift");
    }

    [Fact]
    public void Registry_keys_are_unique_per_record_type_and_ids_deterministic()
    {
        FieldRegistrySeed.Rows.Select(r => (r.RecordType, r.FieldKey)).Should().OnlyHaveUniqueItems();
        foreach (var row in FieldRegistrySeed.Rows)
            FieldRegistrySeed.StableId(row.RecordType, row.FieldKey)
                .Should().Be(FieldRegistrySeed.StableId(row.RecordType, row.FieldKey));
    }

    [Fact]
    public void Every_enum_registry_field_has_an_options_vocabulary_and_vice_versa()
    {
        var enumFields = FieldRegistrySeed.Rows.Where(r => r.DataType == FieldDataType.Enum)
            .Select(r => (r.RecordType, r.FieldKey)).ToHashSet();
        var optionKeys = ViewVocabulary.EnumOptions.Keys.ToHashSet();
        enumFields.Except(optionKeys).Should().BeEmpty("every Enum field needs builder options");
        optionKeys.Except(enumFields).Should().BeEmpty("no orphan option vocabularies");
    }

    [Fact]
    public void Every_record_type_has_a_view_action_mapping()
    {
        Enum.GetValues<RecordType>().Should().OnlyContain(rt => ViewVocabulary.ViewActionFor.ContainsKey(rt));
    }
}
