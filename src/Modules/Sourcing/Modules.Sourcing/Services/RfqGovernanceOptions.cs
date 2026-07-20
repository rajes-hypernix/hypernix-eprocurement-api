namespace FSH.Modules.Sourcing.Services;

public sealed class RfqGovernanceOptions
{
    public int MaxExtensions { get; set; } = 2;
    public int MinRemainingHoursForLateInvite { get; set; } = 72;
}
