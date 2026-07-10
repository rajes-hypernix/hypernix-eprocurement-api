using System.Reflection;
using eProcure.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Regression guard for Bug 1 (Invite page 404): the exact routes + verbs the frontend API client
/// calls must stay defined on the controllers, so an accidental removal/rename is caught in CI.
/// (A stale build at runtime can't be caught by a test, but a route drift in code can.)
/// </summary>
public class OnboardingRoutingTests
{
    [Theory]
    [InlineData(typeof(OnboardingController), "api/onboarding")]
    [InlineData(typeof(CustomListsController), "api/custom-lists")]
    public void Controller_IsApiControllerWithExpectedPrefix(Type controller, string prefix)
    {
        controller.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        controller.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be(prefix);
    }

    [Theory]
    // (method name, HTTP verb attribute, route template) — must match web/src/api/client.ts.
    [InlineData("Templates", typeof(HttpGetAttribute), "templates")]
    [InlineData("Applications", typeof(HttpGetAttribute), "applications")]
    [InlineData("Create", typeof(HttpPostAttribute), "invitations")]         // "Generate & send link"
    [InlineData("Resolve", typeof(HttpPostAttribute), "resolve")]
    [InlineData("Approve", typeof(HttpPostAttribute), "applications/{id:guid}/approve")]
    public void OnboardingController_ExposesRoute(string method, Type verb, string template)
    {
        var m = typeof(OnboardingController).GetMethod(method);
        m.Should().NotBeNull($"OnboardingController.{method} must exist");
        var attr = m!.GetCustomAttributes(verb, false).Cast<HttpMethodAttribute>().SingleOrDefault();
        attr.Should().NotBeNull($"{method} must carry {verb.Name}");
        attr!.Template.Should().Be(template);
    }

    [Fact]
    public void CustomListsController_ExposesListRoute() =>
        typeof(CustomListsController).GetMethod("List")!.GetCustomAttribute<HttpGetAttribute>().Should().NotBeNull();
}
