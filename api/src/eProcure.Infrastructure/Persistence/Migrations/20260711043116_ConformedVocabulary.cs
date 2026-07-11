using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Slice H T6 (AN-5/AN-7): data-only country conformance. Rewrites free-text country LABELS
    /// ("Malaysia") to the controlled COUNTRY Custom List CODE ("MY") on Vendors.Country and
    /// VendorAddresses.Country, so grouping conforms. Statements are shared with (and covered by)
    /// ConformedVocabularyBackfillTests. Down() restores the labels. No schema change.
    /// </summary>
    public partial class ConformedVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in ConformedVocabularyBackfill.Up)
                migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in ConformedVocabularyBackfill.Down)
                migrationBuilder.Sql(sql);
        }
    }
}
