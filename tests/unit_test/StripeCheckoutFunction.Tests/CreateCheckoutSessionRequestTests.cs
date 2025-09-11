using Xunit;
using FluentAssertions;
using StripeCheckoutFunction;
using System.Collections.Generic;

namespace StripeCheckoutFunction.Tests
{
    public class CreateCheckoutSessionRequestTests
    {
        [Fact]
        public void Should_Allow_Valid_Cart_Items()
        {
            var request = new CreateCheckoutSessionRequest
            {
                Items = new List<CartItem>
                {
                    new CartItem { ProductName = "Test", UnitAmount = 1000, Quantity = 2 }
                },
                Currency = "usd",
                SuccessUrl = "https://example.com/success",
                CancelUrl = "https://example.com/cancel"
            };

            request.Items.Should().NotBeNullOrEmpty();
            request.Currency.Should().Be("usd");
            request.SuccessUrl.Should().StartWith("https://");
            request.CancelUrl.Should().StartWith("https://");
        }

        [Fact]
        public void CartItem_Should_Have_Valid_Properties()
        {
            var item = new CartItem
            {
                ProductName = "Widget",
                UnitAmount = 500,
                Quantity = 3
            };

            item.ProductName.Should().Be("Widget");
            item.UnitAmount.Should().BeGreaterThan(0);
            item.Quantity.Should().BeGreaterThan(0);
        }
    }
}
