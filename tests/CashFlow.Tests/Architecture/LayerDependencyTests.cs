using System.Reflection;
using CashFlow.Domain.Accounts;
using NetArchTest.Rules;

namespace CashFlow.Tests.Architecture;

public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Account).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(CashFlow.Application.DependencyInjection).Assembly;

    private const string ApplicationNamespace = "CashFlow.Application";
    private const string InfrastructureNamespace = "CashFlow.Infrastructure";
    private const string ApiNamespace = "CashFlow.Api";
    private const string EntityFrameworkNamespace = "Microsoft.EntityFrameworkCore";

    [Fact]
    public void Domain_DoesNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace, EntityFrameworkNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace, EntityFrameworkNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result)
        => result.IsSuccessful
            ? string.Empty
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
