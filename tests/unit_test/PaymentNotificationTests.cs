using Xunit;
using FluentAssertions;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ServerlessStripe.Shared;

namespace ServerlessStripe.UnitTests
{
    public class PaymentNotificationTests
    {
        [Fact]
        public async Task Handles_Invalid_Queue_Message_Gracefully()
        {
            var service = new PaymentNotificationService();
            var result = await service.ProcessAsync("not-a-valid-base64");
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        // Add more tests for valid event, email, and SMS logic as needed
    }
}
