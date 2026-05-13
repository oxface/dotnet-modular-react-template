using Mediator;
using ModularTemplate.SharedKernel.Validation;

namespace ModularTemplate.Host.Configuration;

public sealed class RequestValidationBehavior<TCommand, TResponse>(
    IEnumerable<IRequestValidator<TCommand>> validators)
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : IBaseCommand
{
    public async ValueTask<TResponse> Handle(
        TCommand message,
        MessageHandlerDelegate<TCommand, TResponse> next,
        CancellationToken cancellationToken)
    {
        List<string> errors = [];

        foreach (IRequestValidator<TCommand> validator in validators)
        {
            IReadOnlyCollection<string> validatorErrors =
                await validator.ValidateAsync(message, cancellationToken);

            errors.AddRange(validatorErrors);
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        return await next(message, cancellationToken);
    }
}
