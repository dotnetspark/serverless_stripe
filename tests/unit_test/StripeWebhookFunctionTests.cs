using Xunit;
using FluentAssertions;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ServerlessStripe.Shared;

namespace ServerlessStripe.UnitTests
{
    public class StripeWebhookFunctionTests
    {
    // Note: Some unit tests inject a fake `ConstructEventDelegate` instead of
    // calling `Stripe.EventUtility.ConstructEvent` directly. stripe.net's
    // JSON deserializer expects a precise event payload shape which is
    // brittle to reproduce in unit tests. Injecting a minimal delegate lets
    // us assert signature verification and downstream behavior without
    // depending on stripe.net internals. For full integration coverage,
    // add separate integration tests that use real Stripe-style payloads.

        [Fact]
        public void Returns_Error_If_Missing_Signature()
        {
            var service = new StripeWebhookService();
            var result = service.ProcessEvent("{}", null, "whsec_test");
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        private static string ComputeStripeSignature(string payload, string secret)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signedPayload = $"{timestamp}.{payload}";
            using var hasher = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
            var hash = hasher.ComputeHash(System.Text.Encoding.UTF8.GetBytes(signedPayload));
            var sig = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return $"t={timestamp},v1={sig}";
        }

        [Fact]
        public void Returns_Error_If_Invalid_Signature()
        {
            var service = new StripeWebhookService();
            var payload = "{\"id\":\"evt_invalid\",\"type\":\"checkout.session.completed\"}";
            var badHeader = "t=123456,v1=deadbeef";
            var result = service.ProcessEvent(payload, badHeader, "whsec_testsecret");
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Invalid Stripe signature");
        }

        [Fact]
        public void Processes_Checkout_Session_Completed_And_Publishes_QueueMessage()
        {
            // inject a fake constructor that returns a checkout.completed event
            var fakeConstruct = new StripeWebhookService.ConstructEventDelegate((json, sig, secret) =>
            {
                return new Stripe.Event { Id = "evt_checkout_1", Type = "checkout.session.completed" };
            });

            var service = new StripeWebhookService(fakeConstruct);
            var payload = "{}";
            var header = "t=123,v1=abc";
            var secret = "whsec_dummy";

            var result = service.ProcessEvent(payload, header, secret);
            Console.WriteLine($"Diagnostic ErrorMessage: {result.ErrorMessage}");
            result.IsValid.Should().BeTrue();
            result.QueueMessage.Should().NotBeNullOrEmpty();

            // decode base64 and verify contains event id and type
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(result.QueueMessage ?? string.Empty));
            decoded.Should().Contain("evt_checkout_1");
            decoded.Should().Contain("checkout.session.completed");
        }

        [Fact]
        public void Ignores_NonCheckout_Event_Types()
        {
            var fakeConstruct = new StripeWebhookService.ConstructEventDelegate((json, sig, secret) =>
            {
                return new Stripe.Event { Id = "evt_other_1", Type = "charge.refunded" };
            });

            var service = new StripeWebhookService(fakeConstruct);
            var payload = "{}";
            var header = "t=123,v1=abc";
            var secret = "whsec_dummy";

            var result = service.ProcessEvent(payload, header, secret);
            Console.WriteLine($"Diagnostic ErrorMessage: {result.ErrorMessage}");
            result.IsValid.Should().BeTrue();
            result.QueueMessage.Should().BeNull();
            result.LogMessage.Should().Contain("Ignored Stripe event type");
        }

        [Fact]
        public void Returns_Success_For_Valid_Signature_Checkout()
        {
            var payload = "{\"id\":\"evt_valid_checkout\",\"object\":\"event\",\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_123\",\"object\":\"checkout.session\"}}}";
            var secret = "whsec_test";
            var header = ComputeStripeSignature(payload, secret);

            var fakeConstruct = new StripeWebhookService.ConstructEventDelegate((json, sig, sec) =>
            {
                if (sig != header || sec != secret)
                {
                    throw new Exception("Invalid signature in fake construct");
                }

                return new Stripe.Event { Id = "evt_valid_checkout", Type = "checkout.session.completed" };
            });

            var service = new StripeWebhookService(fakeConstruct);

            var result = service.ProcessEvent(payload, header, secret);
            Console.WriteLine($"Diagnostic ErrorMessage: {result.ErrorMessage}");
            result.IsValid.Should().BeTrue();
            result.QueueMessage.Should().NotBeNullOrEmpty();

            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(result.QueueMessage ?? string.Empty));
            decoded.Should().Contain("evt_valid_checkout");
            decoded.Should().Contain("checkout.session.completed");
        }

        [Fact]
        public void Returns_Success_For_Valid_Signature_PaymentIntent()
        {
            var payload = "{\"id\":\"evt_valid_payment\",\"object\":\"event\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"pi_123\",\"object\":\"payment_intent\"}}}";
            var secret = "whsec_test";
            var header = ComputeStripeSignature(payload, secret);

            var fakeConstruct = new StripeWebhookService.ConstructEventDelegate((json, sig, sec) =>
            {
                if (sig != header || sec != secret)
                {
                    throw new Exception("Invalid signature in fake construct");
                }

                return new Stripe.Event { Id = "evt_valid_payment", Type = "payment_intent.succeeded" };
            });

            var service = new StripeWebhookService(fakeConstruct);

            var result = service.ProcessEvent(payload, header, secret);
            Console.WriteLine($"Diagnostic ErrorMessage: {result.ErrorMessage}");
            result.IsValid.Should().BeTrue();
            result.QueueMessage.Should().NotBeNullOrEmpty();

            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(result.QueueMessage ?? string.Empty));
            decoded.Should().Contain("evt_valid_payment");
            decoded.Should().Contain("payment_intent.succeeded");
        }

        [Fact]
        public void Returns_Error_When_Signature_Does_Not_Match_Secret()
        {
            var service = new StripeWebhookService();
            var payload = "{\"id\":\"evt_mismatch\",\"type\":\"checkout.session.completed\"}";
            var signingSecret = "whsec_correct";
            var header = ComputeStripeSignature(payload, signingSecret);
            var verificationSecret = "whsec_incorrect";

            var result = service.ProcessEvent(payload, header, verificationSecret);

            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Invalid Stripe signature");
        }
    }
}
