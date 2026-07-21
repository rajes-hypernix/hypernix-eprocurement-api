using System.Security.Claims;

namespace FSH.Modules.Identity.Contracts.Services;

/// <summary>
/// Service for user registration and external authentication.
/// </summary>
public interface IUserRegistrationService
{
    /// <summary>
    /// Registers a new user with password.
    /// </summary>
    /// <param name="firstName">First name.</param>
    /// <param name="lastName">Last name.</param>
    /// <param name="email">Email address.</param>
    /// <param name="userName">Username.</param>
    /// <param name="password">Password.</param>
    /// <param name="confirmPassword">Password confirmation.</param>
    /// <param name="phoneNumber">Phone number.</param>
    /// <param name="origin">Request base URL used to build the confirmation link.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="vendorId">
    /// Set only when provisioning a vendor-portal login — stamps <c>FshUser.VendorId</c> so the
    /// JWT carries a <c>vendorId</c> claim. Null for ordinary internal-user registration.
    /// </param>
    Task<string> RegisterAsync(
        string firstName,
        string lastName,
        string email,
        string userName,
        string password,
        string confirmPassword,
        string phoneNumber,
        string origin,
        CancellationToken cancellationToken,
        Guid? vendorId = null);

    /// <summary>
    /// Gets or creates a user from an external authentication principal.
    /// </summary>
    Task<string> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a user's email address.
    /// </summary>
    Task<string> ConfirmEmailAsync(string userId, string code, string tenant, CancellationToken cancellationToken);

    /// <summary>
    /// Administratively marks a user's email as confirmed without a confirmation token. Gated by the
    /// <c>Permissions.Users.ConfirmEmail</c> permission at the endpoint. Idempotent.
    /// </summary>
    Task AdminConfirmEmailAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-sends the email-confirmation link to a user who has not yet confirmed their address.
    /// <paramref name="origin"/> is the request base URL used to build the confirmation link.
    /// Throws if the user's email is already confirmed.
    /// </summary>
    Task ResendConfirmationEmailAsync(string userId, string origin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a user's phone number.
    /// </summary>
    Task<string> ConfirmPhoneNumberAsync(string userId, string code, CancellationToken cancellationToken = default);
}