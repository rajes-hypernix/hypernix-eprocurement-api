using AutoFixture;
using FSH.Framework.Web.Cors;
using FSH.Framework.Web.Origin;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Contracts.v1.Users.ForgotPassword;
using FSH.Modules.Identity.Features.v1.Users.ForgotPassword;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using CorsOptions = FSH.Framework.Web.Cors.CorsOptions;

namespace Identity.Tests.Handlers;

public sealed class ForgotPasswordCommandHandlerTests
{
    private readonly IUserService _userService;
    private readonly IOptions<OriginOptions> _originOptions;
    private readonly IOptions<CorsOptions> _corsOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ForgotPasswordCommandHandler _sut;
    private readonly IFixture _fixture;

    public ForgotPasswordCommandHandlerTests()
    {
        _userService = Substitute.For<IUserService>();
        _originOptions = Substitute.For<IOptions<OriginOptions>>();
        _corsOptions = Substitute.For<IOptions<CorsOptions>>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _corsOptions.Value.Returns(new CorsOptions { AllowAll = false, AllowedOrigins = ["http://localhost:5175"] });
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        _sut = new ForgotPasswordCommandHandler(_userService, _originOptions, _corsOptions, _httpContextAccessor);
        _fixture = new Fixture();
    }

    [Fact]
    public async Task Handle_Should_CallForgotPasswordAsync_When_ValidRequest()
    {
        var command = _fixture.Create<ForgotPasswordCommand>();
        var originUrl = "https://test.com";
        _originOptions.Value.Returns(new OriginOptions { OriginUrl = new Uri(originUrl) });

        var result = await _sut.Handle(command, CancellationToken.None);

        result.ShouldBe("Password reset email sent.");
        await _userService.Received(1).ForgotPasswordAsync(command.Email, Arg.Is<string>(s => s.StartsWith(originUrl)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_PreferAllowedRequestOrigin_OverConfiguredOrigin()
    {
        var command = _fixture.Create<ForgotPasswordCommand>();
        _originOptions.Value.Returns(new OriginOptions { OriginUrl = new Uri("https://fallback.example") });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Origin = "http://localhost:5175";
        _httpContextAccessor.HttpContext.Returns(httpContext);

        await _sut.Handle(command, CancellationToken.None);

        await _userService.Received(1).ForgotPasswordAsync(
            command.Email,
            "http://localhost:5175",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_IgnoreDisallowedRequestOrigin()
    {
        var command = _fixture.Create<ForgotPasswordCommand>();
        _originOptions.Value.Returns(new OriginOptions { OriginUrl = new Uri("https://fallback.example") });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Origin = "https://evil.example";
        _httpContextAccessor.HttpContext.Returns(httpContext);

        await _sut.Handle(command, CancellationToken.None);

        await _userService.Received(1).ForgotPasswordAsync(
            command.Email,
            Arg.Is<string>(s => s.StartsWith("https://fallback.example")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ThrowInvalidOperationException_When_OriginNotConfigured()
    {
        var command = _fixture.Create<ForgotPasswordCommand>();
        _originOptions.Value.Returns(new OriginOptions { OriginUrl = null });

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_ThrowArgumentNullException_When_CommandIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_PassCancellationToken_ToUserService()
    {
        var command = _fixture.Create<ForgotPasswordCommand>();
        var originUrl = "https://test.com";
        _originOptions.Value.Returns(new OriginOptions { OriginUrl = new Uri(originUrl) });
        using var cts = new CancellationTokenSource();

        await _sut.Handle(command, cts.Token);

        await _userService.Received(1).ForgotPasswordAsync(command.Email, Arg.Is<string>(s => s.StartsWith(originUrl)), cts.Token);
    }
}
