using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModularTemplate.Infrastructure.Transport;
using NSubstitute;
using Shouldly;

namespace ModularTemplate.Host.Tests.Configuration;

public sealed class ServiceBusTransportStartupValidationHostedServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartAsync_WhenTransportIsInMemory_DoesNotInvokeProbe()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:Transport"] = "InMemory"
            })
            .Build();
        var environment = new TestHostEnvironment("Development");
        IServiceBusNamespaceProbe probe = Substitute.For<IServiceBusNamespaceProbe>();
        var service = new ServiceBusTransportStartupValidationHostedService(
            configuration,
            environment,
            probe,
            NullLogger<ServiceBusTransportStartupValidationHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        await probe.DidNotReceiveWithAnyArgs().ProbeAsync(default!, default);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartAsync_WhenAzureTransportMissingConnectionString_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestHostEnvironment("Development");
        IServiceBusNamespaceProbe probe = Substitute.For<IServiceBusNamespaceProbe>();
        var service = new ServiceBusTransportStartupValidationHostedService(
            configuration,
            environment,
            probe,
            NullLogger<ServiceBusTransportStartupValidationHostedService>.Instance);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.StartAsync(CancellationToken.None));

        exception.Message.ShouldContain("ConnectionStrings:service-bus");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartAsync_WhenAzureTransportProbeFails_ThrowsWrappedError()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:service-bus"] = "Endpoint=sb://localhost/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fake="
            })
            .Build();
        var environment = new TestHostEnvironment("Development");
        IServiceBusNamespaceProbe probe = Substitute.For<IServiceBusNamespaceProbe>();
        probe.ProbeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("probe failed")));
        var service = new ServiceBusTransportStartupValidationHostedService(
            configuration,
            environment,
            probe,
            NullLogger<ServiceBusTransportStartupValidationHostedService>.Instance);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.StartAsync(CancellationToken.None));

        exception.Message.ShouldContain("startup transport check failed");
        exception.InnerException.ShouldNotBeNull();
        exception.InnerException.Message.ShouldContain("probe failed");
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
