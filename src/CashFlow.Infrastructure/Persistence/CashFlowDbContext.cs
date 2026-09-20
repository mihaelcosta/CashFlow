using CashFlow.Domain.Accounts;
using CashFlow.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Infrastructure.Persistence;

public sealed class CashFlowDbContext(DbContextOptions<CashFlowDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            ChangeTracker.Clear();
            throw new ConcurrencyConflictException(
                "The account was modified by another operation. The unit of work was discarded.", ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CashFlowDbContext).Assembly);
}
