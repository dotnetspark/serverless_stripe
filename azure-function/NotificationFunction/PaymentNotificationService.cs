using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace NotificationFunction
{
    public class PaymentNotificationService
    {
        public class NotificationResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? EmailStatus { get; set; }
            public string? SmsStatus { get; set; }
        }

        public async Task<NotificationResult> ProcessAsync(string queueMessage)
        {
            try
            {
                var eventJson = Encoding.UTF8.GetString(Convert.FromBase64String(queueMessage));
                var stripeEvent = JsonSerializer.Deserialize<Stripe.Event>(eventJson);
                if (stripeEvent == null)
                {
                    return new NotificationResult { Success = false, ErrorMessage = "Failed to deserialize Stripe event from queue message." };
                }

                string? customerEmail = null;
                string? phoneNumber = null;
                decimal amount = 0;
                if (stripeEvent.Type == "checkout.session.completed")
                {
                    var sessionJson = JsonSerializer.Serialize(stripeEvent.Data.Object);
                    var session = JsonSerializer.Deserialize<JsonElement>(sessionJson);
                    if (session.ValueKind == JsonValueKind.Object)
                    {
                        if (session.TryGetProperty("customer_details", out var customerDetails))
                        {
                            customerEmail = customerDetails.GetProperty("email").GetString();
                        }
                        if (session.TryGetProperty("amount_total", out var amountTotal))
                        {
                            amount = amountTotal.GetDecimal() / 100m;
                        }
                        // Add phone extraction if available
                    }
                }
                // Add logic for payment_intent.succeeded if needed

                string? sendGridApiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
                string? emailStatus = null;
                if (!string.IsNullOrEmpty(sendGridApiKey) && !string.IsNullOrEmpty(customerEmail))
                {
                    var client = new SendGridClient(sendGridApiKey);
                    var from = new EmailAddress("no-reply@example.com", "Stripe Demo");
                    var to = new EmailAddress(customerEmail);
                    var subject = "Payment Received";
                    var plainTextContent = $"Thank you for your payment of {amount:C}.";
                    var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, null);
                    var response = await client.SendEmailAsync(msg);
                    emailStatus = response.StatusCode.ToString();
                }

                string? twilioSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID");
                string? twilioToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");
                string? twilioFrom = Environment.GetEnvironmentVariable("TWILIO_FROM_NUMBER");
                string? smsStatus = null;
                if (!string.IsNullOrEmpty(twilioSid) && !string.IsNullOrEmpty(twilioToken) && !string.IsNullOrEmpty(phoneNumber))
                {
                    TwilioClient.Init(twilioSid, twilioToken);
                    var message = await MessageResource.CreateAsync(
                        to: new PhoneNumber(phoneNumber),
                        from: new PhoneNumber(twilioFrom),
                        body: $"Thank you for your payment of {amount:C}."
                    );
                    smsStatus = message.Sid;
                }

                return new NotificationResult { Success = true, EmailStatus = emailStatus, SmsStatus = smsStatus };
            }
            catch (Exception ex)
            {
                return new NotificationResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
