using Betsson.OnlineWallets.Data.Models;
using Betsson.OnlineWallets.Data.Repositories;
using Betsson.OnlineWallets.Services;
using Moq;
using Xunit;

namespace Betsson.OnlineWallets.UnitTests.Services
{
    public class OnlineWalletServiceTests
    {
        private readonly Mock<IOnlineWalletRepository> _repositoryMock;
        private readonly OnlineWalletService _service;

        public OnlineWalletServiceTests()
        {
            _repositoryMock = new Mock<IOnlineWalletRepository>(MockBehavior.Strict);
            _service = new OnlineWalletService(_repositoryMock.Object);
        }

        [Fact]
        public async Task GetBalance_NewWalletWithNoHistory_ReturnsZero()
        {
            _repositoryMock
                .Setup(r => r.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync((OnlineWalletEntry?)null);

            var balance = await _service.GetBalanceAsync();

            Assert.Equal(0m, balance.Amount);
            _repositoryMock.Verify(r => r.GetLastOnlineWalletEntryAsync(), Times.Once);
        }

        [Fact]
        public async Task GetBalance_WalletWithPreviousTransactions_ReturnsSumOfLastEntryFields()
        {
            _repositoryMock
                .Setup(r => r.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync(new OnlineWalletEntry { BalanceBefore = 125.50m, Amount = -25.25m });

            var balance = await _service.GetBalanceAsync();

            Assert.Equal(100.25m, balance.Amount);
            _repositoryMock.Verify(r => r.GetLastOnlineWalletEntryAsync(), Times.Once);
        }

        [Fact]
        public async Task Deposit_WalletHasExistingBalance_AddsMoneyAndSavesTransaction()
        {
            OnlineWalletEntry? savedEntry = null;
            _repositoryMock
                .Setup(r => r.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync(new OnlineWalletEntry { BalanceBefore = 80m, Amount = 20m });
            _repositoryMock
                .Setup(r => r.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
                .Callback<OnlineWalletEntry>(e => savedEntry = e)
                .Returns(Task.CompletedTask);

            var beforeCall = DateTimeOffset.UtcNow;
            var balance = await _service.DepositFundsAsync(new Models.Deposit { Amount = 40m });
            var afterCall = DateTimeOffset.UtcNow;

            Assert.Equal(140m, balance.Amount);
            Assert.NotNull(savedEntry);
            Assert.Equal(40m, savedEntry.Amount);
            Assert.Equal(100m, savedEntry.BalanceBefore);
            Assert.InRange(savedEntry.EventTime, beforeCall, afterCall);
        }

        [Fact]
        public async Task Deposit_EmptyWalletFirstEverDeposit_StartsFromZeroBalance()
        {
            OnlineWalletEntry? savedEntry = null;
            _repositoryMock
                .Setup(r => r.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync((OnlineWalletEntry?)null);
            _repositoryMock
                .Setup(r => r.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
                .Callback<OnlineWalletEntry>(e => savedEntry = e)
                .Returns(Task.CompletedTask);

            var balance = await _service.DepositFundsAsync(new Models.Deposit { Amount = 25m });

            Assert.Equal(25m, balance.Amount);
            Assert.NotNull(savedEntry);
            Assert.Equal(25m, savedEntry.Amount);
            Assert.Equal(0m, savedEntry.BalanceBefore);
        }
    }
}
