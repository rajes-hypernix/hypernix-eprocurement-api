using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Suppliers
{
    /// <inheritdoc />
    public partial class SuppliersVendorGeoAndEnums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Vendors / owned collections: rename free-text columns ---
            migrationBuilder.RenameColumn(
                name: "Country",
                schema: "suppliers",
                table: "Vendors",
                newName: "CountryCode");

            migrationBuilder.RenameColumn(
                name: "Country",
                schema: "suppliers",
                table: "VendorAddresses",
                newName: "CountryCode");

            migrationBuilder.RenameColumn(
                name: "Bank",
                schema: "suppliers",
                table: "VendorBankAccounts",
                newName: "BankName");

            migrationBuilder.RenameColumn(
                name: "Currency",
                schema: "suppliers",
                table: "VendorBankAccounts",
                newName: "CurrencyCode");

            // --- Onboarding application: rename Country → CountryCode (preserve values) ---
            migrationBuilder.RenameColumn(
                name: "Country",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                newName: "CountryCode");

            migrationBuilder.RenameColumn(
                name: "Country",
                schema: "suppliers",
                table: "OnboardingAddresses",
                newName: "CountryCode");

            migrationBuilder.RenameColumn(
                name: "Bank",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                newName: "BankName");

            migrationBuilder.RenameColumn(
                name: "Currency",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                newName: "CurrencyCode");

            // --- New geo / bank FK columns ---
            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                schema: "suppliers",
                table: "Vendors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StateId",
                schema: "suppliers",
                table: "Vendors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StateId",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankId",
                schema: "suppliers",
                table: "VendorBankAccounts",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                schema: "suppliers",
                table: "VendorAddresses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StateId",
                schema: "suppliers",
                table: "VendorAddresses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankId",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StateId",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "uuid",
                nullable: true);

            // --- Backfill before shrinking CountryCode / normalizing enums ---
            migrationBuilder.Sql(
                """
                -- Country free-text → ISO-2 (best-effort; default MY)
                UPDATE suppliers."Vendors"
                SET "CountryCode" = CASE
                    WHEN length(trim("CountryCode")) = 2 THEN upper(trim("CountryCode"))
                    WHEN lower(trim("CountryCode")) IN ('malaysia', 'my') THEN 'MY'
                    WHEN lower(trim("CountryCode")) IN ('singapore', 'sg') THEN 'SG'
                    WHEN coalesce(trim("CountryCode"), '') = '' THEN 'MY'
                    ELSE left(upper(trim("CountryCode")), 2)
                END;

                UPDATE suppliers."VendorAddresses"
                SET "CountryCode" = CASE
                    WHEN length(trim("CountryCode")) = 2 THEN upper(trim("CountryCode"))
                    WHEN lower(trim("CountryCode")) IN ('malaysia', 'my') THEN 'MY'
                    WHEN lower(trim("CountryCode")) IN ('singapore', 'sg') THEN 'SG'
                    WHEN coalesce(trim("CountryCode"), '') = '' THEN 'MY'
                    ELSE left(upper(trim("CountryCode")), 2)
                END;

                UPDATE suppliers."VendorOnboardingApplications"
                SET "CountryCode" = CASE
                    WHEN length(trim("CountryCode")) = 2 THEN upper(trim("CountryCode"))
                    WHEN lower(trim("CountryCode")) IN ('malaysia', 'my') THEN 'MY'
                    WHEN lower(trim("CountryCode")) IN ('singapore', 'sg') THEN 'SG'
                    WHEN coalesce(trim("CountryCode"), '') = '' THEN 'MY'
                    ELSE left(upper(trim("CountryCode")), 2)
                END;

                UPDATE suppliers."OnboardingAddresses"
                SET "CountryCode" = CASE
                    WHEN length(trim("CountryCode")) = 2 THEN upper(trim("CountryCode"))
                    WHEN lower(trim("CountryCode")) IN ('malaysia', 'my') THEN 'MY'
                    WHEN lower(trim("CountryCode")) IN ('singapore', 'sg') THEN 'SG'
                    WHEN coalesce(trim("CountryCode"), '') = '' THEN 'MY'
                    ELSE left(upper(trim("CountryCode")), 2)
                END;

                -- Currency ISO-3
                UPDATE suppliers."VendorBankAccounts"
                SET "CurrencyCode" = CASE
                    WHEN coalesce(trim("CurrencyCode"), '') = '' THEN 'MYR'
                    ELSE left(upper(trim("CurrencyCode")), 3)
                END;

                UPDATE suppliers."OnboardingBankAccounts"
                SET "CurrencyCode" = CASE
                    WHEN coalesce(trim("CurrencyCode"), '') = '' THEN 'MYR'
                    ELSE left(upper(trim("CurrencyCode")), 3)
                END;

                -- VendorType: Non-SWEC / Non SWEC → NonSwec; SWEC → Swec
                UPDATE suppliers."Vendors"
                SET "Type" = CASE
                    WHEN replace(replace(upper(trim("Type")), '-', ''), ' ', '') IN ('SWEC') THEN 'Swec'
                    ELSE 'NonSwec'
                END;

                UPDATE suppliers."VendorOnboardingApplications"
                SET "Type" = CASE
                    WHEN replace(replace(upper(trim("Type")), '-', ''), ' ', '') IN ('SWEC') THEN 'Swec'
                    ELSE 'NonSwec'
                END;

                UPDATE suppliers."VendorOnboardingInvitations"
                SET "Type" = CASE
                    WHEN replace(replace(upper(trim("Type")), '-', ''), ' ', '') IN ('SWEC') THEN 'Swec'
                    ELSE 'NonSwec'
                END;

                -- Soft-match BankId from platform.Banks by name + tenant.
                -- Postgres UPDATE target alias is only valid in WHERE, not in FROM/JOIN ON.
                UPDATE suppliers."VendorBankAccounts" AS vba
                SET "BankId" = b."Id"
                FROM suppliers."Vendors" AS v
                INNER JOIN platform."Banks" AS b ON b."TenantId" = v."TenantId"
                WHERE v."Id" = vba."VendorId"
                  AND lower(trim(b."Name")) = lower(trim(vba."BankName"))
                  AND vba."BankId" = '00000000-0000-0000-0000-000000000000';

                UPDATE suppliers."OnboardingBankAccounts" AS oba
                SET "BankId" = b."Id"
                FROM suppliers."VendorOnboardingApplications" AS a
                INNER JOIN platform."Banks" AS b ON b."TenantId" = a."TenantId"
                WHERE a."Id" = oba."ApplicationId"
                  AND lower(trim(b."Name")) = lower(trim(oba."BankName"))
                  AND oba."BankId" = '00000000-0000-0000-0000-000000000000';
                """);

            // Shrink application/onboarding CountryCode to varchar(2); tighten owned column lengths
            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ValidTo",
                schema: "suppliers",
                table: "OnboardingCertifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "suppliers",
                table: "OnboardingCertifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                schema: "suppliers",
                table: "OnboardingCertifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "suppliers",
                table: "OnboardingCertifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Swift",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AccountNo",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "BankName",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "State",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Postcode",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Line",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CityId",
                schema: "suppliers",
                table: "Vendors",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CountryCode",
                schema: "suppliers",
                table: "Vendors",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_CityId",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_CountryCode",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_VendorBankAccounts_BankId",
                schema: "suppliers",
                table: "VendorBankAccounts",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorAddresses_CityId",
                schema: "suppliers",
                table: "VendorAddresses",
                column: "CityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vendors_CityId",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropIndex(
                name: "IX_Vendors_CountryCode",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropIndex(
                name: "IX_VendorOnboardingApplications_CityId",
                schema: "suppliers",
                table: "VendorOnboardingApplications");

            migrationBuilder.DropIndex(
                name: "IX_VendorOnboardingApplications_CountryCode",
                schema: "suppliers",
                table: "VendorOnboardingApplications");

            migrationBuilder.DropIndex(
                name: "IX_VendorBankAccounts_BankId",
                schema: "suppliers",
                table: "VendorBankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_VendorAddresses_CityId",
                schema: "suppliers",
                table: "VendorAddresses");

            migrationBuilder.DropColumn(
                name: "CityId",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "StateId",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "CityId",
                schema: "suppliers",
                table: "VendorOnboardingApplications");

            migrationBuilder.DropColumn(
                name: "StateId",
                schema: "suppliers",
                table: "VendorOnboardingApplications");

            migrationBuilder.DropColumn(
                name: "BankId",
                schema: "suppliers",
                table: "VendorBankAccounts");

            migrationBuilder.DropColumn(
                name: "CityId",
                schema: "suppliers",
                table: "VendorAddresses");

            migrationBuilder.DropColumn(
                name: "StateId",
                schema: "suppliers",
                table: "VendorAddresses");

            migrationBuilder.DropColumn(
                name: "BankId",
                schema: "suppliers",
                table: "OnboardingBankAccounts");

            migrationBuilder.DropColumn(
                name: "CityId",
                schema: "suppliers",
                table: "OnboardingAddresses");

            migrationBuilder.DropColumn(
                name: "StateId",
                schema: "suppliers",
                table: "OnboardingAddresses");

            migrationBuilder.RenameColumn(
                name: "CountryCode",
                schema: "suppliers",
                table: "Vendors",
                newName: "Country");

            migrationBuilder.RenameColumn(
                name: "CurrencyCode",
                schema: "suppliers",
                table: "VendorBankAccounts",
                newName: "Currency");

            migrationBuilder.RenameColumn(
                name: "BankName",
                schema: "suppliers",
                table: "VendorBankAccounts",
                newName: "Bank");

            migrationBuilder.RenameColumn(
                name: "CountryCode",
                schema: "suppliers",
                table: "VendorAddresses",
                newName: "Country");

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2);

            migrationBuilder.RenameColumn(
                name: "CountryCode",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                newName: "Country");

            migrationBuilder.AlterColumn<string>(
                name: "ValidTo",
                schema: "suppliers",
                table: "OnboardingCertifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "suppliers",
                table: "OnboardingCertifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                schema: "suppliers",
                table: "OnboardingCertifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "suppliers",
                table: "OnboardingCertifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Swift",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNo",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "BankName",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.RenameColumn(
                name: "BankName",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                newName: "Bank");

            migrationBuilder.RenameColumn(
                name: "CurrencyCode",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                newName: "Currency");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "State",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Postcode",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Line",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CountryCode",
                schema: "suppliers",
                table: "OnboardingAddresses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2);

            migrationBuilder.RenameColumn(
                name: "CountryCode",
                schema: "suppliers",
                table: "OnboardingAddresses",
                newName: "Country");
        }
    }
}
