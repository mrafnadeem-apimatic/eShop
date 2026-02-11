using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaypalServerSdk.Standard.Authentication;
using PaypalEnvironment = PaypalServerSdk.Standard.Environment;
using PaypalServerSdkClient = PaypalServerSdk.Standard.PaypalServerSdkClient;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMqEventBus("EventBus")
    .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(nameof(PaymentOptions));

// HTTP client used to query Ordering.API for order totals before invoking PayPal.
// Use service discovery so this works in containerized and cloud environments.
builder.Services.AddHttpClient<IOrderingApiClient, OrderingApiClient>(client =>
    {
        client.BaseAddress = new Uri("https+http://ordering-api");
    })
    .AddClientCredentialsToken("ServiceAuth");

// PayPal SDK client configuration
builder.Services.AddSingleton<PaypalServerSdkClient>(sp =>
{
    var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<PaymentOptions>>();
    var settings = optionsMonitor.CurrentValue;
    var logger = sp.GetRequiredService<ILogger<PaypalServerSdkClient>>();

    var clientId = settings.PayPalClientId ?? string.Empty;
    var clientSecret = settings.PayPalClientSecret ?? string.Empty;
    var environment = settings.PayPalEnvironment;

    var paypalEnvironment = string.Equals(environment, "Live", StringComparison.OrdinalIgnoreCase)
        ? PaypalEnvironment.Production
        : PaypalEnvironment.Sandbox;

    return new PaypalServerSdkClient.Builder()
        .ClientCredentialsAuth(new ClientCredentialsAuthModel.Builder(clientId, clientSecret).Build())
        .Environment(paypalEnvironment)
        .LoggingConfig(config => config
            .Logger(logger)
            .LogLevel(LogLevel.Information))
        .HttpClientConfig(config => config
            .Timeout(TimeSpan.FromSeconds(60)))
        .Build();
});

builder.Services.AddScoped<IPaymentService, PayPalPaymentService>();

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
