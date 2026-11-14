// Role: Checks the prepared publication SQL/EF contract without opening any SQL connection.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NbJewel.Api.Knowledge;
namespace NbJewel.Api.Tests;

public sealed class PublicationSchemaContractTests
{
    [Fact]
    public void PublicationUsesExternallyAssignedErpItemIdInModelAndPreparedSchema()
    {
        using var db = new KnowledgeDatabase(new DbContextOptionsBuilder<KnowledgeDatabase>()
            .UseSqlServer("Server=unused.invalid;Database=model_only;Integrated Security=True").Options);
        var property = db.Model.FindEntityType(typeof(ProductPublication))!.FindProperty(nameof(ProductPublication.ItemId))!;
        Assert.Equal(ValueGenerated.Never, property.ValueGenerated);
        var schema = File.ReadAllText(Path.Combine(SampleRepositoryFactory.FindRoot(), "docs", "platform-schema.sql"));
        var table = schema.Split("CREATE TABLE [AiPublicationProducts] (")[1].Split(");")[0];
        Assert.DoesNotContain("IDENTITY", table);
        Assert.Contains("[AssetRevisionId] nvarchar(32)", table);
    }
}
