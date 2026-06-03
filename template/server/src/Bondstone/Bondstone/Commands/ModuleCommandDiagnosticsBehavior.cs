using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Bondstone.Messaging;

namespace Bondstone.Commands;

public sealed class ModuleCommandDiagnosticsBehavior<TCommand, TResult>(
    ILogger<ModuleCommandDiagnosticsBehavior<TCommand, TResult>> logger)
    : IModuleCommandPipelineBehavior<TCommand, TResult>
    where TCommand : IModuleCommand<TResult>
{
    public async ValueTask<TResult> HandleAsync(
        TCommand command,
        ModuleCommandHandlerDelegate<TCommand, TResult> next,
        CancellationToken cancellationToken)
    {
        string commandType = typeof(TCommand).FullName ?? typeof(TCommand).Name;
        using Activity? activity = BondstoneDiagnostics.StartActivity(
            $"Bondstone command {typeof(TCommand).Name}",
            ActivityKind.Internal);
        activity?.SetTag("bondstone.command.type", commandType);

        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug("Handling module command {CommandType}.", commandType);

        try
        {
            TResult result = await next(command, cancellationToken);
            logger.LogInformation(
                "Handled module command {CommandType} in {ElapsedMilliseconds} ms.",
                commandType,
                stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Module command {CommandType} was canceled after {ElapsedMilliseconds} ms.",
                commandType,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Module command {CommandType} failed after {ElapsedMilliseconds} ms.",
                commandType,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
