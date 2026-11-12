using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

using TransactionValidation.Gateway.PolicyStore.Context;

using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

public sealed class GatewayPolicyMigrationSnapshotTests
{
    [Fact]
    public void ModelSnapshot_MatchesCurrentModel()
    {
        using var dbContext = new GatewayPolicyDbContext(
            new DbContextOptionsBuilder<GatewayPolicyDbContext>()
                .UseNpgsql("Host=localhost;Database=unused")
                .Options);
        var snapshotModel = dbContext.GetService<IModelRuntimeInitializer>()
            .Initialize(dbContext.GetService<IMigrationsAssembly>().ModelSnapshot!.Model);

        var differences = dbContext.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshotModel.GetRelationalModel(),
            dbContext.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Empty(differences);
    }
}
