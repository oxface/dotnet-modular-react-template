using System.Text.Json;
using Microsoft.Extensions.Options;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Outbox;

public sealed class DurableRequestSender(
    IDurableCommandSender durableCommandSender,
    IDurableOperationReader operationReader,
    IOptions<DurableRequestOptions> options)
    : IDurableRequestSender
{
    private readonly DurableRequestOptions _options = options.Value;

    public async Task<DurableRequestResult<TResult>> SendAndWaitAsync<TCommand, TResult>(
        TCommand command,
        string targetModule,
        Guid? durableOperationId = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TCommand : IDurableCommand
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetModule);

        TimeSpan requestTimeout = timeout ?? _options.DefaultTimeout;
        if (requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");
        }

        if (_options.PollingInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(DurableRequestOptions.PollingInterval)} must be greater than zero.");
        }

        Guid requestedDurableOperationId = durableOperationId ?? Guid.NewGuid();
        CommandSubmission submission = durableCommandSender.Send(
            command,
            targetModule,
            durableOperationId: requestedDurableOperationId);
        Guid effectiveDurableOperationId = submission.DurableOperationId ?? requestedDurableOperationId;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(requestTimeout);

        while (true)
        {
            DurableOperationSnapshot? operation = await operationReader.GetOperationAsync(
                effectiveDurableOperationId,
                cancellationToken);

            DurableRequestResult<TResult>? completedResult = TryCreateCompletedResult<TResult>(
                submission,
                effectiveDurableOperationId,
                operation);
            if (completedResult is not null)
            {
                return completedResult;
            }

            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return new DurableRequestResult<TResult>(
                    submission.SubmissionId,
                    effectiveDurableOperationId,
                    DurableRequestStatus.TimedOut,
                    Result: default,
                    FailureReason: null);
            }

            TimeSpan delay = remaining < _options.PollingInterval
                ? remaining
                : _options.PollingInterval;
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static DurableRequestResult<TResult>? TryCreateCompletedResult<TResult>(
        CommandSubmission submission,
        Guid durableOperationId,
        DurableOperationSnapshot? operation)
    {
        if (operation is null)
        {
            return null;
        }

        return operation.State switch
        {
            DurableOperationState.Completed => new DurableRequestResult<TResult>(
                submission.SubmissionId,
                durableOperationId,
                DurableRequestStatus.Completed,
                DeserializeResult<TResult>(operation.ResultJson),
                FailureReason: null),
            DurableOperationState.Failed => new DurableRequestResult<TResult>(
                submission.SubmissionId,
                durableOperationId,
                DurableRequestStatus.Failed,
                Result: default,
                operation.FailureReason),
            DurableOperationState.Cancelled => new DurableRequestResult<TResult>(
                submission.SubmissionId,
                durableOperationId,
                DurableRequestStatus.Cancelled,
                Result: default,
                FailureReason: null),
            _ => null
        };
    }

    private static TResult? DeserializeResult<TResult>(string? resultJson)
    {
        return string.IsNullOrWhiteSpace(resultJson)
            ? default
            : JsonSerializer.Deserialize<TResult>(resultJson);
    }
}
