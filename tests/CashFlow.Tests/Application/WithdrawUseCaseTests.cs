using CashFlow.Application.Accounts.Commands;
using CashFlow.Application.Common;
using CashFlow.Domain.Accounts;
using CashFlow.Domain.Accounts.Exceptions;
using CashFlow.Domain.Common;
using CashFlow.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace CashFlow.Tests.Application;

public sealed class WithdrawUseCaseTests
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly WithdrawUseCase _useCase;

    public WithdrawUseCaseTests()
    {
        _useCase = new WithdrawUseCase(_accounts, _unitOfWork, _clock);
    }

    [Fact]
    public async Task Execute_WithSufficientFunds_PersistsDebit_ThenCommitsOnce()
    {
        // Arrange
        var account = AccountBuilder.WithFunds(1_000m);
        _accounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        // Act
        var result = await _useCase.ExecuteAsync(new WithdrawCommand(account.Id, 300m, "Rent"));

        // Assert
        Assert.Equal(TransactionType.Debit, result.Type);
        Assert.Equal(300m, result.Amount);
        Assert.Equal(700m, result.BalanceAfter);
        Assert.Equal("Rent", result.Description);
        Assert.Equal(_clock.GetUtcNow(), result.OccurredAt);

        Received.InOrder(() =>
        {
            _accounts.Add(Arg.Is<Transaction>(t => t.Id == result.Id && t.AccountId == account.Id));
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_UsesInjectedClock_ForOccurredAt()
    {
        // Arrange
        var account = AccountBuilder.WithFunds(100m);
        _accounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _clock.Advance(TimeSpan.FromDays(3));

        // Act
        var result = await _useCase.ExecuteAsync(new WithdrawCommand(account.Id, 1m, null));

        // Assert
        Assert.Equal(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero), result.OccurredAt);
    }

    [Fact]
    public async Task Execute_WithInsufficientFunds_Throws_AndNothingIsPersisted()
    {
        // Arrange
        var account = AccountBuilder.WithFunds(10m);
        _accounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        // Act
        var act = () => _useCase.ExecuteAsync(new WithdrawCommand(account.Id, 10.01m, null));

        // Assert
        await Assert.ThrowsAsync<InsufficientFundsException>(act);
        Assert.Equal(10m, account.Balance);
        _accounts.DidNotReceive().Add(Arg.Any<Transaction>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ForUnknownAccount_ThrowsNotFound_WithoutTouchingUnitOfWork()
    {
        // Arrange
        var unknownId = Guid.NewGuid();
        _accounts.GetByIdAsync(unknownId, Arg.Any<CancellationToken>()).Returns((Account?)null);

        // Act
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _useCase.ExecuteAsync(new WithdrawCommand(unknownId, 1m, null)));

        // Assert
        Assert.Equal(nameof(Account), exception.Resource);
        Assert.Equal(unknownId, exception.Id);
        await _unitOfWork.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }

    [Fact]
    public async Task Execute_PropagatesCancellationToken_ToRepositoryAndUnitOfWork()
    {
        // Arrange
        var account = AccountBuilder.WithFunds(100m);
        using var cts = new CancellationTokenSource();
        _accounts.GetByIdAsync(account.Id, cts.Token).Returns(account);

        // Act
        await _useCase.ExecuteAsync(new WithdrawCommand(account.Id, 1m, null), cts.Token);

        // Assert
        await _accounts.Received(1).GetByIdAsync(account.Id, cts.Token);
        await _unitOfWork.Received(1).CommitAsync(cts.Token);
    }

    [Fact]
    public async Task Execute_WhenCommitFailsWithConcurrencyConflict_LetsItBubbleUp()
    {
        // Arrange
        var account = AccountBuilder.WithFunds(100m);
        _accounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConcurrencyConflictException("stale")));

        // Act
        var act = () => _useCase.ExecuteAsync(new WithdrawCommand(account.Id, 1m, null));

        // Assert
        await Assert.ThrowsAsync<ConcurrencyConflictException>(act);
    }
}
