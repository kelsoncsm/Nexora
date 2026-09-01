using System.Net;

namespace Nexora.Infrastructure.Notifications;

public sealed record WelcomeEmailContent(string Subject, string HtmlBody);

public static class WelcomeEmailTemplate
{
    public const string Key = "WelcomeEmail";

    public static WelcomeEmailContent Render(string displayName, string? applicationUrl)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        var link = Uri.TryCreate(applicationUrl, UriKind.Absolute, out var uri)
            ? $"<p><a href=\"{WebUtility.HtmlEncode(uri.ToString())}\">Acessar o Nexora</a></p>"
            : string.Empty;
        return new WelcomeEmailContent(
            "Boas-vindas ao Nexora",
            $"<h1>Boas-vindas ao Nexora</h1><p>Olá, {safeName}. Sua conta foi criada.</p>{link}");
    }
}
