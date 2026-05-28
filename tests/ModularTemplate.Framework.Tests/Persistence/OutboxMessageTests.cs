using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.SharedKernel.Messaging;
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
            targetModule: "operations",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{}");
        string longError = new('x', OutboxMessage.MaxErrorLength + 100);

        message.MarkFailed(longError, _ => TimeSpan.Zero);

        message.Error.ShouldNotBeNull();
        message.Error.Length.ShouldBe(OutboxMessage.MaxErrorLength);
    }
}
