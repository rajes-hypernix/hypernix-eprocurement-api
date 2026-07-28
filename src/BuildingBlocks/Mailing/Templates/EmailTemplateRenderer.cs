using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FSH.Framework.Mailing.Templates;

public sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private static readonly Regex TokenRegex = new(
        @"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Assembly _assembly = typeof(EmailTemplateRenderer).Assembly;

    public string Render(string templateName, IReadOnlyDictionary<string, string?> tokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(tokens);

        string template = _cache.GetOrAdd(templateName.Trim(), LoadTemplate);

        return TokenRegex.Replace(template, match =>
        {
            string key = match.Groups["key"].Value;
            if (!tokens.TryGetValue(key, out string? value) || value is null)
            {
                return string.Empty;
            }

            return WebUtility.HtmlEncode(value);
        });
    }

    private string LoadTemplate(string templateName)
    {
        string resourceSuffix = $".Templates.{templateName}.html";
        string? resourceName = _assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new FileNotFoundException(
                $"Email template '{templateName}' was not found as an embedded resource ending with '{resourceSuffix}'.");
        }

        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Could not open embedded email template '{resourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
