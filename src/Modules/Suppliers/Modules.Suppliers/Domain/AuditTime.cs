namespace FSH.Modules.Suppliers.Domain;

internal static class AuditTime
{
    public static DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();

    public static DateTimeOffset FromUtc(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
}
