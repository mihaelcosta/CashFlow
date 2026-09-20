using CashFlow.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CashFlow.Infrastructure.Persistence.Seeding;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<CashFlowDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        await context.Database.MigrateAsync(cancellationToken);

        if (await context.Accounts.AnyAsync(a => a.Id == DefaultAccount.Id, cancellationToken))
        {
            return;
        }

        context.Accounts.Add(new Account(DefaultAccount.Id, DefaultAccount.Name, clock.GetUtcNow()));
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Default account {AccountId} created", DefaultAccount.Id);
    }
}
