using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Stripe;
using Stripe.Checkout;
using System.Text.Json;
using System.Collections.Generic;

namespace StripeCheckoutFunction
{
    public class CreateCheckoutSessionRequest
    {
        public List<CartItem>? Items { get; set; }
        public string? Currency { get; set; }
        public string? SuccessUrl { get; set; }
        public string? CancelUrl { get; set; }
    }

    public class CartItem
    {
        public string? ProductName { get; set; }
        public long UnitAmount { get; set; } // in cents
        public int Quantity { get; set; }
    }

    public class CreateCheckoutSession
    {
        private readonly ILogger _logger;
        public CreateCheckoutSession(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CreateCheckoutSession>();
        }

        [Function("CreateCheckoutSession")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<CreateCheckoutSessionRequest>(requestBody);

            if (data == null || data.Items == null || string.IsNullOrEmpty(data.Currency) || string.IsNullOrEmpty(data.SuccessUrl) || string.IsNullOrEmpty(data.CancelUrl))
            {
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request payload.");
                return badResponse;
            }

            // Use environment variable for local/dev/test, Key Vault for production
            string environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Development";
            string? stripeApiKey = Environment.GetEnvironmentVariable("STRIPE_API_KEY");
            if (string.IsNullOrEmpty(stripeApiKey))
            {
                if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
                {
                    var keyVaultUrl = Environment.GetEnvironmentVariable("KEY_VAULT_URL");
                    var secretName = Environment.GetEnvironmentVariable("STRIPE_SECRET_NAME");
                    if (string.IsNullOrEmpty(keyVaultUrl) || string.IsNullOrEmpty(secretName))
                    {
                        var badResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                        await badResponse.WriteStringAsync("Missing Key Vault configuration and no local Stripe API key.");
                        return badResponse;
                    }
                    var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
                    KeyVaultSecret secret = await client.GetSecretAsync(secretName);
                    stripeApiKey = secret.Value;
                }
                else
                {
                    var badResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                    await badResponse.WriteStringAsync("STRIPE_API_KEY environment variable is not set.");
                    return badResponse;
                }
            }
            StripeConfiguration.ApiKey = stripeApiKey;

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",
                SuccessUrl = data.SuccessUrl,
                CancelUrl = data.CancelUrl
            };

            foreach (var item in data.Items)
            {
                options.LineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = data.Currency,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.ProductName
                        },
                        UnitAmount = item.UnitAmount
                    },
                    Quantity = item.Quantity
                });
            }

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { sessionId = session.Id, url = session.Url });
            return response;
        }
    }
}
