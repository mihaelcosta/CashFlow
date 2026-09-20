using CashFlow.Application.Abstractions;
using CashFlow.Domain.Accounts;
using CashFlow.Domain.Common;
using CashFlow.Infrastructure.Persistence;
using CashFlow.Infrastructure.Persistence.Queries;
using CashFlow.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CashFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<CashFlowDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseSqlite(database.ConnectionString);
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CashFlowDbContext>());
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionHistoryReader, TransactionHistoryReader>();

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
