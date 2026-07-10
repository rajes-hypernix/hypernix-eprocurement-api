using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Maps PostgreSQL's <c>xmin</c> system column as an optimistic-concurrency token on nine
    /// aggregate roots (T2). <c>xmin</c> already exists on every table, so there is NO DDL to run —
    /// this migration only records the model mapping in the snapshot. (Npgsql 10 removed the
    /// <c>UseXminAsConcurrencyToken</c> helper that used to suppress the scaffolded AddColumn; the
    /// EF scaffolder therefore emitted <c>AddColumn "xmin"</c>, which Postgres would reject as a
    /// system-column name clash — so the body is intentionally empty.)
    /// </summary>
    public partial class ConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: xmin is a PostgreSQL system column, present on every table by definition.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: the system column cannot (and must not) be dropped.
        }
    }
}
