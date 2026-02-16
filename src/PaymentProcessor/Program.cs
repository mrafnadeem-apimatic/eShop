using eShop.PaymentProcessor.Services;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMqEventBus("EventBus")
    .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(nameof(PaymentOptions));

builder.Services.AddOptions<PayPalOptions>()
    .BindConfiguration(nameof(PayPalOptions))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId), "PayPal ClientId must be configured.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientSecret), "PayPal ClientSecret must be configured.")
    .ValidateOnStart();

builder.Services.AddSingleton<PaypalServerSdkClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;

    var environment = string.Equals(options.Environment, "Live", StringComparison.OrdinalIgnoreCase)
        ? PaypalServerSdk.Standard.Environment.Production
        : PaypalServerSdk.Standard.Environment.Sandbox;

    return new PaypalServerSdkClient.Builder()
        .ClientCredentialsAuth(
            new ClientCredentialsAuthModel.Builder(options.ClientId, options.ClientSecret)
                .Build())
        .HttpClientConfig(c => c.Timeout(TimeSpan.FromSeconds(30)))
        .Environment(environment)
        .Build();
});

builder.Services.AddSingleton<IPayPalCaptureService, PayPalCaptureService>();

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
