using CashFlow.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CashFlow.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Balance)
            .HasPrecision(18, 2);

        builder.Property(a => a.Version)
            .IsConcurrencyToken();

        builder.Property(a => a.CreatedAt)
            .HasConversion<DateTimeOffsetToBinaryConverter>();
    }
}
