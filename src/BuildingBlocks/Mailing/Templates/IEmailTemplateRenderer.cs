namespace FSH.Framework.Mailing.Templates;

/// <summary>
/// Loads HTML email templates from the Mailing assembly and substitutes <c>{{Token}}</c> placeholders.
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <param name="templateName">File stem under Templates/ (e.g. <c>reset-password</c>).</param>
    /// <param name="tokens">Placeholder values; each value is HTML-encoded before substitution.</param>
    string Render(string templateName, IReadOnlyDictionary<string, string?> tokens);
}
