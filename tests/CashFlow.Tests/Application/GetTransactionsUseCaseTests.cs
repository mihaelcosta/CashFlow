using CashFlow.Application.Abstractions;
using CashFlow.Application.Accounts.Queries;
using CashFlow.Application.Accounts.Results;
using CashFlow.Application.Common;
using CashFlow.Domain.Accounts;
using CashFlow.Tests.Builders;
using NSubstitute;

namespace CashFlow.Tests.Application;

public sealed class GetTransactionsUseCaseTests
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ITransactionHistoryReader _history = Substitute.For<ITransactionHistoryReader>();
    private readonly GetTransactionsUseCase _useCase;

    public GetTransactionsUseCaseTests()
    {
        _useCase = new GetTransactionsUseCase(_accounts, _history);
    }

    [Fact]
    public async Task Execute_ForExistingAccount_DelegatesTheExactQueryToTheReader()
    {
        // Arrange
        var account = AccountBuilder.Empty();
        var query = new GetTransactionsQuery(account.Id, TransactionType.Debit, null, null, Page: 2, PageSize: 10);
        var expected = new PagedResult<TransactionResult>([], 2, 10, 0);

        _accounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _history.ReadAsync(query, Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        var result = await _useCase.ExecuteAsync(query);

        // Assert
        Assert.Same(expected, result);
        await _history.Received(1).ReadAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ForUnknownAccount_ThrowsNotFound_AndNeverHitsTheReader()
    {
        // Arrange
        var query = new GetTransactionsQuery(Guid.NewGuid(), null, null, null, 1, 20);
        _accounts.GetByIdAsync(query.AccountId, Arg.Any<CancellationToken>()).Returns((Account?)null);

        // Act
        var act = () => _useCase.ExecuteAsync(query);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
        await _history.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
    }
}
