using CashFlow.Application.Abstractions;
using CashFlow.Domain.Common;
using Microsoft.Extensions.Logging;

namespace CashFlow.Application.Decorators;

public sealed class ConcurrencyRetryDecorator<TRequest, TResponse>(
    IUseCase<TRequest, TResponse> inner,
    ILogger<ConcurrencyRetryDecorator<TRequest, TResponse>> logger) : IUseCase<TRequest, TResponse>
{
    public const int MaxAttempts = 3;

    public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await inner.ExecuteAsync(request, cancellationToken);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                logger.LogWarning(
                    "Concurrency conflict executing {UseCase} (attempt {Attempt}/{MaxAttempts}); retrying",
                    typeof(TRequest).Name,
                    attempt,
                    MaxAttempts);
            }
        }
    }
}
