using FSH.Framework.Web.Origin;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Contracts.v1.Users.ForgotPassword;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using CorsOptions = FSH.Framework.Web.Cors.CorsOptions;

namespace FSH.Modules.Identity.Features.v1.Users.ForgotPassword;

public sealed class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, string>
{
    private readonly IUserService _userService;
    private readonly IOptions<OriginOptions> _originOptions;
    private readonly IOptions<CorsOptions> _corsOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ForgotPasswordCommandHandler(
        IUserService userService,
        IOptions<OriginOptions> originOptions,
        IOptions<CorsOptions> corsOptions,
        IHttpContextAccessor httpContextAccessor)
    {
        _userService = userService;
        _originOptions = originOptions;
        _corsOptions = corsOptions;
        _httpContextAccessor = httpContextAccessor;
    }

    public async ValueTask<string> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var origin = ResolveClientOrigin()
            ?? _originOptions.Value?.OriginUrl?.ToString();

        if (string.IsNullOrWhiteSpace(origin))
        {
            throw new InvalidOperationException("Origin URL is not configured.");
        }

        await _userService.ForgotPasswordAsync(command.Email, origin, cancellationToken).ConfigureAwait(false);

        return "Password reset email sent.";
    }

    /// <summary>
    /// Prefer the calling SPA's Origin (e.g. e-procurement :5175) so the reset link
    /// lands on the same app. Only accept origins allowed by CorsOptions to avoid
    /// open-redirect phishing via forged Origin headers.
    /// </summary>
    private string? ResolveClientOrigin()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null)
        {
            return null;
        }

        var candidate = request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(candidate)
            && Uri.TryCreate(request.Headers.Referer.FirstOrDefault(), UriKind.Absolute, out var referer))
        {
            candidate = referer.GetLeftPart(UriPartial.Authority);
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var cors = _corsOptions.Value;
        if (cors.AllowAll)
        {
            return candidate.TrimEnd('/');
        }

        var allowed = cors.AllowedOrigins ?? [];
        if (allowed.Any(o => string.Equals(o.TrimEnd('/'), candidate.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
        {
            return candidate.TrimEnd('/');
        }

        return null;
    }
}
