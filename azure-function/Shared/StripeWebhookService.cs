using Stripe;
using System;
using System.Text;
using System.Text.Json;

namespace ServerlessStripe.Shared
{
    public class StripeWebhookService
    {
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
                stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);
            }
            catch (Exception ex)
            {
                return new WebhookResult { IsValid = false, ErrorMessage = $"Invalid Stripe signature: {ex.Message}" };
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
