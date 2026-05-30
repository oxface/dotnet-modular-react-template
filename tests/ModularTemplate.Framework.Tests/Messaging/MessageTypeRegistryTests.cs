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

    [Fact]
    [Trait("Category", "Unit")]
    public void Register_WhenMessageHasIdentityAttribute_UsesStableIdentity()
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
    public void RegisterFromAssembly_WhenMessagesHaveIdentityAttributes_RegistersStableIdentities()
    {
        var registry = new MessageTypeRegistry();

        IReadOnlyCollection<MessageTypeRegistration> registrations =
            registry.RegisterFromAssembly(typeof(AttributedCommand).Assembly);

        registrations.ShouldContain(registration => registration.ClrType == typeof(AttributedCommand)
            && registration.MessageTypeName == "identity.attributed-command.v1");
        registry.Registrations.ShouldContain(registration => registration.ClrType == typeof(AttributedCommand)
            && registration.MessageTypeName == "identity.attributed-command.v1");
        registry.ResolveClrType("identity.attributed-command.v1")
            .ShouldBe(typeof(AttributedCommand));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CommandSubmission_WhenAccepted_CarriesSubmissionStatusAndOptionalOperationIdOnly()
    {
        Guid submissionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid operationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var submission = new CommandSubmission(
            submissionId,
            operationId,
            CommandSubmissionStatus.Accepted);

        submission.SubmissionId.ShouldBe(submissionId);
        submission.OperationId.ShouldBe(operationId);
        submission.Status.ShouldBe(CommandSubmissionStatus.Accepted);
    }

    private sealed record TestIntegrationEvent : IIntegrationEvent;

    private sealed record TestDurableCommand : IDurableCommand;

    [MessageIdentity("identity.attributed-command.v1")]
    private sealed record AttributedCommand : IDurableCommand;
}
