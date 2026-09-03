using System.Net;

namespace Nexora.Infrastructure.Notifications;

public sealed record TenantInvitationEmailContent(string Subject, string HtmlBody);

/// <summary>
/// Renders the "you were invited to a company" e-mail. The accept URL carries the raw invitation
/// token — it is present in the outbox payload only until the message is processed, then the
/// payload is redacted (see <see cref="EmailOutboxProcessor"/>).
/// </summary>
public static class TenantInvitationEmailTemplate
{
    public const string Key = "TenantInvitationEmail";

    public static TenantInvitationEmailContent Render(string tenantName, string roleName, string inviterEmail, string acceptUrl)
    {
        var safeTenant = WebUtility.HtmlEncode(tenantName);
        var safeRole = WebUtility.HtmlEncode(roleName);
        var safeInviter = WebUtility.HtmlEncode(inviterEmail);
        var link = Uri.TryCreate(acceptUrl, UriKind.Absolute, out var uri)
            ? $"<p><a href=\"{WebUtility.HtmlEncode(uri.ToString())}\">Aceitar o convite</a></p>"
            : string.Empty;
        return new TenantInvitationEmailContent(
            $"Convite para {tenantName} no Nexora",
            $"<h1>Você foi convidado para {safeTenant}</h1>" +
            $"<p>{safeInviter} convidou você para participar de <strong>{safeTenant}</strong> no Nexora " +
            $"com o perfil <strong>{safeRole}</strong>.</p>" +
            link +
            "<p>Este convite expira em alguns dias. Se você não reconhece este convite, ignore este e-mail.</p>");
    }
}
