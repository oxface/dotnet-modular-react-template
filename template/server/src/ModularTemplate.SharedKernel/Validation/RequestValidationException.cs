namespace ModularTemplate.SharedKernel.Validation;

public sealed class RequestValidationException(IReadOnlyCollection<string> errors)
    : Exception("Request validation failed.")
{
    public IReadOnlyCollection<string> Errors { get; } = errors;
}
