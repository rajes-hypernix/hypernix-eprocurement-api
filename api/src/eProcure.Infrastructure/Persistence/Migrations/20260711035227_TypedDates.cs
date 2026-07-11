using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Slice H T4 (AN-1, AN-6): retire the dd/MM/yyyy string date fields for typed columns. Four
    /// string-only fields become <c>date</c> (Invoices.Date, Grns.ReceivedDate, Asns.ShippedDate /
    /// ExpectedDate); the two PR display strings (RaisedDate / RequiredDate) are DROPPED because the
    /// typed <c>RaisedOn</c> / <c>RequiredOn</c> columns already hold the same values (0 nulls in the
    /// data) and are now what the DTO exposes.
    ///
    /// The four conversions parse dd/MM/yyyy. A pre-check RAISES if any non-empty value is not
    /// dd/MM/yyyy — the migration STOPS rather than defaulting or dropping a bad date (T4 rule).
    /// (VendorCertification.ValidTo is intentionally NOT converted: its data is bare years /
    /// placeholders, not dates — see BACKLOG.)
    ///
    /// Down() restores the prior shape from the typed data: the four columns become dd/MM/yyyy text
    /// again (null → ''), and the PR display strings are re-added and backfilled from RaisedOn /
    /// RequiredOn — no information is lost across Up→Down→Up.
    /// </summary>
    public partial class TypedDates : Migration
    {
        // (table, column) for the four string-only fields converting to a typed date.
        private static readonly (string Table, string Col)[] StringDates =
        [
            ("Invoices", "Date"),
            ("Grns", "ReceivedDate"),
            ("Asns", "ShippedDate"),
            ("Asns", "ExpectedDate"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // STOP if any non-empty legacy value is not dd/MM/yyyy — never default a bad date.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE bad int;
                BEGIN
                    SELECT
                        (SELECT count(*) FROM ""Invoices"" WHERE ""Date"" <> '' AND ""Date"" !~ '^\d{2}/\d{2}/\d{4}$') +
                        (SELECT count(*) FROM ""Grns"" WHERE ""ReceivedDate"" <> '' AND ""ReceivedDate"" !~ '^\d{2}/\d{2}/\d{4}$') +
                        (SELECT count(*) FROM ""Asns"" WHERE ""ShippedDate"" <> '' AND ""ShippedDate"" !~ '^\d{2}/\d{2}/\d{4}$') +
                        (SELECT count(*) FROM ""Asns"" WHERE ""ExpectedDate"" <> '' AND ""ExpectedDate"" !~ '^\d{2}/\d{2}/\d{4}$')
                    INTO bad;
                    IF bad > 0 THEN
                        RAISE EXCEPTION 'TypedDates: % date value(s) are not dd/MM/yyyy - refusing to convert (Slice H T4 stop-condition).', bad;
                    END IF;
                END $$;");

            // text -> nullable date, parsing dd/MM/yyyy; empty string -> null. The columns were NOT NULL
            // (string default ''); the typed columns are DateOnly? so drop NOT NULL to match the model.
            foreach (var (table, col) in StringDates)
            {
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" ALTER COLUMN ""{col}"" DROP NOT NULL;");
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" ALTER COLUMN ""{col}"" TYPE date USING to_date(NULLIF(""{col}"", ''), 'DD/MM/YYYY');");
            }

            // PR display strings are redundant with the typed RaisedOn / RequiredOn (now exposed).
            migrationBuilder.DropColumn(name: "RaisedDate", table: "PurchaseRequisitions");
            migrationBuilder.DropColumn(name: "RequiredDate", table: "PurchaseRequisitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // date -> dd/MM/yyyy text (null -> ''); restore NOT NULL to match the original columns.
            foreach (var (table, col) in StringDates)
            {
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" ALTER COLUMN ""{col}"" TYPE text USING coalesce(to_char(""{col}"", 'DD/MM/YYYY'), '');");
                migrationBuilder.Sql($@"ALTER TABLE ""{table}"" ALTER COLUMN ""{col}"" SET NOT NULL;");
            }

            // Re-add the PR display strings and rebuild them from the typed columns.
            migrationBuilder.AddColumn<string>(name: "RaisedDate", table: "PurchaseRequisitions", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "RequiredDate", table: "PurchaseRequisitions", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.Sql(@"
                UPDATE ""PurchaseRequisitions""
                SET ""RaisedDate"" = coalesce(to_char(""RaisedOn"", 'DD/MM/YYYY'), ''),
                    ""RequiredDate"" = coalesce(to_char(""RequiredOn"", 'DD/MM/YYYY'), '');");
        }
    }
}
