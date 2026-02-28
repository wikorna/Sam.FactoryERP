namespace FactoryERP.ArchTests;

using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

public sealed class ModuleDependencyTests
{
    [Fact]
    public void SalesDomainMustNotDependOnOtherModules()
    {
        var forbidden = new[]
        {
            "FactoryERP.Modules.Production",
            "FactoryERP.Modules.Purchasing",
            "FactoryERP.Modules.Inventory",
            "FactoryERP.Modules.Costing",
            "FactoryERP.Modules.Quality",
            "FactoryERP.Modules.Admin",
            "FactoryERP.Modules.EDI",
            "FactoryERP.Modules.Labeling"
        };

        var result = Types.InAssembly(typeof(FactoryERP.Modules.Sales.Domain.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            result.FailingTypeNames != null
                ? string.Join(System.Environment.NewLine, result.FailingTypeNames)
                : string.Empty);
    }
}
