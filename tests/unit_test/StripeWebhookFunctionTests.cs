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
        [Fact]
        public void Returns_Error_If_Missing_Signature()
        {
            var service = new StripeWebhookService();
            var result = service.ProcessEvent("{}", null, "whsec_test");
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        // Add more tests for signature verification, event types, and queue output as needed
    }
}
