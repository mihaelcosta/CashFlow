using CashFlow.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CashFlow.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2);

        builder.Property(t => t.BalanceAfter)
            .HasPrecision(18, 2);

        builder.Property(t => t.Description)
            .HasMaxLength(200);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.OccurredAt)
            .HasConversion<DateTimeOffsetToBinaryConverter>();

        builder.HasIndex(t => new { t.AccountId, t.Sequence })
            .IsUnique();

        builder.HasIndex(t => new { t.AccountId, t.OccurredAt });
    }
}
