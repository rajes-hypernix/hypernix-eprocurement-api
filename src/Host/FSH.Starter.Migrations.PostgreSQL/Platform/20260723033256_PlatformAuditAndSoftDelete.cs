using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Platform
{
    /// <inheritdoc />
    public partial class PlatformAuditAndSoftDelete : Migration
    {
        private static readonly string[] AuditedWithLegacyStamps =
        [
            "Countries",
            "States",
            "Cities",
            "Banks",
            "CustomLists",
            "CustomListItems",
            "OrgUnits",
            "FormTemplates",
            "SavedViews",
        ];

        private static readonly string[] SoftDeleteTables =
        [
            "Countries",
            "States",
            "Cities",
            "Banks",
        ];

        private static readonly string[] AllPlatformTables =
        [
            "Countries",
            "States",
            "Cities",
            "Banks",
            "CustomLists",
            "CustomListItems",
            "OrgUnits",
            "FormTemplates",
            "FormTemplateQuestions",
            "FieldRegistryEntries",
            "SavedViews",
            "SavedViewFilters",
            "SavedViewColumns",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in AuditedWithLegacyStamps)
            {
                migrationBuilder.RenameColumn(
                    name: "CreatedUtc",
                    schema: "platform",
                    table: table,
                    newName: "CreatedOnUtc");

                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "LastModifiedOnUtc",
                    schema: "platform",
                    table: table,
                    type: "timestamp with time zone",
                    nullable: true);

                migrationBuilder.Sql(
                    $"""
                    UPDATE platform."{table}"
                    SET "LastModifiedOnUtc" = "UpdatedUtc"
                    WHERE "UpdatedUtc" IS NOT NULL;
                    """);

                migrationBuilder.DropColumn(
                    name: "UpdatedUtc",
                    schema: "platform",
                    table: table);

                migrationBuilder.AddColumn<string>(
                    name: "CreatedBy",
                    schema: "platform",
                    table: table,
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true);

                migrationBuilder.AddColumn<string>(
                    name: "LastModifiedBy",
                    schema: "platform",
                    table: table,
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true);
            }

            foreach (var table in SoftDeleteTables)
            {
                migrationBuilder.AddColumn<bool>(
                    name: "IsDeleted",
                    schema: "platform",
                    table: table,
                    type: "boolean",
                    nullable: false,
                    defaultValue: false);

                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "DeletedOnUtc",
                    schema: "platform",
                    table: table,
                    type: "timestamp with time zone",
                    nullable: true);

                migrationBuilder.AddColumn<string>(
                    name: "DeletedBy",
                    schema: "platform",
                    table: table,
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true);

                migrationBuilder.CreateIndex(
                    name: $"IX_{table}_IsDeleted",
                    schema: "platform",
                    table: table,
                    column: "IsDeleted");
            }

            AddNewAuditColumns(migrationBuilder, "FormTemplateQuestions");
            AddNewAuditColumns(migrationBuilder, "FieldRegistryEntries");

            migrationBuilder.Sql(
                """
                UPDATE platform."FormTemplateQuestions"
                SET "CreatedOnUtc" = NOW() AT TIME ZONE 'utc'
                WHERE "CreatedOnUtc" = TIMESTAMPTZ '-infinity'
                   OR "CreatedOnUtc" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                """);

            migrationBuilder.Sql(
                """
                UPDATE platform."FieldRegistryEntries"
                SET "CreatedOnUtc" = NOW() AT TIME ZONE 'utc'
                WHERE "CreatedOnUtc" = TIMESTAMPTZ '-infinity'
                   OR "CreatedOnUtc" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                """);

            foreach (var table in AllPlatformTables)
            {
                migrationBuilder.Sql(
                    $"""
                    UPDATE platform."{table}"
                    SET "TenantId" = 'root'
                    WHERE "TenantId" IS NULL OR BTRIM("TenantId") = '';
                    """);
            }
        }

        private static void AddNewAuditColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOnUtc",
                schema: "platform",
                table: table,
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "platform",
                table: table,
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOnUtc",
                schema: "platform",
                table: table,
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "platform",
                table: table,
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in SoftDeleteTables)
            {
                migrationBuilder.DropIndex(
                    name: $"IX_{table}_IsDeleted",
                    schema: "platform",
                    table: table);

                migrationBuilder.DropColumn(name: "DeletedBy", schema: "platform", table: table);
                migrationBuilder.DropColumn(name: "DeletedOnUtc", schema: "platform", table: table);
                migrationBuilder.DropColumn(name: "IsDeleted", schema: "platform", table: table);
            }

            migrationBuilder.DropColumn(name: "CreatedBy", schema: "platform", table: "FormTemplateQuestions");
            migrationBuilder.DropColumn(name: "CreatedOnUtc", schema: "platform", table: "FormTemplateQuestions");
            migrationBuilder.DropColumn(name: "LastModifiedBy", schema: "platform", table: "FormTemplateQuestions");
            migrationBuilder.DropColumn(name: "LastModifiedOnUtc", schema: "platform", table: "FormTemplateQuestions");

            migrationBuilder.DropColumn(name: "CreatedBy", schema: "platform", table: "FieldRegistryEntries");
            migrationBuilder.DropColumn(name: "CreatedOnUtc", schema: "platform", table: "FieldRegistryEntries");
            migrationBuilder.DropColumn(name: "LastModifiedBy", schema: "platform", table: "FieldRegistryEntries");
            migrationBuilder.DropColumn(name: "LastModifiedOnUtc", schema: "platform", table: "FieldRegistryEntries");

            foreach (var table in AuditedWithLegacyStamps)
            {
                migrationBuilder.DropColumn(name: "CreatedBy", schema: "platform", table: table);
                migrationBuilder.DropColumn(name: "LastModifiedBy", schema: "platform", table: table);

                migrationBuilder.AddColumn<DateTime>(
                    name: "UpdatedUtc",
                    schema: "platform",
                    table: table,
                    type: "timestamp with time zone",
                    nullable: false,
                    defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

                migrationBuilder.Sql(
                    $"""
                    UPDATE platform."{table}"
                    SET "UpdatedUtc" = COALESCE("LastModifiedOnUtc", "CreatedOnUtc");
                    """);

                migrationBuilder.DropColumn(name: "LastModifiedOnUtc", schema: "platform", table: table);

                migrationBuilder.RenameColumn(
                    name: "CreatedOnUtc",
                    schema: "platform",
                    table: table,
                    newName: "CreatedUtc");
            }
        }
    }
}
