using Microsoft.EntityFrameworkCore.Migrations;
using eProcure.Infrastructure.Persistence;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandardFormsSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CF-FIX4-T2: standard forms for PO / GRN / Invoice — EntryFormSeed is THE single
            // source (migration + tests read the same rows); idempotent per the D4 rule.
            // Each form ships its L3 Header group (IsHeader=true) even when it has no fields.
            foreach (var (type, code, name, rows) in Application.Forms.EntryFormSeed.StandardForms)
            {
                var (def, subtabs, groups, fields) = Application.Forms.EntryFormSeed.ToStandardFormEntities(type, code, name, rows);
                migrationBuilder.Sql(SeedSql.InsertDoNothing(
                    "EntryFormDefs",
                    ["Id", "Code", "Name", "RecordType", "IsSystem", "Active", "CreatedUtc", "UpdatedUtc"],
                    [def.Id, def.Code, def.Name, type.ToString(), true, true, def.CreatedUtc, def.UpdatedUtc]));
                foreach (var g in groups)
                    migrationBuilder.Sql(SeedSql.InsertDoNothing(
                        "EntryFormGroups",
                        ["Id", "FormDefId", "SubtabId", "Title", "Sort", "ColumnBreak", "IsHeader"],
                        [g.Id, g.FormDefId, g.SubtabId, g.Title, g.Sort, g.ColumnBreak, g.IsHeader]));
                foreach (var fld in fields)
                    migrationBuilder.Sql(SeedSql.InsertDoNothing(
                        "EntryFormFields",
                        ["Id", "FormDefId", "FieldKey", "GroupId", "ColumnBreak", "Sort", "DisplayType",
                         "RequiredOnForm", "DefaultValue", "SourceFieldKey", "FullWidth", "Label", "Placeholder"],
                        [fld.Id, fld.FormDefId, fld.FieldKey, fld.GroupId, false, fld.Sort, "Normal",
                         false, null, null, fld.FullWidth, fld.Label, fld.Placeholder]));
            }
            // GRN numbering: the SAME format the string-prefix mint produced (GRN-YEAR-D4),
            // now scheme-driven like the other record types.
            var grnScheme = Application.Forms.EntryFormSeed.Schemes.Single(s => s.RecordType == Domain.Views.RecordType.Grn);
            migrationBuilder.Sql(SeedSql.InsertDoNothing(
                "NumberingSchemes",
                ["Id", "RecordType", "Prefix", "YearSegment", "Digits", "UpdatedUtc"],
                [Application.Forms.EntryFormSeed.SchemeId(Domain.Views.RecordType.Grn), "Grn",
                 grnScheme.Prefix, grnScheme.YearSegment, grnScheme.Digits, new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc)]));

            // The Grn record type is new — its native registry rows ride the same seed.
            foreach (var row in Application.Views.FieldRegistrySeed.Rows.Where(r => r.RecordType == Domain.Views.RecordType.Grn))
                migrationBuilder.Sql(SeedSql.InsertDoNothing(
                    "FieldRegistry",
                    ["Id", "RecordType", "FieldKey", "Kind", "Label", "DataType", "CustomFieldDefId", "SegmentDefId"],
                    [Application.Views.FieldRegistrySeed.StableId(row.RecordType, row.FieldKey), row.RecordType.ToString(),
                     row.FieldKey, "Native", row.Label, row.DataType.ToString(), null, null]));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
