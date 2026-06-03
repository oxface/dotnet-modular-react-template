using Bondstone.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Tests.Messaging;

public sealed class MessageTypeRegistryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenMessageTypeIsValid_ResolvesByClrTypeAndTypeName()
    {
        var registry = new MessageTypeRegistry();

        registry.Register<TestIntegrationEvent>("identity.user-created.v1");

        registry.GetMessageTypeName<TestIntegrationEvent>()
            .ShouldBe("identity.user-created.v1");
        registry.ResolveClrType("identity.user-created.v1")
            .ShouldBe(typeof(TestIntegrationEvent));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenTypeIsAlreadyRegisteredWithSameName_ReturnsSameRegistration()
    {
        var registry = new MessageTypeRegistry();

        MessageTypeRegistration first = registry.Register<TestDurableCommand>("identity.sync-user.v1");
        MessageTypeRegistration second = registry.Register<TestDurableCommand>("identity.sync-user.v1");

        second.ShouldBe(first);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenTypeIsAlreadyRegisteredWithDifferentName_Throws()
    {
        var registry = new MessageTypeRegistry();
        registry.Register<TestDurableCommand>("identity.sync-user.v1");

        Should.Throw<InvalidOperationException>(
            () => registry.Register<TestDurableCommand>("identity.sync-user.v2"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenMessageTypeNameIsAlreadyRegisteredForDifferentClrType_Throws()
    {
        var registry = new MessageTypeRegistry();
        registry.Register<TestDurableCommand>("identity.sync-user.v1");

        Should.Throw<InvalidOperationException>(
            () => registry.Register<TestIntegrationEvent>("identity.sync-user.v1"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveClrType_WhenTypeNameIsUnknown_Throws()
    {
        var registry = new MessageTypeRegistry();

        Should.Throw<KeyNotFoundException>(
            () => registry.ResolveClrType("identity.unknown.v1"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenDurableCommandHasIdentityAttribute_UsesStableIdentity()
    {
        var registry = new MessageTypeRegistry();

        registry.Register<AttributedCommand>();

        registry.GetMessageTypeName<AttributedCommand>()
            .ShouldBe("identity.attributed-command.v1");
        registry.ResolveClrType("identity.attributed-command.v1")
            .ShouldBe(typeof(AttributedCommand));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenIntegrationEventHasIdentityAttribute_UsesStableIdentity()
    {
        var registry = new MessageTypeRegistry();

        registry.Register<AttributedIntegrationEvent>();

        registry.GetMessageTypeName<AttributedIntegrationEvent>()
            .ShouldBe("identity.attributed-event.v1");
        registry.ResolveClrType("identity.attributed-event.v1")
            .ShouldBe(typeof(AttributedIntegrationEvent));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenDurableCommandUsesIntegrationEventIdentity_Throws()
    {
        var registry = new MessageTypeRegistry();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.Register<CommandWithEventIdentity>());

        exception.Message.ShouldContain(nameof(DurableCommandIdentityAttribute));
        exception.Message.ShouldContain(nameof(IntegrationEventIdentityAttribute));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenIntegrationEventUsesDurableCommandIdentity_Throws()
    {
        var registry = new MessageTypeRegistry();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => registry.Register<EventWithCommandIdentity>());

        exception.Message.ShouldContain(nameof(IntegrationEventIdentityAttribute));
        exception.Message.ShouldContain(nameof(DurableCommandIdentityAttribute));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RegisterFromAssembly_WhenMessagesHaveIdentityAttributes_RegistersStableIdentities()
    {
        var registry = new MessageTypeRegistry();

        IReadOnlyCollection<MessageTypeRegistration> registrations =
            registry.RegisterFromAssembly(typeof(AttributedCommand).Assembly);

        registrations.ShouldContain(registration => registration.ClrType == typeof(AttributedCommand)
            && registration.MessageTypeName == "identity.attributed-command.v1");
        registrations.ShouldContain(registration => registration.ClrType == typeof(AttributedIntegrationEvent)
            && registration.MessageTypeName == "identity.attributed-event.v1");
        registry.Registrations.ShouldContain(registration => registration.ClrType == typeof(AttributedCommand)
            && registration.MessageTypeName == "identity.attributed-command.v1");
        registry.Registrations.ShouldContain(registration => registration.ClrType == typeof(AttributedIntegrationEvent)
            && registration.MessageTypeName == "identity.attributed-event.v1");
        registry.ResolveClrType("identity.attributed-command.v1")
            .ShouldBe(typeof(AttributedCommand));
        registry.ResolveClrType("identity.attributed-event.v1")
            .ShouldBe(typeof(AttributedIntegrationEvent));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CommandSubmission_WhenAccepted_CarriesSubmissionStatusAndOptionalDurableOperationIdOnly()
    {
        Guid submissionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid durableOperationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var submission = new CommandSubmission(
            submissionId,
            durableOperationId,
            CommandSubmissionStatus.Accepted);

        submission.SubmissionId.ShouldBe(submissionId);
        submission.DurableOperationId.ShouldBe(durableOperationId);
        submission.Status.ShouldBe(CommandSubmissionStatus.Accepted);
    }

    private sealed record TestIntegrationEvent : IIntegrationEvent;

    private sealed record TestDurableCommand : IDurableCommand;

    [DurableCommandIdentity("identity.attributed-command.v1")]
    private sealed record AttributedCommand : IDurableCommand;

    [IntegrationEventIdentity("identity.attributed-event.v1")]
    private sealed record AttributedIntegrationEvent : IIntegrationEvent;

    [IntegrationEventIdentity("identity.wrong-command.v1")]
    private sealed record CommandWithEventIdentity : IDurableCommand;

    [DurableCommandIdentity("identity.wrong-event.v1")]
    private sealed record EventWithCommandIdentity : IIntegrationEvent;
}
