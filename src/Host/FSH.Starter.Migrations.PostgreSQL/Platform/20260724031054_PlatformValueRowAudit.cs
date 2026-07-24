using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Platform
{
    /// <inheritdoc />
    public partial class PlatformValueRowAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CustomFieldValues / SegmentAssignments: preserve UpdatedUtc as CreatedOnUtc,
            // then seed LastModifiedOnUtc from the same stamp.
            migrationBuilder.RenameColumn(
                name: "UpdatedUtc",
                schema: "platform",
                table: "CustomFieldValues",
                newName: "CreatedOnUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedUtc",
                schema: "platform",
                table: "SegmentAssignments",
                newName: "CreatedOnUtc");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "platform",
                table: "CustomFieldValues",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "platform",
                table: "CustomFieldValues",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOnUtc",
                schema: "platform",
                table: "CustomFieldValues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "platform",
                table: "SegmentAssignments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "platform",
                table: "SegmentAssignments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOnUtc",
                schema: "platform",
                table: "SegmentAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE platform."CustomFieldValues"
                SET "LastModifiedOnUtc" = "CreatedOnUtc"
                WHERE "CreatedOnUtc" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE platform."SegmentAssignments"
                SET "LastModifiedOnUtc" = "CreatedOnUtc"
                WHERE "CreatedOnUtc" IS NOT NULL;
                """);

            // NumberSequences had no stamp — add full FSH audit columns.
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "platform",
                table: "NumberSequences",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOnUtc",
                schema: "platform",
                table: "NumberSequences",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "TIMESTAMPTZ 'now'");

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "platform",
                table: "NumberSequences",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOnUtc",
                schema: "platform",
                table: "NumberSequences",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "platform",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "platform",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "LastModifiedOnUtc",
                schema: "platform",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "platform",
                table: "SegmentAssignments");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "platform",
                table: "SegmentAssignments");

            migrationBuilder.DropColumn(
                name: "LastModifiedOnUtc",
                schema: "platform",
                table: "SegmentAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "platform",
                table: "NumberSequences");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                schema: "platform",
                table: "NumberSequences");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                schema: "platform",
                table: "NumberSequences");

            migrationBuilder.DropColumn(
                name: "LastModifiedOnUtc",
                schema: "platform",
                table: "NumberSequences");

            migrationBuilder.RenameColumn(
                name: "CreatedOnUtc",
                schema: "platform",
                table: "CustomFieldValues",
                newName: "UpdatedUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedOnUtc",
                schema: "platform",
                table: "SegmentAssignments",
                newName: "UpdatedUtc");
        }
    }
}
