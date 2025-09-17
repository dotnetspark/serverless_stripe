using Xunit;
using FluentAssertions;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ServerlessStripe.Shared;

namespace ServerlessStripe.UnitTests
{
    public class PaymentNotificationServiceTests
    {
        [Fact]
        public async Task Returns_Error_For_Invalid_Base64()
        {
            var service = new PaymentNotificationService();
            var result = await service.ProcessAsync("not-a-valid-base64");
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Processes_Valid_Checkout_Session_Without_External_Keys()
        {
            // ensure external providers are not configured
            Environment.SetEnvironmentVariable("SENDGRID_API_KEY", null);
            Environment.SetEnvironmentVariable("TWILIO_ACCOUNT_SID", null);
            Environment.SetEnvironmentVariable("TWILIO_AUTH_TOKEN", null);
            Environment.SetEnvironmentVariable("TWILIO_FROM_NUMBER", null);

            var service = new PaymentNotificationService();

            var evtJson = "{\"id\":\"evt_1\",\"object\":\"event\",\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"customer_details\":{\"email\":\"cust@example.com\"},\"amount_total\":5000}}}";
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(evtJson));

            var result = await service.ProcessAsync(payload);

            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            result.EmailStatus.Should().BeNull();
            result.SmsStatus.Should().BeNull();
        }

        [Fact]
        public async Task Returns_Success_For_NonCheckout_Event()
        {
            var service = new PaymentNotificationService();

            var evtJson = "{\"id\":\"evt_2\",\"object\":\"event\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{}}}";
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(evtJson));

            var result = await service.ProcessAsync(payload);

            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public async Task Returns_Success_When_CustomerEmail_Missing()
        {
            var service = new PaymentNotificationService();

            var evtJson = "{\"id\":\"evt_3\",\"object\":\"event\",\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"amount_total\":2500}}}";
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(evtJson));

            var result = await service.ProcessAsync(payload);

            result.Success.Should().BeTrue();
            result.EmailStatus.Should().BeNull();
            result.SmsStatus.Should().BeNull();
        }
    }
}
