using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Identity
{
    /// <inheritdoc />
    [DbContext(typeof(FSH.Modules.Identity.Data.IdentityDbContext))]
    [Migration("20260807040000_UserAndRoleCreatedOnUtc")]
    public partial class UserAndRoleCreatedOnUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOnUtc",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedOnUtc",
                schema: "identity",
                table: "Roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Existing rows: stamp with migration time so the UI shows a real date (not year 0001).
            migrationBuilder.Sql(
                """
                UPDATE identity."Users" SET "CreatedOnUtc" = NOW() WHERE "CreatedOnUtc" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                UPDATE identity."Roles" SET "CreatedOnUtc" = NOW() WHERE "CreatedOnUtc" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                schema: "identity",
                table: "Roles");
        }
    }
}
