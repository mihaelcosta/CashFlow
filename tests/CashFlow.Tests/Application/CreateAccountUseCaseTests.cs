using CashFlow.Application.Accounts.Commands;
using CashFlow.Domain.Accounts;
using CashFlow.Domain.Common;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace CashFlow.Tests.Application;

public sealed class CreateAccountUseCaseTests
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly CreateAccountUseCase _useCase;

    public CreateAccountUseCaseTests()
    {
        _useCase = new CreateAccountUseCase(_accounts, _unitOfWork, _clock);
    }

    [Fact]
    public async Task Execute_CreatesAccount_StampedWithClockTime_AndCommits()
    {
        // Act
        var result = await _useCase.ExecuteAsync(new CreateAccountCommand("  Acme Ltda  "));

        // Assert
        Assert.Equal("Acme Ltda", result.Name);
        Assert.Equal(0m, result.Balance);
        Assert.Equal(_clock.GetUtcNow(), result.CreatedAt);
        Assert.NotEqual(Guid.Empty, result.Id);

        _accounts.Received(1).Add(Arg.Is<Account>(a => a.Id == result.Id && a.Name == "Acme Ltda"));
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithBlankName_Throws_AndDoesNotCommit()
    {
        // Act
        var act = () => _useCase.ExecuteAsync(new CreateAccountCommand(" "));

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
        _accounts.DidNotReceive().Add(Arg.Any<Account>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
