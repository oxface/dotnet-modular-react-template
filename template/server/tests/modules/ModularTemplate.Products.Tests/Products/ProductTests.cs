using ModularTemplate.Products.Products;
using ModularTemplate.Products.Products.Events;
using Shouldly;

namespace ModularTemplate.Products.Tests.Products;

public sealed class ProductTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenProductIsCreated_SetsValuesAndRecordsDomainEvent()
    {
        Product product = Product.Create("Document Library", "{\"color\":\"green\"}");

        product.Name.ShouldBe("Document Library");
        product.MetadataJson.ShouldBe("{\"color\":\"green\"}");
        product.DomainEvents.Single().ShouldBeOfType<ProductCreatedDomainEvent>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenProductIdIsEmpty_Throws()
    {
        Should.Throw<ArgumentException>(() => Product.Create(Guid.Empty, "Document Library"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rename_WhenNameIsValid_UpdatesNameAndTimestamp()
    {
        Product product = Product.Create("Document Library");
        DateTimeOffset originalUpdatedAtUtc = product.UpdatedAtUtc;

        product.Rename("Knowledge Base");

        product.Name.ShouldBe("Knowledge Base");
        product.UpdatedAtUtc.ShouldBeGreaterThanOrEqualTo(originalUpdatedAtUtc);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rename_WhenNameIsBlank_Throws()
    {
        Product product = Product.Create("Document Library");

        Should.Throw<ArgumentException>(() => product.Rename(" "));
    }
}
