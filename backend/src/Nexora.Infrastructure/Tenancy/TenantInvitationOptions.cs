namespace Nexora.Infrastructure.Tenancy;

/// <summary>Centralised invitation settings — no magic numbers spread through the code.</summary>
public sealed class TenantInvitationOptions
{
    public const string SectionName = "Tenancy:Invitations";

    /// <summary>How long a freshly issued (or resent) invitation token stays valid.</summary>
    public int ExpirationDays { get; init; } = 7;
}
