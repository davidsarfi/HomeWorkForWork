using System.Net;
using System.Net.Http.Json;
using Betsson.OnlineWallets.Web.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Bettson.OnlineWallets.ApiTests
{
    public class OnlineWalletFreshWalletTests : IClassFixture<WebApplicationFactory<Betsson.OnlineWallets.Web.Startup>>
    {
        private readonly HttpClient _client;

        public OnlineWalletFreshWalletTests(WebApplicationFactory<Betsson.OnlineWallets.Web.Startup> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetBalance_BrandNewWallet_ReturnsZero()
        {
            var response = await _client.GetAsync("/onlinewallet/balance");

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            Assert.NotNull(body);
            // ReSharper disable once PossibleNullReferenceException — guarded by Assert.NotNull above
            Assert.Equal(0m, body!.Amount);
        }
    }

    public class OnlineWalletBalanceTests : IClassFixture<WebApplicationFactory<Betsson.OnlineWallets.Web.Startup>>
    {
        private readonly HttpClient _client;

        public OnlineWalletBalanceTests(WebApplicationFactory<Betsson.OnlineWallets.Web.Startup> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetBalance_AfterDeposit_ReflectsDepositedAmount()
        {
            var deposit = new DepositRequest { Amount = 75m };
            await _client.PostAsJsonAsync("/onlinewallet/deposit", deposit);

            var response = await _client.GetAsync("/onlinewallet/balance");

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            Assert.NotNull(body);
            Assert.True(body!.Amount >= 75m);
        }

        [Fact]
        public async Task GetBalance_ResponseIsJson_WithAmountProperty()
        {
            var response = await _client.GetAsync("/onlinewallet/balance");

            response.EnsureSuccessStatusCode();
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("amount", content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Deposit_ValidAmount_ReturnsOkWithNewBalance()
        {
            var balanceResponse = await _client.GetAsync("/onlinewallet/balance");
            var balanceBefore = (await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>())!.Amount;

            var response = await _client.PostAsJsonAsync("/onlinewallet/deposit", new DepositRequest { Amount = 50m });

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            Assert.NotNull(body);
            Assert.Equal(balanceBefore + 50m, body!.Amount);
        }

        [Fact]
        public async Task Deposit_ZeroAmount_ReturnsOkButBalanceUnchanged()
        {
            var balanceResponse = await _client.GetAsync("/onlinewallet/balance");
            var balanceBefore = (await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>())!.Amount;

            var response = await _client.PostAsJsonAsync("/onlinewallet/deposit", new DepositRequest { Amount = 0m });

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            Assert.NotNull(body);
            Assert.Equal(balanceBefore, body!.Amount);
        }

        [Fact]
        public async Task Deposit_NegativeAmount_ReturnsBadRequest()
        {
            var response = await _client.PostAsJsonAsync("/onlinewallet/deposit", new DepositRequest { Amount = -10m });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Deposit_SmallDecimalAmount_HandlesPenniesCorrectly()
        {
            var balanceResponse = await _client.GetAsync("/onlinewallet/balance");
            var balanceBefore = (await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>())!.Amount;

            var response = await _client.PostAsJsonAsync("/onlinewallet/deposit", new DepositRequest { Amount = 0.01m });

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            Assert.NotNull(body);
            Assert.Equal(balanceBefore + 0.01m, body!.Amount);
        }

        [Fact]
        public async Task Deposit_LargeAmount_HandlesItWithoutProblems()
        {
            var balanceResponse = await _client.GetAsync("/onlinewallet/balance");
            var balanceBefore = (await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>())!.Amount;

            var response = await _client.PostAsJsonAsync("/onlinewallet/deposit", new DepositRequest { Amount = 1_000_000m });

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            Assert.NotNull(body);
            Assert.Equal(balanceBefore + 1_000_000m, body!.Amount);
        }

        [Fact]
        public async Task Deposit_NullBody_ReturnsUnsupportedMediaType()
        {
            var response = await _client.PostAsync("/onlinewallet/deposit", null);

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }

        [Fact]
        public async Task Withdraw_EnoughMoney_ReturnsOkWithReducedBalance()
        {
            await _client.PostAsJsonAsync("/onlinewallet/deposit", new DepositRequest { Amount = 200m });
            var balanceResponse = await _client.GetAsync("/onlinewallet/balance");
            var balanceBefore = (await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>())!.Amount;

            var response = await _client.PostAsJsonAsync("/onlinewallet/withdraw", new WithdrawalRequest { Amount = 30m });

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<BalanceResponse>();
            Assert.NotNull(body);
            Assert.Equal(balanceBefore - 30m, body!.Amount);
        }

        [Fact]
        public async Task Withdraw_MoreThanBalance_ReturnsBadRequest()
        {
            var balanceResponse = await _client.GetAsync("/onlinewallet/balance");
            var currentBalance = (await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>())!.Amount;

            var response = await _client.PostAsJsonAsync("/onlinewallet/withdraw",
                new WithdrawalRequest { Amount = currentBalance + 999_999m });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Withdraw_MoreThanBalance_ResponseContainsInsufficientFundsMessage()
        {
            var balanceResponse = await _client.GetAsync("/onlinewallet/balance");
            var currentBalance = (await balanceResponse.Content.ReadFromJsonAsync<BalanceResponse>())!.Amount;

            var response = await _client.PostAsJsonAsync("/onlinewallet/withdraw",
                new WithdrawalRequest { Amount = currentBalance + 999_999m });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("insufficient", content, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Withdraw_NegativeAmount_ReturnsBadRequest()
        {
            var response = await _client.PostAsJsonAsync("/onlinewallet/withdraw", new WithdrawalRequest { Amount = -5m });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Withdraw_MaxDecimalValue_DoesNotOverflowOrCrash()
        {
            var response = await _client.PostAsJsonAsync("/onlinewallet/withdraw",
                new WithdrawalRequest { Amount = decimal.MaxValue });

            Assert.False(response.IsSuccessStatusCode);
        }
    }
}
