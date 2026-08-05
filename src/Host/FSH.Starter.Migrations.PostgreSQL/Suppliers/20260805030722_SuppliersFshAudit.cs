using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Suppliers
{
    /// <inheritdoc />
    public partial class SuppliersFshAudit : Migration
    {
        private static readonly string[] TablesWithCreatedAndUpdated =
        [
            "Vendors",
            "VendorUsers",
            "VendorOnboardingApplications",
            "VendorFinancialAssessments",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TablesWithCreatedAndUpdated)
            {
                migrationBuilder.RenameColumn(
                    name: "CreatedUtc",
                    schema: "suppliers",
                    table: table,
                    newName: "CreatedOnUtc");

                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "LastModifiedOnUtc",
                    schema: "suppliers",
                    table: table,
                    type: "timestamp with time zone",
                    nullable: true);

                migrationBuilder.Sql(
                    $"""
                    UPDATE suppliers."{table}"
                    SET "LastModifiedOnUtc" = "UpdatedUtc"
                    WHERE "UpdatedUtc" IS NOT NULL;
                    """);

                migrationBuilder.DropColumn(
                    name: "UpdatedUtc",
                    schema: "suppliers",
                    table: table);

                AddActorColumns(migrationBuilder, table);
            }

            migrationBuilder.RenameColumn(
                name: "CreatedUtc",
                schema: "suppliers",
                table: "VendorOnboardingInvitations",
                newName: "CreatedOnUtc");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOnUtc",
                schema: "suppliers",
                table: "VendorOnboardingInvitations",
                type: "timestamp with time zone",
                nullable: true);

            AddActorColumns(migrationBuilder, "VendorOnboardingInvitations");

            migrationBuilder.RenameIndex(
                name: "IX_VendorOnboardingApplications_CreatedUtc",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                newName: "IX_VendorOnboardingApplications_CreatedOnUtc");

            migrationBuilder.RenameIndex(
                name: "IX_VendorOnboardingInvitations_CreatedUtc",
                schema: "suppliers",
                table: "VendorOnboardingInvitations",
                newName: "IX_VendorOnboardingInvitations_CreatedOnUtc");

            AddNewAuditColumns(migrationBuilder, "SwecCategories");
            AddNewAuditColumns(migrationBuilder, "OnboardingClarificationRounds");

            migrationBuilder.Sql(
                """
                UPDATE suppliers."SwecCategories"
                SET "CreatedOnUtc" = NOW() AT TIME ZONE 'utc'
                WHERE "CreatedOnUtc" = TIMESTAMPTZ '-infinity'
                   OR "CreatedOnUtc" = TIMESTAMPTZ '0001-01-01 00:00:00+00';

                UPDATE suppliers."OnboardingClarificationRounds"
                SET "CreatedOnUtc" = COALESCE("RaisedUtc", NOW() AT TIME ZONE 'utc'),
                    "LastModifiedOnUtc" = "RespondedUtc"
                WHERE "CreatedOnUtc" = TIMESTAMPTZ '-infinity'
                   OR "CreatedOnUtc" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreatedBy", schema: "suppliers", table: "SwecCategories");
            migrationBuilder.DropColumn(name: "CreatedOnUtc", schema: "suppliers", table: "SwecCategories");
            migrationBuilder.DropColumn(name: "LastModifiedBy", schema: "suppliers", table: "SwecCategories");
            migrationBuilder.DropColumn(name: "LastModifiedOnUtc", schema: "suppliers", table: "SwecCategories");

            migrationBuilder.DropColumn(name: "CreatedBy", schema: "suppliers", table: "OnboardingClarificationRounds");
            migrationBuilder.DropColumn(name: "CreatedOnUtc", schema: "suppliers", table: "OnboardingClarificationRounds");
            migrationBuilder.DropColumn(name: "LastModifiedBy", schema: "suppliers", table: "OnboardingClarificationRounds");
            migrationBuilder.DropColumn(name: "LastModifiedOnUtc", schema: "suppliers", table: "OnboardingClarificationRounds");

            migrationBuilder.RenameIndex(
                name: "IX_VendorOnboardingApplications_CreatedOnUtc",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                newName: "IX_VendorOnboardingApplications_CreatedUtc");

            migrationBuilder.RenameIndex(
                name: "IX_VendorOnboardingInvitations_CreatedOnUtc",
                schema: "suppliers",
                table: "VendorOnboardingInvitations",
                newName: "IX_VendorOnboardingInvitations_CreatedUtc");

            migrationBuilder.DropColumn(name: "CreatedBy", schema: "suppliers", table: "VendorOnboardingInvitations");
            migrationBuilder.DropColumn(name: "LastModifiedBy", schema: "suppliers", table: "VendorOnboardingInvitations");
            migrationBuilder.DropColumn(name: "LastModifiedOnUtc", schema: "suppliers", table: "VendorOnboardingInvitations");
            migrationBuilder.RenameColumn(
                name: "CreatedOnUtc",
                schema: "suppliers",
                table: "VendorOnboardingInvitations",
                newName: "CreatedUtc");

            foreach (var table in TablesWithCreatedAndUpdated)
            {
                migrationBuilder.DropColumn(name: "CreatedBy", schema: "suppliers", table: table);
                migrationBuilder.DropColumn(name: "LastModifiedBy", schema: "suppliers", table: table);

                migrationBuilder.AddColumn<DateTime>(
                    name: "UpdatedUtc",
                    schema: "suppliers",
                    table: table,
                    type: "timestamp with time zone",
                    nullable: false,
                    defaultValue: new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc));

                migrationBuilder.Sql(
                    $"""
                    UPDATE suppliers."{table}"
                    SET "UpdatedUtc" = COALESCE("LastModifiedOnUtc", "CreatedOnUtc");
                    """);

                migrationBuilder.DropColumn(name: "LastModifiedOnUtc", schema: "suppliers", table: table);
                migrationBuilder.RenameColumn(
                    name: "CreatedOnUtc",
                    schema: "suppliers",
                    table: table,
                    newName: "CreatedUtc");
            }
        }

        private static void AddActorColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "suppliers",
                table: table,
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                schema: "suppliers",
                table: table,
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        private static void AddNewAuditColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOnUtc",
                schema: "suppliers",
                table: table,
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero));

            AddActorColumns(migrationBuilder, table);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedOnUtc",
                schema: "suppliers",
                table: table,
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
