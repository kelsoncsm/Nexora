namespace Nexora.Infrastructure.Verticals;

public sealed class VerticalSetupOptions
{
    public const string SectionName = "VerticalTemplates";
    public Dictionary<string, VerticalTemplateOptions> Templates { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class VerticalTemplateOptions
{
    public string Name { get; init; } = string.Empty;
    public string OnboardingTitle { get; init; } = string.Empty;
    public string OnboardingDescription { get; init; } = string.Empty;
    public string DashboardTitle { get; init; } = string.Empty;
    public VerticalTerminologyOptions Terminology { get; init; } = new();
    public List<VerticalPresetOptions> Services { get; init; } = [];
}

public sealed class VerticalTerminologyOptions
{
    public string Customer { get; init; } = "Cliente";
    public string Professional { get; init; } = "Profissional";
    public string Service { get; init; } = "Serviço";
    public string Schedule { get; init; } = "Agenda";
}

public sealed class VerticalPresetOptions
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
}
