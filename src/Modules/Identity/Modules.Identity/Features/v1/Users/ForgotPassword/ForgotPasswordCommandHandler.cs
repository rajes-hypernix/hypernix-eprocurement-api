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

        var origin = ResolveClientOrigin();
        if (string.IsNullOrWhiteSpace(origin))
        {
            throw new InvalidOperationException("Origin URL is not configured.");
        }

        await _userService.ForgotPasswordAsync(command.Email, origin, cancellationToken).ConfigureAwait(false);

        return "Password reset email sent.";
    }

    /// <summary>
    /// Build the SPA base URL for the reset link.
    /// Browser <c>Origin</c> is scheme+host+port only (no path), so IIS apps under a virtual
    /// directory (e.g. <c>/UI-REAL</c>) must prefer <see cref="OriginOptions.OriginUrl"/> when it
    /// shares that host and includes a path. Otherwise prefer a CORS-allowed request Origin
    /// (local multi-port SPAs), then fall back to configured OriginUrl.
    /// </summary>
    private string? ResolveClientOrigin()
    {
        var configured = _originOptions.Value?.OriginUrl?.ToString()?.TrimEnd('/');

        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null)
        {
            return configured;
        }

        var candidate = request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(candidate)
            && Uri.TryCreate(request.Headers.Referer.FirstOrDefault(), UriKind.Absolute, out var referer))
        {
            candidate = referer.GetLeftPart(UriPartial.Authority);
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return configured;
        }

        candidate = candidate.TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(configured)
            && Uri.TryCreate(configured, UriKind.Absolute, out var configuredUri)
            && Uri.TryCreate(candidate, UriKind.Absolute, out var candidateUri)
            && string.Equals(
                configuredUri.GetLeftPart(UriPartial.Authority),
                candidateUri.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase)
            && configuredUri.AbsolutePath is { Length: > 1 })
        {
            return configured;
        }

        var cors = _corsOptions.Value;
        if (cors.AllowAll)
        {
            return candidate;
        }

        var allowed = cors.AllowedOrigins ?? [];
        if (allowed.Any(o => string.Equals(o.TrimEnd('/'), candidate, StringComparison.OrdinalIgnoreCase)))
        {
            return candidate;
        }

        return configured;
    }
}
