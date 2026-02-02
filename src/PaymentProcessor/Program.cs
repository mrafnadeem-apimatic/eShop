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

// Only register the PayPal SDK client and its wrapper when PayPal is enabled
// and credentials are configured. Otherwise, the payment service will fall
// back to the simulated behavior without invoking the SDK.
var paymentOptions = builder.Configuration
    .GetSection(nameof(PaymentOptions))
    .Get<PaymentOptions>() ?? new PaymentOptions();

if (paymentOptions.UsePayPal)
{
    if (string.IsNullOrWhiteSpace(paymentOptions.PayPalClientId) ||
        string.IsNullOrWhiteSpace(paymentOptions.PayPalClientSecret))
    {
        throw new InvalidOperationException(
            "PaymentOptions misconfiguration: PayPal is enabled (UsePayPal=true) " +
            "but PayPalClientId or PayPalClientSecret is not configured.");
    }

    builder.Services.AddSingleton(sp =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentOptions>>().Value;

        var environment = options.PayPalEnvironment?.Equals("Live", StringComparison.OrdinalIgnoreCase) == true
            ? PaypalServerSdk.Standard.Environment.Production
            : PaypalServerSdk.Standard.Environment.Sandbox;

        var authModel = new PaypalServerSdk.Standard.Authentication.ClientCredentialsAuthModel.Builder(
                options.PayPalClientId!,
                options.PayPalClientSecret!)
            .Build();

        return new PaypalServerSdk.Standard.PaypalServerSdkClient.Builder()
            .ClientCredentialsAuth(authModel)
            .Environment(environment)
            .LoggingConfig(config => config
                .LogLevel(Microsoft.Extensions.Logging.LogLevel.Information)
                .RequestConfig(reqConfig => reqConfig.Body(false))
                .ResponseConfig(respConfig => respConfig.Headers(false)))
            .Build();
    });

    // Register a wrapper over the PayPal Orders SDK controller so that the domain
    // service can be unit tested without depending directly on SDK types.
    builder.Services.AddSingleton<IPayPalOrdersClient, PayPalOrdersClient>();
}
else
{
    // PayPal is disabled or not configured; still provide an IPayPalOrdersClient so
    // that the payment service can be resolved, but it should never be invoked
    // because the service falls back to simulated payments in this case.
    builder.Services.AddSingleton<IPayPalOrdersClient, DisabledPayPalOrdersClient>();
}

builder.Services.AddScoped<IPaymentService, PayPalPaymentService>();

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
