using CashFlow.Application.Abstractions;
using CashFlow.Application.Accounts.Commands;
using CashFlow.Application.Accounts.Queries;
using CashFlow.Application.Accounts.Results;
using CashFlow.Application.Common;
using CashFlow.Application.Decorators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CashFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddCommandWithConcurrencyRetry<CreateAccountCommand, AccountResult, CreateAccountUseCase>();
        services.AddCommandWithConcurrencyRetry<DepositCommand, TransactionResult, DepositUseCase>();
        services.AddCommandWithConcurrencyRetry<WithdrawCommand, TransactionResult, WithdrawUseCase>();

        services.AddScoped<IUseCase<GetBalanceQuery, BalanceResult>, GetBalanceUseCase>();
        services.AddScoped<IUseCase<GetTransactionsQuery, PagedResult<TransactionResult>>, GetTransactionsUseCase>();

        return services;
    }

    private static IServiceCollection AddCommandWithConcurrencyRetry<TRequest, TResponse, TUseCase>(
        this IServiceCollection services)
        where TUseCase : class, IUseCase<TRequest, TResponse>
    {
        services.AddScoped<TUseCase>();
        services.AddScoped<IUseCase<TRequest, TResponse>>(provider =>
            new ConcurrencyRetryDecorator<TRequest, TResponse>(
                provider.GetRequiredService<TUseCase>(),
                provider.GetRequiredService<ILogger<ConcurrencyRetryDecorator<TRequest, TResponse>>>()));

        return services;
    }
}
