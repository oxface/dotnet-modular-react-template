using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Host.Tests.Authentication;
using ModularTemplate.Host.Tests.Support;
using ModularTemplate.Operations.Contracts.Operations;
using NSubstitute;
using Shouldly;

namespace ModularTemplate.Host.Tests.Operations;

public sealed class GetOperationEndpointTests
{
    [Fact]
    [Trait("Category", "Application")]
    public async Task GetOperation_WhenOperationExists_ReturnsOperationDetails()
    {
        Guid operationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var operationsQueries = Substitute.For<IOperationsQueries>();
        operationsQueries.GetOperationAsync(operationId, Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new OperationDetails(
                operationId,
                "notifications.send-email",
                OperationStatus.Completed,
                DateTimeOffset.Parse("2026-05-01T12:00:00Z"),
                DateTimeOffset.Parse("2026-05-01T12:00:03Z"),
                DateTimeOffset.Parse("2026-05-01T12:00:03Z"),
                null,
                null,
                "{\"message\":\"sent\"}",
                "{\"priority\":\"high\"}"));

        await using var factory = new HostApplicationFactory(services =>
        {
            services.AddScoped(_ => operationsQueries);
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/operations/{operationId}");
        request.Headers.Add(TestAuthenticationHandler.SubjectHeader, "subject-1");

        HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OperationDetails>(cancellationToken: CancellationToken.None);
        body.ShouldNotBeNull();
        body.OperationId.ShouldBe(operationId);
        body.Status.ShouldBe(OperationStatus.Completed);
    }

    [Fact]
    [Trait("Category", "Application")]
    public async Task GetOperation_WhenOperationDoesNotExist_ReturnsNotFound()
    {
        var operationsQueries = Substitute.For<IOperationsQueries>();
        operationsQueries.GetOperationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs((OperationDetails?)null);

        await using var factory = new HostApplicationFactory(services =>
        {
            services.AddScoped(_ => operationsQueries);
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/operations/33333333-3333-3333-3333-333333333333");
        request.Headers.Add(TestAuthenticationHandler.SubjectHeader, "subject-1");

        HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
