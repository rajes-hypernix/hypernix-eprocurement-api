using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Slice H T8: the PrLineSourcing → PR-line foreign key the Slice G backlog deferred. It became
    /// possible once T1 gave <c>PrLine</c> a stable Guid primary key. Because PrLine is an OWNED entity,
    /// EF Core won't model a navigation to it from outside the aggregate, so this is a DB-level FK added
    /// by hand (the "reference-by-id, no nav" pattern, now with real referential integrity).
    ///
    /// It is DEFERRABLE INITIALLY DEFERRED: EF does not know about this FK, so it can't order the two
    /// inserts within a transaction — deferring the check to commit makes any interleaving safe. Orphan
    /// detection (same discipline as the T1 FK migration) refuses to add the constraint if any sourcing
    /// row already points at a missing PrLine.
    /// </summary>
    public partial class PrLineSourcingFk : Migration
    {
        private const string Fk = "FK_PrLineSourcings_PrLines_PrLineId";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                DECLARE orphans int;
                BEGIN
                    SELECT count(*) INTO orphans
                    FROM ""PrLineSourcings"" s
                    LEFT JOIN ""PrLines"" pl ON pl.""Id"" = s.""PrLineId""
                    WHERE pl.""Id"" IS NULL;
                    IF orphans > 0 THEN
                        RAISE EXCEPTION 'PrLineSourcingFk: % sourcing row(s) reference a missing PrLine - refusing to add the FK.', orphans;
                    END IF;
                END $$;");

            migrationBuilder.Sql($@"
                ALTER TABLE ""PrLineSourcings"" ADD CONSTRAINT ""{Fk}""
                FOREIGN KEY (""PrLineId"") REFERENCES ""PrLines"" (""Id"") DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql($@"ALTER TABLE ""PrLineSourcings"" DROP CONSTRAINT ""{Fk}"";");
    }
}
