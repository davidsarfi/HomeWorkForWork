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

        [Fact]
        public async Task Deposit_ZeroAmount_ProcessesNormallyAndBalanceStaysTheSame()
        {
            OnlineWalletEntry? savedEntry = null;
            _repositoryMock
                .Setup(r => r.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync(new OnlineWalletEntry { BalanceBefore = 30m, Amount = 20m });
            _repositoryMock
                .Setup(r => r.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
                .Callback<OnlineWalletEntry>(e => savedEntry = e)
                .Returns(Task.CompletedTask);

            var balance = await _service.DepositFundsAsync(new Models.Deposit { Amount = 0m });

            Assert.Equal(50m, balance.Amount);
            Assert.NotNull(savedEntry);
            Assert.Equal(0m, savedEntry.Amount);
            Assert.Equal(50m, savedEntry.BalanceBefore);
        }

        [Fact]
        public async Task Deposit_TinyPennyAmount_HandlesSmallDecimalsCorrectly()
        {
            OnlineWalletEntry? savedEntry = null;
            _repositoryMock
                .Setup(r => r.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync(new OnlineWalletEntry { BalanceBefore = 50.50m, Amount = 49.49m });
            _repositoryMock
                .Setup(r => r.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
                .Callback<OnlineWalletEntry>(e => savedEntry = e)
                .Returns(Task.CompletedTask);

            var balance = await _service.DepositFundsAsync(new Models.Deposit { Amount = 0.01m });

            Assert.Equal(100.00m, balance.Amount);
            Assert.NotNull(savedEntry);
            Assert.Equal(0.01m, savedEntry.Amount);
            Assert.Equal(99.99m, savedEntry.BalanceBefore);
        }

        [Fact]
        public async Task Deposit_MaxDecimalAmount_OnZeroBalance_ReturnsMaxValue()
        {
            _repositoryMock
                .Setup(r => r.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync((OnlineWalletEntry?)null);
            _repositoryMock
                .Setup(r => r.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
                .Returns(Task.CompletedTask);

            var balance = await _service.DepositFundsAsync(new Models.Deposit { Amount = decimal.MaxValue });

            Assert.Equal(decimal.MaxValue, balance.Amount);
        }

        [Fact]
        public async Task Deposit_MaxDecimalAmount_OnNonZeroBalance_ThrowsOverflowException()
        {
            _repositoryMock
                .Setup(r => r.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync(new OnlineWalletEntry { BalanceBefore = 1m, Amount = 0m });
            _repositoryMock
                .Setup(r => r.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsAsync<OverflowException>(() =>
                _service.DepositFundsAsync(new Models.Deposit { Amount = decimal.MaxValue }));
        }

        [Fact]
        public async Task Withdraw_EnoughMoneyInWallet_SubtractsAndSavesNegativeAmount()
        {
            OnlineWalletEntry? savedEntry = null;
            _repositoryMock
                .Setup(r => r.GetLastOnlineWalletEntryAsync())
                .ReturnsAsync(new OnlineWalletEntry { BalanceBefore = 120m, Amount = 30m });
            _repositoryMock
                .Setup(r => r.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
                .Callback<OnlineWalletEntry>(e => savedEntry = e)
                .Returns(Task.CompletedTask);

            var balance = await _service.WithdrawFundsAsync(new Models.Withdrawal { Amount = 20m });

            Assert.Equal(130m, balance.Amount);
            Assert.NotNull(savedEntry);
            Assert.Equal(-20m, savedEntry.Amount);
            Assert.Equal(150m, savedEntry.BalanceBefore);
            _repositoryMock.Verify(r => r.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()), Times.Once);
        }
    }
}
