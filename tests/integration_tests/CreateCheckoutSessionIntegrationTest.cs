using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace ServerlessStripe.IntegrationTests
{
    public class CreateCheckoutSessionIntegrationTest
    {
        private readonly string _functionUrl = Environment.GetEnvironmentVariable("FUNCTION_URL") ?? "http://localhost:7071/api/CreateCheckoutSession";

        [Fact]
        public async Task CanCreateStripeCheckoutSession()
        {
            var payload = new
            {
                Items = new[]
                {
                    new { ProductName = "Test Product", UnitAmount = 1000, Quantity = 1 }
                },
                Currency = "usd",
                SuccessUrl = "https://example.com/success",
                CancelUrl = "https://example.com/cancel"
            };

            var json = JsonSerializer.Serialize(payload);
            using var client = new HttpClient();
            var response = await client.PostAsync(_functionUrl, new StringContent(json, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<CheckoutSessionResponse>(responseBody);

            result.Should().NotBeNull();
            result!.sessionId.Should().NotBeNullOrEmpty();
            result.url.Should().StartWith("https://checkout.stripe.com/");
        }

        private class CheckoutSessionResponse
        {
            public string? sessionId { get; set; }
            public string? url { get; set; }
        }
    }
}
