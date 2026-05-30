using Rebus.Messages;
using Rebus.Pipeline;

namespace ModularTemplate.Infrastructure.Transport;

internal sealed class ReceivingModuleHeaderStep(string moduleName) : IIncomingStep
{
    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        TransportMessage transportMessage = context.Load<TransportMessage>()
            ?? throw new InvalidOperationException(
                $"No Rebus transport message is available while entering module queue '{moduleName}'.");

        transportMessage.Headers[RebusMessageHeaders.ReceivingModule] = moduleName;
        await next();
    }
}
