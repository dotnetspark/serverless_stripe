using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Stripe;
using SendGrid;
using SendGrid.Helpers.Mail;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace ServerlessStripe.Shared
{
    public interface IEmailSender
    {
        Task<string?> SendEmailAsync(string to, string subject, string body);
    }

    public interface ISmsSender
    {
        Task<string?> SendSmsAsync(string to, string body);
    }

    public class SendGridEmailSender : IEmailSender
    {
        private readonly string? _apiKey;
        public SendGridEmailSender(string? apiKey) => _apiKey = apiKey;
        public async Task<string?> SendEmailAsync(string to, string subject, string body)
        {
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(to)) return null;
            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress("no-reply@example.com", "Stripe Demo");
            var toAddr = new EmailAddress(to);
            var msg = MailHelper.CreateSingleEmail(from, toAddr, subject, body, null);
            var response = await client.SendEmailAsync(msg);
            return response.StatusCode.ToString();
        }
    }

    public class TwilioSmsSender : ISmsSender
    {
        private readonly string? _sid;
        private readonly string? _token;
        private readonly string? _from;
        public TwilioSmsSender(string? sid, string? token, string? from)
        {
            _sid = sid; _token = token; _from = from;
        }

        public async Task<string?> SendSmsAsync(string to, string body)
        {
            if (string.IsNullOrEmpty(_sid) || string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_from) || string.IsNullOrEmpty(to)) return null;
            TwilioClient.Init(_sid, _token);
            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(to),
                from: new PhoneNumber(_from),
                body: body
            );
            return message.Sid;
        }
    }

    public class PaymentNotificationService
    {
        public class NotificationResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public string? EmailStatus { get; set; }
            public string? SmsStatus { get; set; }
        }

        private readonly IEmailSender? _emailSender;
        private readonly ISmsSender? _smsSender;

        public PaymentNotificationService(IEmailSender? emailSender = null, ISmsSender? smsSender = null)
        {
            _emailSender = emailSender ?? new SendGridEmailSender(Environment.GetEnvironmentVariable("SENDGRID_API_KEY"));
            _smsSender = smsSender ?? new TwilioSmsSender(Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID"), Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN"), Environment.GetEnvironmentVariable("TWILIO_FROM_NUMBER"));
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
                            if (customerDetails.TryGetProperty("email", out var emailProp))
                            {
                                customerEmail = emailProp.GetString();
                            }
                            if (customerDetails.TryGetProperty("phone", out var phoneProp))
                            {
                                phoneNumber = phoneProp.GetString();
                            }
                        }
                        if (session.TryGetProperty("amount_total", out var amountTotal))
                        {
                            amount = amountTotal.GetDecimal() / 100m;
                        }
                        // Add phone extraction if available
                    }
                }

                // Fallback: if customerEmail or phoneNumber or amount are still missing, try parsing raw JSON
                if (string.IsNullOrEmpty(customerEmail) || string.IsNullOrEmpty(phoneNumber) || amount == 0)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(eventJson);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("data", out var data) && data.TryGetProperty("object", out var obj))
                        {
                            if ((string.IsNullOrEmpty(customerEmail) || string.IsNullOrEmpty(phoneNumber)) && obj.TryGetProperty("customer_details", out var cust))
                            {
                                if (string.IsNullOrEmpty(customerEmail) && cust.TryGetProperty("email", out var e)) customerEmail = e.GetString();
                                if (string.IsNullOrEmpty(phoneNumber) && cust.TryGetProperty("phone", out var p)) phoneNumber = p.GetString();
                            }
                            if (amount == 0 && obj.TryGetProperty("amount_total", out var amt))
                            {
                                amount = amt.GetDecimal() / 100m;
                            }
                        }
                    }
                    catch
                    {
                        // ignore fallback parse errors
                    }
                }

                string? emailStatus = null;
                if (_emailSender != null && !string.IsNullOrEmpty(customerEmail))
                {
                    emailStatus = await _emailSender.SendEmailAsync(customerEmail, "Payment Received", $"Thank you for your payment of {amount:C}.");
                }

                string? smsStatus = null;
                if (_smsSender != null && !string.IsNullOrEmpty(phoneNumber))
                {
                    smsStatus = await _smsSender.SendSmsAsync(phoneNumber, $"Thank you for your payment of {amount:C}.");
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
