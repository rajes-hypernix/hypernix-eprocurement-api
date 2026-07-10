namespace eProcure.Application.Abstractions;

/// <summary>
/// Injectable UTC clock so deadline / timestamp logic is testable
/// (BUSINESS-RULES: bid deadline is a server timestamp).
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
