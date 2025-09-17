using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using Xunit;
using System.Text.Json;

namespace ServerlessStripe.IntegrationTests
{
    public class LocalSettingsFixture
    {
        public LocalSettingsFixture()
        {
            string? settingsPath = Environment.GetEnvironmentVariable("LOCAL_SETTINGS_PATH");

            if (string.IsNullOrEmpty(settingsPath))
            {
                // fallback to default path if environment variable is not set
                settingsPath = "/workspaces/serverless_stripe/azure-function/local.settings.json";
            }

            if (File.Exists(settingsPath))
            {
                Console.WriteLine($"[DEBUG] Found local.settings.json at: {settingsPath}");
                var json = File.ReadAllText(settingsPath);
                using var doc = JsonDocument.Parse(json);
                var values = doc.RootElement.GetProperty("Values");
                foreach (var prop in values.EnumerateObject())
                {
                    Console.WriteLine($"[DEBUG] Exporting {prop.Name}={prop.Value.GetString()}");
                    Environment.SetEnvironmentVariable(prop.Name, prop.Value.GetString());
                }
            }
            else
            {
                var message = $"[DEBUG] local.settings.json not found at {settingsPath}";
                Console.Error.WriteLine(message);
                throw new Exception(message);
            }
        }
    }

    public class KeyVaultIntegrationTest : IClassFixture<LocalSettingsFixture>
    {
        [Fact]
        public async Task CanReadStripeApiKeyFromKeyVault()
        {
            var keyVaultUrl = Environment.GetEnvironmentVariable("KEY_VAULT_URL");
            var secretName = Environment.GetEnvironmentVariable("STRIPE_SECRET_NAME");
            keyVaultUrl.Should().NotBeNullOrEmpty("KEY_VAULT_URL is not set");
            secretName.Should().NotBeNullOrEmpty("STRIPE_SECRET_NAME is not set");

            if (string.IsNullOrEmpty(keyVaultUrl))
            {
                throw new InvalidOperationException("KEY_VAULT_URL is not set or empty");
            }
            if (string.IsNullOrEmpty(secretName))
            {
                throw new InvalidOperationException("STRIPE_SECRET_NAME is not set or empty");
            }

            var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
            KeyVaultSecret secret = await client.GetSecretAsync(secretName);

            secret.Value.Should().NotBeNullOrEmpty("Stripe API key is missing or empty in Key Vault");
        }
    }
}
