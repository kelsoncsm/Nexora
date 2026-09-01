using Nexora.Application;
using Nexora.Domain;

namespace Nexora.UnitTests.Architecture;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void DomainDoesNotReferenceOuterLayers()
    {
        var references = typeof(DomainAssembly).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("Nexora.Application", references);
        Assert.DoesNotContain("Nexora.Infrastructure", references);
        Assert.DoesNotContain("Nexora.Api", references);
    }

    [Fact]
    public void ApplicationDoesNotReferenceInfrastructureOrApi()
    {
        var references = typeof(ApplicationAssembly).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("Nexora.Infrastructure", references);
        Assert.DoesNotContain("Nexora.Api", references);
    }
}
