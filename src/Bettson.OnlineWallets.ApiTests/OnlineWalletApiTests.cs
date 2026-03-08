using System.Net.Http.Json;
using Betsson.OnlineWallets.Web.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Bettson.OnlineWallets.ApiTests
{
    public class OnlineWalletBalanceTests : IClassFixture<WebApplicationFactory<Betsson.OnlineWallets.Web.Startup>>
    {
        private readonly HttpClient _client;

        public OnlineWalletBalanceTests(WebApplicationFactory<Betsson.OnlineWallets.Web.Startup> factory)
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
            Assert.Equal(0m, body!.Amount);
        }
    }
}
