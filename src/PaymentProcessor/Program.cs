using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;
using PaypalEnvironment = PaypalServerSdk.Standard.Environment;
using eShop.PaymentProcessor.PayPal;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMqEventBus("EventBus")
    .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(nameof(PaymentOptions));

builder.Services.AddOptions<PayPalOptions>()
    .BindConfiguration(nameof(PayPalOptions));

var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddSingleton<PaypalServerSdkClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;

    // Map the configured PayPal environment (e.g. "Sandbox" or "Production") to the SDK enum.
    // Values other than "Production" default to Sandbox.
    var environment = string.Equals(options.Environment, "Production", StringComparison.OrdinalIgnoreCase)
        ? PaypalEnvironment.Production
        : PaypalEnvironment.Sandbox;

    return new PaypalServerSdkClient.Builder()
        .ClientCredentialsAuth(
            new ClientCredentialsAuthModel.Builder(options.ClientId, options.ClientSecret)
                .Build())
        .HttpClientConfig(httpClientConfig =>
            httpClientConfig.Timeout(TimeSpan.FromSeconds(100)))
        .Environment(environment)
        .LoggingConfig(config => config
            .LogLevel(LogLevel.Information)
            .RequestConfig(reqConfig => reqConfig.Body(isDevelopment))
            .ResponseConfig(respConfig => respConfig.Headers(isDevelopment)))
        .Build();
});

builder.Services.AddSingleton<IPayPalOrderCaptureClient, SdkPayPalOrderCaptureClient>();

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
