using ModularTemplate.SharedKernel.Extensions;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Extensions;

public sealed class StringExtensionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void TrimDistinctRequired_WhenValuesContainWhitespaceAndDuplicates_ReturnsNormalizedValues()
    {
        string[] values = new[] { " identity ", "operations", "identity", " " }
            .TrimDistinctRequired("modules");

        values.ShouldBe(["identity", "operations"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ContainsTrimmedOrdinal_WhenValueMatchesAfterTrimming_ReturnsTrue()
    {
        new[] { " identity " }.ContainsTrimmedOrdinal("identity").ShouldBeTrue();
    }
}
