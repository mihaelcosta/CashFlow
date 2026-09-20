using CashFlow.Application.Accounts.Commands;
using CashFlow.Application.Common;
using CashFlow.Domain.Accounts;
using CashFlow.Domain.Accounts.Exceptions;
using CashFlow.Domain.Common;
using CashFlow.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace CashFlow.Tests.Application;

public sealed class DepositUseCaseTests
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly DepositUseCase _useCase;

    public DepositUseCaseTests()
    {
        _useCase = new DepositUseCase(_accounts, _unitOfWork, _clock);
    }

    [Fact]
    public async Task Execute_PersistsCredit_ThenCommitsOnce()
    {
        // Arrange
        var account = AccountBuilder.Empty();
        _accounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        // Act
        var result = await _useCase.ExecuteAsync(new DepositCommand(account.Id, 2_500m, "Customer payment"));

        // Assert
        Assert.Equal(TransactionType.Credit, result.Type);
        Assert.Equal(2_500m, result.BalanceAfter);
        Assert.Equal(2_500m, account.Balance);

        _accounts.Received(1).Add(Arg.Is<Transaction>(t => t.Id == result.Id));
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithInvalidAmount_Throws_AndNothingIsPersisted()
    {
        // Arrange
        var account = AccountBuilder.Empty();
        _accounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        // Act
        var act = () => _useCase.ExecuteAsync(new DepositCommand(account.Id, 0m, null));

        // Assert
        await Assert.ThrowsAsync<InvalidAmountException>(act);
        _accounts.DidNotReceive().Add(Arg.Any<Transaction>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ForUnknownAccount_ThrowsNotFound()
    {
        // Arrange
        _accounts.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Account?)null);

        // Act
        var act = () => _useCase.ExecuteAsync(new DepositCommand(Guid.NewGuid(), 1m, null));

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
