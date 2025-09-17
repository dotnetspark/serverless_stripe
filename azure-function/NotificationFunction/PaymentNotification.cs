using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ServerlessStripe.Shared;

namespace NotificationFunction
{
    public class PaymentNotification
    {
        private readonly ILogger _logger;
        private readonly PaymentNotificationService _service;

        public PaymentNotification(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PaymentNotification>();
            _service = new PaymentNotificationService();
        }

        [Function("PaymentNotification")]
        public async Task Run([QueueTrigger("stripe-events", Connection = "AZURE_STORAGE_QUEUE_CONNECTION_STRING")] string queueMessage)
        {
            var result = await _service.ProcessAsync(queueMessage);
            if (!result.Success)
            {
                _logger.LogError($"Payment notification failed: {result.ErrorMessage}");
            }
            else
            {
                if (!string.IsNullOrEmpty(result.EmailStatus))
                    _logger.LogInformation($"Email sent, status: {result.EmailStatus}");
                if (!string.IsNullOrEmpty(result.SmsStatus))
                    _logger.LogInformation($"SMS sent, sid: {result.SmsStatus}");
            }
        }
    }
}
