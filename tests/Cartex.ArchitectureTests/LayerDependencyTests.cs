using System.Reflection;
using Cartex.Application;
using Cartex.Auth.Services;
using Cartex.Domain.Entities;
using Cartex.Infrastructure.Web;
using Cartex.Persistence;
using NetArchTest.Rules;
using Xunit;

namespace Cartex.ArchitectureTests;

public class LayerDependencyTests
{
    private const string Domain = "Cartex.Domain";
    private const string Application = "Cartex.Application";
    private const string Persistence = "Cartex.Persistence";
    private const string Auth = "Cartex.Auth";
    private const string Infrastructure = "Cartex.Infrastructure";
    private const string Api = "Cartex.Api";

    private static readonly Assembly DomainAssembly = typeof(Business).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Cartex.Application.DependencyInjection).Assembly;
    private static readonly Assembly PersistenceAssembly = typeof(ApplicationDbContext).Assembly;
    private static readonly Assembly AuthAssembly = typeof(IJwtTokenGenerator).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(HttpPagingMetadataWriter).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer() =>
        AssertNoDependency(DomainAssembly, Application, Persistence, Auth, Infrastructure, Api);

    [Fact]
    public void Application_should_not_depend_on_infrastructure_or_api() =>
        AssertNoDependency(ApplicationAssembly, Infrastructure, Api);

    [Fact]
    public void Persistence_should_not_depend_on_application_auth_infrastructure_or_api() =>
        AssertNoDependency(PersistenceAssembly, Application, Auth, Infrastructure, Api);

    [Fact]
    public void Auth_should_not_depend_on_application_persistence_infrastructure_or_api() =>
        AssertNoDependency(AuthAssembly, Application, Persistence, Infrastructure, Api);

    [Fact]
    public void Infrastructure_should_not_depend_on_api() =>
        AssertNoDependency(InfrastructureAssembly, Api);

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        var violations = result.FailingTypes?.Select(t => t.FullName) ?? [];

        Assert.True(result.IsSuccessful,
            $"{assembly.GetName().Name} must not depend on [{string.Join(", ", forbidden)}]. " +
            $"Violations: {string.Join(", ", violations)}");
    }
}
