using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// OpenTelemetry Tracing
builder.Services.AddOpenTelemetryTracing(tracing => tracing
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("StripeCheckoutFunction"))
    .AddSource("StripeCheckoutFunction")
    .AddOtlpExporter(options =>
    {
        // Configure OTLP endpoint for OpenTelemetry Collector
        options.Endpoint = new Uri("http://localhost:4317"); // Change to your collector endpoint
    })
);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
