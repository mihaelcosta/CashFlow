using CashFlow.Application.Common;
using CashFlow.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Api.ErrorHandling;

internal sealed class ApplicationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = Map(exception);
        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }

    private static ProblemDetails? Map(Exception exception) => exception switch
    {
        NotFoundException => Problem(StatusCodes.Status404NotFound, "Resource not found", exception),
        ConcurrencyConflictException => Problem(StatusCodes.Status409Conflict, "Concurrent update conflict", exception),
        DomainException => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", exception),
        _ => null
    };

    private static ProblemDetails Problem(int status, string title, Exception exception) => new()
    {
        Status = status,
        Title = title,
        Detail = exception.Message,
        Extensions = { ["errorType"] = exception.GetType().Name }
    };
}
