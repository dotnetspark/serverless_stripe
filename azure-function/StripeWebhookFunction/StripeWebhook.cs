using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ServerlessStripe.Shared;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace StripeWebhookFunction
{
    public class StripeWebhook
    {
        public class StripeWebhookResponse
        {
            public string? stripeevents { get; set; } // queue output binding
            public HttpResponseData? HttpResponse { get; set; }
        }

        private readonly ILogger _logger;
        private readonly StripeWebhookService _service;
        public StripeWebhook(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<StripeWebhook>();
            _service = new StripeWebhookService();
        }

        [Function("StripeWebhook")]
        public async Task<StripeWebhookResponse> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            var result = new StripeWebhookResponse();
            string? webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");
            string json = await new StreamReader(req.Body).ReadToEndAsync();
            string? stripeSignature = req.Headers.GetValues("Stripe-Signature").FirstOrDefault();

            var serviceResult = _service.ProcessEvent(json, stripeSignature, webhookSecret);
            if (!serviceResult.IsValid)
            {
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync(serviceResult.ErrorMessage ?? "Invalid webhook event.");
                result.HttpResponse = badResponse;
                return result;
            }
            if (!string.IsNullOrEmpty(serviceResult.QueueMessage))
            {
                result.stripeevents = serviceResult.QueueMessage;
            }
            if (!string.IsNullOrEmpty(serviceResult.LogMessage))
            {
                _logger.LogInformation(serviceResult.LogMessage);
            }
            var okResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await okResponse.WriteStringAsync("Webhook received.");
            result.HttpResponse = okResponse;
            return result;
        }
    }
}
