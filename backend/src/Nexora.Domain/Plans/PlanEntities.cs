namespace Nexora.Domain.Plans;

public sealed class Feature
{
    private Feature() { }
    public Feature(string code, string name, DateTimeOffset createdAt) { Id = Guid.NewGuid(); Code = code; Name = name; IsActive = true; CreatedAt = createdAt; }
    public Guid Id { get; private set; } public string Code { get; private set; } = string.Empty; public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } public DateTimeOffset CreatedAt { get; private set; }
    public void Update(string name, bool active) { Name = name; IsActive = active; }
}

public sealed class Plan
{
    private Plan() { }
    public Plan(string code, string name, DateTimeOffset createdAt) { Id = Guid.NewGuid(); Code = code; Name = name; IsActive = true; CreatedAt = createdAt; }
    public Guid Id { get; private set; } public string Code { get; private set; } = string.Empty; public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } public bool IsPublic { get; private set; } public bool IsTrialEligible { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } public ICollection<PlanFeature> Features { get; } = [];
    public void Update(string name, bool active) { Name = name; IsActive = active; }
    public void ConfigureCommercialAvailability(bool isPublic, bool isTrialEligible) { IsPublic = isPublic; IsTrialEligible = isTrialEligible; }
}

public sealed class PlanFeature
{
    private PlanFeature() { }
    public PlanFeature(Guid planId, Guid featureId, bool enabled, int? limit) { PlanId = planId; FeatureId = featureId; Enabled = enabled; Limit = limit; }
    public Guid PlanId { get; private set; } public Plan Plan { get; private set; } = null!; public Guid FeatureId { get; private set; }
    public Feature Feature { get; private set; } = null!; public bool Enabled { get; private set; } public int? Limit { get; private set; }
    public void Configure(bool enabled, int? limit) { Enabled = enabled; Limit = limit; }
}

public sealed class TenantFeatureOverride
{
    private TenantFeatureOverride() { }
    public TenantFeatureOverride(Guid tenantId, Guid featureId, bool enabled, int? limit) { Id = Guid.NewGuid(); TenantId = tenantId; FeatureId = featureId; Enabled = enabled; Limit = limit; }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid FeatureId { get; private set; }
    public Feature Feature { get; private set; } = null!; public bool Enabled { get; private set; } public int? Limit { get; private set; }
    public void Configure(bool enabled, int? limit) { Enabled = enabled; Limit = limit; }
}
