using Bondstone.Commands;
using ModularTemplate.Host.Configuration;
using ModularTemplate.SharedKernel.Validation;
using Shouldly;

namespace ModularTemplate.Host.Tests.Configuration;

public sealed class RequestValidationBehaviorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValidatorsPass_CallsNextHandler()
    {
        var behavior = new RequestValidationBehavior<TestCommand, string>(
            [new PassingValidator()]);

        string result = await behavior.Handle(
            new TestCommand("valid"),
            (_, _) => new ValueTask<string>("handled"),
            CancellationToken.None);

        result.ShouldBe("handled");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenValidatorsReturnErrors_ThrowsValidationException()
    {
        var behavior = new RequestValidationBehavior<TestCommand, string>(
            [new FailingValidator("Name is required."), new FailingValidator("Name is too long.")]);
        bool nextWasCalled = false;

        RequestValidationException exception = await Should.ThrowAsync<RequestValidationException>(
            async () => await behavior.Handle(
                new TestCommand(""),
                (_, _) =>
                {
                    nextWasCalled = true;
                    return new ValueTask<string>("handled");
                },
                CancellationToken.None));

        exception.Errors.ShouldBe(["Name is required.", "Name is too long."]);
        nextWasCalled.ShouldBeFalse();
    }

    private sealed record TestCommand(string Name) : IModuleCommand<string>;

    private sealed class PassingValidator : IRequestValidator<TestCommand>
    {
        public ValueTask<IReadOnlyCollection<string>> ValidateAsync(
            TestCommand request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IReadOnlyCollection<string>>([]);
        }
    }

    private sealed class FailingValidator(string error) : IRequestValidator<TestCommand>
    {
        public ValueTask<IReadOnlyCollection<string>> ValidateAsync(
            TestCommand request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IReadOnlyCollection<string>>([error]);
        }
    }
}
