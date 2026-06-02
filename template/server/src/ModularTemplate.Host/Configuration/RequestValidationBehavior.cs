using Bondstone.Commands;
using ModularTemplate.SharedKernel.Validation;

namespace ModularTemplate.Host.Configuration;

public sealed class RequestValidationBehavior<TCommand, TResponse>(
    IEnumerable<IRequestValidator<TCommand>> validators)
    : IModuleCommandPipelineBehavior<TCommand, TResponse>
    where TCommand : IModuleCommand<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TCommand message,
        ModuleCommandHandlerDelegate<TCommand, TResponse> next,
        CancellationToken cancellationToken)
    {
        return await HandleAsync(message, next, cancellationToken);
    }

    public async ValueTask<TResponse> HandleAsync(
        TCommand command,
        ModuleCommandHandlerDelegate<TCommand, TResponse> next,
        CancellationToken cancellationToken)
    {
        List<string> errors = [];

        foreach (IRequestValidator<TCommand> validator in validators)
        {
            IReadOnlyCollection<string> validatorErrors =
                await validator.ValidateAsync(command, cancellationToken);

            errors.AddRange(validatorErrors);
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        return await next(command, cancellationToken);
    }
}
