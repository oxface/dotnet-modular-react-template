using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Host.Tests.Authentication;
using ModularTemplate.Host.Tests.Support;
using ModularTemplate.Products.Contracts.Products;
using NSubstitute;
using Shouldly;

namespace ModularTemplate.Host.Tests.Products;

public sealed class GetProductEndpointTests
{
    [Fact]
    [Trait("Category", "Application")]
    public async Task GetProduct_WhenProductExists_ReturnsProductDetails()
    {
        Guid productId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var productsQueries = Substitute.For<IProductsQueries>();
        productsQueries.GetProductAsync(productId, Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new ProductDetails(
                productId,
                "Document Library",
                DateTimeOffset.Parse("2026-05-01T12:00:00Z"),
                DateTimeOffset.Parse("2026-05-01T12:00:03Z")));

        await using var factory = new HostApplicationFactory(services =>
        {
            services.AddScoped(_ => productsQueries);
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/products/{productId}");
        request.Headers.Add(TestAuthenticationHandler.SubjectHeader, "subject-1");

        HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductDetails>(cancellationToken: CancellationToken.None);
        body.ShouldNotBeNull();
        body.ProductId.ShouldBe(productId);
        body.Name.ShouldBe("Document Library");
    }

    [Fact]
    [Trait("Category", "Application")]
    public async Task GetProduct_WhenProductDoesNotExist_ReturnsNotFound()
    {
        var productsQueries = Substitute.For<IProductsQueries>();
        productsQueries.GetProductAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs((ProductDetails?)null);

        await using var factory = new HostApplicationFactory(services =>
        {
            services.AddScoped(_ => productsQueries);
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/products/33333333-3333-3333-3333-333333333333");
        request.Headers.Add(TestAuthenticationHandler.SubjectHeader, "subject-1");

        HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
