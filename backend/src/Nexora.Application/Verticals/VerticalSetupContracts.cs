namespace Nexora.Application.Verticals;

public sealed record VerticalTerminology(string Customer, string Professional, string Service, string Schedule);
public sealed record VerticalServicePreset(string Code, string Name, string Category);
public sealed record VerticalSetupView(string Code, string Name, string OnboardingTitle, string OnboardingDescription,
    string DashboardTitle, VerticalTerminology Terminology, IReadOnlyList<VerticalServicePreset> ServicePresets);
public sealed record VerticalServiceSelection(string PresetCode, int DurationMinutes, decimal Price);
public sealed record ApplyVerticalSetup(IReadOnlyList<VerticalServiceSelection> Services);
public sealed record AppliedVerticalSetup(IReadOnlyList<Guid> ServiceIds);

public interface IVerticalSetupService
{
    Task<VerticalSetupView?> GetAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<AppliedVerticalSetup> ApplyAsync(Guid tenantId, ApplyVerticalSetup input, CancellationToken cancellationToken);
}

public sealed class VerticalSetupValidationException(string message) : Exception(message);
