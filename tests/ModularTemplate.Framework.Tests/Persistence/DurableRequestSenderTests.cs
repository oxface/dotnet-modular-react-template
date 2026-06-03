using System.Text.Json;
using Bondstone.Messaging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class DurableRequestSenderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SendAndWaitAsync_WhenOperationCompletes_ReturnsDeserializedResult()
    {
        Guid submissionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid durableOperationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        IDurableCommandSender commandSender = Substitute.For<IDurableCommandSender>();
        commandSender
            .Send(
                Arg.Any<TestRequestCommand>(),
                "products",
                Arg.Is<Guid?>(value => value == durableOperationId),
                Arg.Any<Guid?>(),
                Arg.Any<int?>())
            .Returns(new CommandSubmission(
                submissionId,
                durableOperationId,
                CommandSubmissionStatus.Accepted));
        IDurableOperationReader operationReader = Substitute.For<IDurableOperationReader>();
        operationReader
            .GetOperationAsync(durableOperationId, Arg.Any<CancellationToken>())
            .Returns(new DurableOperationSnapshot(
                durableOperationId,
                DurableOperationState.Completed,
                JsonSerializer.Serialize(new TestRequestResult("done")),
                FailureReason: null));
        var sender = new DurableRequestSender(
            commandSender,
            operationReader,
            Options.Create(new DurableRequestOptions
            {
                PollingInterval = TimeSpan.FromMilliseconds(1),
                DefaultTimeout = TimeSpan.FromSeconds(1)
            }));

        DurableRequestResult<TestRequestResult> result = await sender.SendAndWaitAsync<TestRequestCommand, TestRequestResult>(
            new TestRequestCommand("hello"),
            "products",
            durableOperationId,
            timeout: TimeSpan.FromSeconds(1),
            CancellationToken.None);

        result.SubmissionId.ShouldBe(submissionId);
        result.DurableOperationId.ShouldBe(durableOperationId);
        result.Status.ShouldBe(DurableRequestStatus.Completed);
        result.Result.ShouldBe(new TestRequestResult("done"));
        result.FailureReason.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SendAndWaitAsync_WhenOperationDoesNotCompleteBeforeTimeout_ReturnsTimedOut()
    {
        Guid durableOperationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        IDurableCommandSender commandSender = Substitute.For<IDurableCommandSender>();
        commandSender
            .Send(
                Arg.Any<TestRequestCommand>(),
                "products",
                Arg.Is<Guid?>(value => value == durableOperationId),
                Arg.Any<Guid?>(),
                Arg.Any<int?>())
            .Returns(new CommandSubmission(
                Guid.NewGuid(),
                durableOperationId,
                CommandSubmissionStatus.Accepted));
        IDurableOperationReader operationReader = Substitute.For<IDurableOperationReader>();
        operationReader
            .GetOperationAsync(durableOperationId, Arg.Any<CancellationToken>())
            .Returns(new DurableOperationSnapshot(
                durableOperationId,
                DurableOperationState.Running,
                ResultJson: null,
                FailureReason: null));
        var sender = new DurableRequestSender(
            commandSender,
            operationReader,
            Options.Create(new DurableRequestOptions
            {
                PollingInterval = TimeSpan.FromMilliseconds(1),
                DefaultTimeout = TimeSpan.FromMilliseconds(5)
            }));

        DurableRequestResult<TestRequestResult> result = await sender.SendAndWaitAsync<TestRequestCommand, TestRequestResult>(
            new TestRequestCommand("hello"),
            "products",
            durableOperationId,
            timeout: TimeSpan.FromMilliseconds(5),
            CancellationToken.None);

        result.DurableOperationId.ShouldBe(durableOperationId);
        result.Status.ShouldBe(DurableRequestStatus.TimedOut);
        result.Result.ShouldBeNull();
    }

    private sealed record TestRequestCommand(string Value) : IDurableCommand;

    private sealed record TestRequestResult(string Value);
}
