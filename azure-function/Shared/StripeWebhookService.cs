using Stripe;
using System;
using System.Text;
using System.Text.Json;

namespace ServerlessStripe.Shared
{
    public class StripeWebhookService
    {
        public delegate Stripe.Event ConstructEventDelegate(string json, string stripeSignature, string webhookSecret);

        private readonly ConstructEventDelegate _constructEvent;

        public StripeWebhookService() : this((json, sig, secret) => EventUtility.ConstructEvent(json, sig, secret)) { }

        public StripeWebhookService(ConstructEventDelegate constructEvent)
        {
            _constructEvent = constructEvent ?? throw new ArgumentNullException(nameof(constructEvent));
        }
        public class WebhookResult
        {
            public bool IsValid { get; set; }
            public string? ErrorMessage { get; set; }
            public string? QueueMessage { get; set; }
            public string? LogMessage { get; set; }
        }

        public WebhookResult ProcessEvent(string json, string? stripeSignature, string? webhookSecret)
        {
            if (string.IsNullOrEmpty(webhookSecret) || string.IsNullOrEmpty(stripeSignature))
            {
                return new WebhookResult { IsValid = false, ErrorMessage = "Missing Stripe webhook secret or signature." };
            }

            Event stripeEvent;
            try
            {
                stripeEvent = _constructEvent(json, stripeSignature, webhookSecret);
            }
            catch (Exception ex)
            {
                return new WebhookResult { IsValid = false, ErrorMessage = $"Invalid Stripe signature: {ex}" };
            }

            if (stripeEvent.Type == "checkout.session.completed" || stripeEvent.Type == "payment_intent.succeeded")
            {
                string eventPayload = JsonSerializer.Serialize(stripeEvent);
                string queueMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(eventPayload));
                return new WebhookResult
                {
                    IsValid = true,
                    QueueMessage = queueMessage,
                    LogMessage = $"Published Stripe event {stripeEvent.Id} to queue."
                };
            }
            else
            {
                return new WebhookResult
                {
                    IsValid = true,
                    LogMessage = $"Ignored Stripe event type: {stripeEvent.Type}"
                };
            }
        }
    }
}
