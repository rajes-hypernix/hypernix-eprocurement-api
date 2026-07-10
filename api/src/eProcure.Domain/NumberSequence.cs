namespace eProcure.Domain;

/// <summary>
/// Server-side monotonic counter backing human-readable codes
/// (e.g. RFQ-2026-0001). One row per (Prefix, Year). Codes are never derived
/// from collection length or random numbers (BUSINESS-RULES [$]).
/// </summary>
public class NumberSequence
{
    public string Prefix { get; private set; } = default!;
    public int Year { get; private set; }
    public int LastValue { get; private set; }

    // EF Core
    private NumberSequence() { }

    public NumberSequence(string prefix, int year)
    {
        Prefix = prefix;
        Year = year;
        LastValue = 0;
    }

    /// <summary>Increments and returns the next value in this sequence.</summary>
    public int Next()
    {
        LastValue += 1;
        return LastValue;
    }

    /// <summary>Fast-forwards the counter so it never re-issues an already-used code
    /// (used to align the sequence with seeded codes).</summary>
    public void AdvanceTo(int value)
    {
        if (value > LastValue) LastValue = value;
    }
}
