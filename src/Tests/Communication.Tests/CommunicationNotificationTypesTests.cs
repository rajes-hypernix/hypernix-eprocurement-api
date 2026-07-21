using FSH.Modules.Communication.Contracts;
using Shouldly;
using Xunit;

namespace Communication.Tests;

public sealed class CommunicationNotificationTypesTests
{
    [Fact]
    public void Types_are_stable_dotted_strings()
    {
        CommunicationNotificationTypes.OnboardingSubmitted.ShouldBe("suppliers.onboarding.submitted");
        CommunicationNotificationTypes.OnboardingApproved.ShouldBe("suppliers.onboarding.approved");
        CommunicationNotificationTypes.RfqReleased.ShouldBe("sourcing.rfq.released");
        CommunicationNotificationTypes.AwardApproved.ShouldBe("sourcing.award.approved");
    }
}
