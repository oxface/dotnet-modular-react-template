using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Users;
using ModularTemplate.Operations.Infrastructure.Persistence;
using ModularTemplate.Operations.Operations;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.SharedKernel.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class ModuleUnitOfWorkTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveChangesTransactionalAsync_WhenMultipleModuleContextsChanged_ThrowsBoundaryViolation()
    {
        await using var identityContext = CreateIdentityContext();
        await using var operationsContext = CreateOperationsContext();
        await using ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        var unitOfWork = new ModuleUnitOfWork(
            [(IModuleDbContext)identityContext, (IModuleDbContext)operationsContext],
            serviceProvider,
            new MessageTypeRegistry());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () =>
            {
                identityContext.LocalUsers.Add(
                    LocalUser.Create("oidc", "subject-1", "Ada", "ada@example.test"));
                operationsContext.Operations.Add(Operation.Create("test-operation"));
                await unitOfWork.SaveChangesTransactionalAsync(CancellationToken.None);
            });

        exception.Message.ShouldContain("more than one module DbContext");
        exception.Message.ShouldContain("identity");
        exception.Message.ShouldContain("operations");
    }

    private static IdentityDbContext CreateIdentityContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used")
            .Options;

        return new IdentityDbContext(options);
    }

    private static OperationsDbContext CreateOperationsContext()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used")
            .Options;

        return new OperationsDbContext(options);
    }
}
