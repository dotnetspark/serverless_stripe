using Xunit;
using FluentAssertions;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ServerlessStripe.Shared;
using Moq;

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

        [Fact]
        public async Task Uses_Injected_Email_And_Sms_Senders()
        {
            var mockEmail = new Moq.Mock<IEmailSender>();
            var mockSms = new Moq.Mock<ISmsSender>();

            mockEmail.Setup(m => m.SendEmailAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<string>()))
                .ReturnsAsync("OK");
            mockSms.Setup(m => m.SendSmsAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<string>()))
                .ReturnsAsync("SID123");

            var service = new PaymentNotificationService(mockEmail.Object, mockSms.Object);

            var evtJson = "{\"id\":\"evt_4\",\"object\":\"event\",\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"customer_details\":{\"email\":\"cust@example.com\",\"phone\":\"+15551234567\"},\"amount_total\":1500}}}";
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(evtJson));

            var result = await service.ProcessAsync(payload);

            result.Success.Should().BeTrue();
            result.EmailStatus.Should().Be("OK");
            result.SmsStatus.Should().Be("SID123");

            mockEmail.Verify(m => m.SendEmailAsync("cust@example.com", Moq.It.IsAny<string>(), Moq.It.IsAny<string>()), Moq.Times.Once);
            mockSms.Verify(m => m.SendSmsAsync(
                Moq.It.Is<string>(s => s == "+15551234567"), Moq.It.IsAny<string>()), Moq.Times.Once);
        }
    }
}
