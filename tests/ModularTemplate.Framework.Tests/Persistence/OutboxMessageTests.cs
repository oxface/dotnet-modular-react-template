using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class OutboxMessageTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void MarkFailed_WhenErrorExceedsColumnLimit_TruncatesErrorSummary()
    {
        OutboxMessage message = OutboxMessage.Create(
            Guid.NewGuid(),
            MessageKind.Command,
            "test.command.v1",
            sourceModule: "identity",
            targetModule: "products",
            correlationId: Guid.NewGuid(),
            causationId: null,
            durableOperationId: null,
            payload: "{}");
        string longError = new('x', OutboxMessage.MaxErrorLength + 100);

        message.MarkFailed(longError, _ => TimeSpan.Zero);

        message.Error.ShouldNotBeNull();
        message.Error.Length.ShouldBe(OutboxMessage.MaxErrorLength);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenCommandHasNoTargetModule_Throws()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => OutboxMessage.Create(
                Guid.NewGuid(),
                MessageKind.Command,
                "test.command.v1",
                sourceModule: "identity",
                targetModule: null,
                correlationId: Guid.NewGuid(),
                causationId: null,
                durableOperationId: null,
                payload: "{}"));

        exception.Message.ShouldContain("target module");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenEventHasTargetModule_Throws()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => OutboxMessage.Create(
                Guid.NewGuid(),
                MessageKind.Event,
                "test.event.v1",
                sourceModule: "identity",
                targetModule: "products",
                correlationId: Guid.NewGuid(),
                causationId: null,
                durableOperationId: null,
                payload: "{}"));

        exception.Message.ShouldContain("must not specify a target module");
    }
}
