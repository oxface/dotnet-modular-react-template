using Bondstone.Commands;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleCommandBus_WhenValidationFails_DoesNotEnterLaterTransactionBehavior()
    {
        var services = new ServiceCollection();
        var recorder = new PipelineRecorder();
        services.AddSingleton(recorder);
        services.AddScoped<IRequestValidator<TestCommand>>(_ => new FailingValidator("Name is required."));
        services.AddModuleCommands(options =>
        {
            options.AssemblyMarkers.Add(typeof(TestCommandHandler));
            options.PipelineBehaviors.Add(typeof(RecordingTransactionBehavior<,>));
            options.PipelineBehaviors.Add(typeof(RequestValidationBehavior<,>));
        });

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IModuleCommandBus commandBus = serviceProvider.GetRequiredService<IModuleCommandBus>();

        RequestValidationException exception = await Should.ThrowAsync<RequestValidationException>(
            async () => await commandBus.SendAsync(new TestCommand(""), CancellationToken.None));

        exception.Errors.ShouldBe(["Name is required."]);
        recorder.TransactionWasEntered.ShouldBeFalse();
    }

    private sealed record TestCommand(string Name) : IModuleCommand<string>;

    private sealed class TestCommandHandler : IModuleCommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            return new ValueTask<string>("handled");
        }
    }

    private sealed class RecordingTransactionBehavior<TCommand, TResult>(
        PipelineRecorder recorder)
        : IModuleCommandPipelineBehavior<TCommand, TResult>
        where TCommand : IModuleCommand<TResult>
    {
        public ValueTask<TResult> HandleAsync(
            TCommand command,
            ModuleCommandHandlerDelegate<TCommand, TResult> next,
            CancellationToken cancellationToken)
        {
            recorder.TransactionWasEntered = true;
            return next(command, cancellationToken);
        }
    }

    private sealed class PipelineRecorder
    {
        public bool TransactionWasEntered { get; set; }
    }

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
