using ModularTemplate.SharedKernel.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Tests.Messaging;

public sealed class MessageTypeRegistryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenMessageTypeIsValid_ResolvesByClrTypeAndTypeName()
    {
        var registry = new MessageTypeRegistry();

        registry.Register<TestIntegrationEvent>("ModularTemplate.identity.user-created.v1");

        registry.GetMessageTypeName<TestIntegrationEvent>()
            .ShouldBe("ModularTemplate.identity.user-created.v1");
        registry.ResolveClrType("ModularTemplate.identity.user-created.v1")
            .ShouldBe(typeof(TestIntegrationEvent));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenTypeIsAlreadyRegisteredWithSameName_ReturnsSameRegistration()
    {
        var registry = new MessageTypeRegistry();

        MessageTypeRegistration first = registry.Register<TestDurableCommand>("ModularTemplate.identity.sync-user.v1");
        MessageTypeRegistration second = registry.Register<TestDurableCommand>("ModularTemplate.identity.sync-user.v1");

        second.ShouldBe(first);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenTypeIsAlreadyRegisteredWithDifferentName_Throws()
    {
        var registry = new MessageTypeRegistry();
        registry.Register<TestDurableCommand>("ModularTemplate.identity.sync-user.v1");

        Should.Throw<InvalidOperationException>(
            () => registry.Register<TestDurableCommand>("ModularTemplate.identity.sync-user.v2"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenMessageTypeNameIsAlreadyRegisteredForDifferentClrType_Throws()
    {
        var registry = new MessageTypeRegistry();
        registry.Register<TestDurableCommand>("ModularTemplate.identity.sync-user.v1");

        Should.Throw<InvalidOperationException>(
            () => registry.Register<TestIntegrationEvent>("ModularTemplate.identity.sync-user.v1"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveClrType_WhenTypeNameIsUnknown_Throws()
    {
        var registry = new MessageTypeRegistry();

        Should.Throw<KeyNotFoundException>(
            () => registry.ResolveClrType("ModularTemplate.identity.unknown.v1"));
    }

    private sealed record TestIntegrationEvent : IIntegrationEvent;

    private sealed record TestDurableCommand : IDurableCommand;
}
