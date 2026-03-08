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
    }
}
