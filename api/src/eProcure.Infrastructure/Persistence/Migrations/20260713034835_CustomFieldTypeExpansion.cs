using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomFieldTypeExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomFieldValues_ExactlyOne",
                table: "CustomFieldValues");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomFieldValues_KindMatch",
                table: "CustomFieldValues");

            migrationBuilder.AddColumn<DateTime>(
                name: "ValueDateTime",
                table: "CustomFieldValues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValueLabel",
                table: "CustomFieldValues",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomFieldValues_ExactlyOne",
                table: "CustomFieldValues",
                sql: "(CASE WHEN \"ValueText\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueNumber\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueMoney\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueDate\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueBool\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueListCode\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueDateTime\" IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomFieldValues_KindMatch",
                table: "CustomFieldValues",
                sql: "(\"DataType\" IN ('Text','LongText','Email','Telephone','Hyperlink','Image','Document') AND \"ValueText\" IS NOT NULL) OR (\"DataType\" IN ('Int','Decimal','Percent') AND \"ValueNumber\" IS NOT NULL) OR (\"DataType\" = 'Money' AND \"ValueMoney\" IS NOT NULL) OR (\"DataType\" = 'Date' AND \"ValueDate\" IS NOT NULL) OR (\"DataType\" = 'DateTime' AND \"ValueDateTime\" IS NOT NULL) OR (\"DataType\" = 'Bool' AND \"ValueBool\" IS NOT NULL) OR (\"DataType\" = 'ListValue' AND \"ValueListCode\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomFieldValues_ExactlyOne",
                table: "CustomFieldValues");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomFieldValues_KindMatch",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "ValueDateTime",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "ValueLabel",
                table: "CustomFieldValues");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomFieldValues_ExactlyOne",
                table: "CustomFieldValues",
                sql: "(CASE WHEN \"ValueText\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueNumber\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueMoney\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueDate\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueBool\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueListCode\" IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomFieldValues_KindMatch",
                table: "CustomFieldValues",
                sql: "(\"DataType\" IN ('Text','LongText') AND \"ValueText\" IS NOT NULL) OR (\"DataType\" IN ('Int','Decimal') AND \"ValueNumber\" IS NOT NULL) OR (\"DataType\" = 'Money' AND \"ValueMoney\" IS NOT NULL) OR (\"DataType\" = 'Date' AND \"ValueDate\" IS NOT NULL) OR (\"DataType\" = 'Bool' AND \"ValueBool\" IS NOT NULL) OR (\"DataType\" = 'ListValue' AND \"ValueListCode\" IS NOT NULL)");
        }
    }
}
