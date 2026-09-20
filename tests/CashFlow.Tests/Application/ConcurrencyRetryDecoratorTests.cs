using CashFlow.Application.Abstractions;
using CashFlow.Application.Decorators;
using CashFlow.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CashFlow.Tests.Application;

public sealed class ConcurrencyRetryDecoratorTests
{
    private const int MaxAttempts = ConcurrencyRetryDecorator<string, string>.MaxAttempts;

    private readonly IUseCase<string, string> _inner = Substitute.For<IUseCase<string, string>>();
    private readonly FakeLogger<ConcurrencyRetryDecorator<string, string>> _logger = new();
    private readonly ConcurrencyRetryDecorator<string, string> _decorator;

    public ConcurrencyRetryDecoratorTests()
    {
        _decorator = new ConcurrencyRetryDecorator<string, string>(_inner, _logger);
    }

    [Fact]
    public async Task Execute_WhenInnerSucceeds_CallsItOnce_AndLogsNothing()
    {
        // Arrange
        _inner.ExecuteAsync("request", Arg.Any<CancellationToken>()).Returns("ok");

        // Act
        var result = await _decorator.ExecuteAsync("request");

        // Assert
        Assert.Equal("ok", result);
        await _inner.Received(1).ExecuteAsync("request", Arg.Any<CancellationToken>());
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task Execute_WhenConflictIsTransient_RetriesUntilSuccess_AndLogsEachRetry()
    {
        // Arrange
        _inner.ExecuteAsync("request", Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new ConcurrencyConflictException("stale"),
                _ => throw new ConcurrencyConflictException("stale again"),
                _ => "ok");

        // Act
        var result = await _decorator.ExecuteAsync("request");

        // Assert
        Assert.Equal("ok", result);
        await _inner.Received(3).ExecuteAsync("request", Arg.Any<CancellationToken>());

        var logs = _logger.Collector.GetSnapshot();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, log => Assert.Equal(LogLevel.Warning, log.Level));
        Assert.Contains("attempt 1/3", logs[0].Message);
        Assert.Contains("attempt 2/3", logs[1].Message);
    }

    [Fact]
    public async Task Execute_WhenConflictPersists_GivesUpAfterMaxAttempts_AndRethrowsTheLastConflict()
    {
        // Arrange
        _inner.ExecuteAsync("request", Arg.Any<CancellationToken>()).ThrowsAsync(new ConcurrencyConflictException("stale"));

        // Act
        var act = () => _decorator.ExecuteAsync("request");

        // Assert
        await Assert.ThrowsAsync<ConcurrencyConflictException>(act);
        await _inner.Received(MaxAttempts).ExecuteAsync("request", Arg.Any<CancellationToken>());
        Assert.Equal(MaxAttempts - 1, _logger.Collector.Count);
    }

    [Fact]
    public async Task Execute_DoesNotRetry_BusinessOrUnexpectedExceptions()
    {
        // Arrange
        _inner.ExecuteAsync("request", Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var act = () => _decorator.ExecuteAsync("request");

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
        await _inner.Received(1).ExecuteAsync("request", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_DoesNotRetry_WhenTheCallerCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _inner.ExecuteAsync("request", cts.Token).ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var act = () => _decorator.ExecuteAsync("request", cts.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(act);
        await _inner.Received(1).ExecuteAsync("request", cts.Token);
    }

    [Fact]
    public async Task Execute_PassesTheSameCancellationToken_OnEveryAttempt()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _inner.ExecuteAsync("request", cts.Token)
            .Returns(_ => throw new ConcurrencyConflictException("stale"), _ => "ok");

        // Act
        await _decorator.ExecuteAsync("request", cts.Token);

        // Assert
        await _inner.Received(2).ExecuteAsync("request", cts.Token);
    }
}
