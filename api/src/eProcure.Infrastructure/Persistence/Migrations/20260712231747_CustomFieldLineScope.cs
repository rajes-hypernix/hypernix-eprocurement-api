using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomFieldLineScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomFieldValues_FieldDefId_RecordId",
                table: "CustomFieldValues");

            migrationBuilder.AddColumn<Guid>(
                name: "LineId",
                table: "CustomFieldValues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "CustomFieldDefs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_FieldDefId_RecordId",
                table: "CustomFieldValues",
                columns: new[] { "FieldDefId", "RecordId" },
                unique: true,
                filter: "\"LineId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_FieldDefId_RecordId_LineId",
                table: "CustomFieldValues",
                columns: new[] { "FieldDefId", "RecordId", "LineId" },
                unique: true,
                filter: "\"LineId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomFieldValues_FieldDefId_RecordId",
                table: "CustomFieldValues");

            migrationBuilder.DropIndex(
                name: "IX_CustomFieldValues_FieldDefId_RecordId_LineId",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "LineId",
                table: "CustomFieldValues");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "CustomFieldDefs");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_FieldDefId_RecordId",
                table: "CustomFieldValues",
                columns: new[] { "FieldDefId", "RecordId" },
                unique: true);
        }
    }
}
