namespace ModularTemplate.SharedKernel.Validation;

public interface IRequestValidator<in TRequest>
{
    ValueTask<IReadOnlyCollection<string>> ValidateAsync(
        TRequest request,
        CancellationToken cancellationToken);
}
