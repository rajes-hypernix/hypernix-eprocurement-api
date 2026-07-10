using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Intentionally empty (Slice G T2). It maps PostgreSQL's <c>xmin</c> system column as the
    /// optimistic-concurrency token on nine aggregate roots — but there is NO DDL to run, and the
    /// migration exists for one reason only: to record the concurrency-token mapping in the model
    /// snapshot.
    ///
    /// Why empty:
    ///   • <c>xmin</c> is a PostgreSQL SYSTEM column — it already exists on every table, so it can be
    ///     neither added (name clash) nor dropped.
    ///   • Npgsql 10 REMOVED the <c>UseXminAsConcurrencyToken()</c> helper that used to flag the
    ///     property as a system column and suppress its migration. So we map it MANUALLY in
    ///     <c>AppDbContext.OnModelCreating</c>:
    ///     <c>Property&lt;uint&gt;("xmin").HasColumnName("xmin").HasColumnType("xid")
    ///        .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()</c>.
    ///   • Without that helper the EF scaffolder treats <c>xmin</c> as an ordinary new property and
    ///     emits <c>AddColumn&lt;uint&gt;("xmin", type: "xid")</c>, which Postgres rejects as a
    ///     system-column name clash. We therefore stripped the generated DDL and left the body empty.
    ///
    /// Why it must still exist (do not delete it): applying this empty migration writes <c>xmin</c>
    /// into <c>AppDbContextModelSnapshot</c>. Without that snapshot entry, EVERY subsequent
    /// `migrations add` would re-detect <c>xmin</c> as a new column and scaffold the same broken
    /// <c>AddColumn("xmin")</c> again.
    /// </summary>
    public partial class ConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: xmin is a PostgreSQL system column, present on every table by definition — see
            // the class summary. The mapping is recorded in the snapshot, not applied as DDL.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: the system column cannot (and must not) be dropped.
        }
    }
}
