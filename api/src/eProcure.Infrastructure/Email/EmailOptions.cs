namespace eProcure.Infrastructure.Email;

/// <summary>SMTP transport settings (bound from the "Email" section). No secrets in code — the host,
/// credentials, and from-address all come from configuration / environment.</summary>
public sealed class EmailOptions
{
    public string From { get; set; } = "noreply@eprocure.local";
    public string FromName { get; set; } = "SPSB eProcure";
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    /// <summary>SMTP host. When blank (e.g. local dev), the sender logs the message instead of dialing out.</summary>
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseSsl { get; set; } = true;
}
