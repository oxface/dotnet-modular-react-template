using ModularTemplate.SharedKernel.Extensions;

namespace ModularTemplate.Infrastructure.Persistence;

public sealed class ModuleUnitOfWorkContext : IModuleUnitOfWorkContext
{
    private readonly AsyncLocal<Stack<string>> _moduleStack = new();

    public string? CurrentModuleName => _moduleStack.Value?.TryPeek(out string? moduleName) == true
        ? moduleName
        : null;

    public IDisposable StartModuleScope(string moduleName)
    {
        string normalizedModuleName = moduleName.TrimRequired(nameof(moduleName));
        Stack<string> stack = _moduleStack.Value ??= new Stack<string>();
        if (stack.TryPeek(out string? currentModuleName)
            && !string.Equals(currentModuleName, normalizedModuleName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot enter module unit of work '{normalizedModuleName}' while module unit of work '{currentModuleName}' is active. " +
                "Use durable messaging for cross-module write work.");
        }

        stack.Push(normalizedModuleName);
        return new ModuleScope(this, normalizedModuleName);
    }

    private void ExitModule(string moduleName)
    {
        Stack<string>? stack = _moduleStack.Value;
        if (stack is null || stack.Count == 0)
        {
            throw new InvalidOperationException(
                $"No active module unit of work exists while leaving module '{moduleName}'.");
        }

        string currentModuleName = stack.Pop();
        if (!string.Equals(currentModuleName, moduleName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Active module unit of work '{currentModuleName}' does not match leaving module '{moduleName}'.");
        }
    }

    private sealed class ModuleScope(ModuleUnitOfWorkContext context, string moduleName) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            context.ExitModule(moduleName);
            _disposed = true;
        }
    }
}
