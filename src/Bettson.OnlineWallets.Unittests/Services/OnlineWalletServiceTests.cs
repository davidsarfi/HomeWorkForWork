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
    }
}
