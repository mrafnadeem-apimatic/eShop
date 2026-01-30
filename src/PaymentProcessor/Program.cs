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

// Register a singleton PayPalServerSDK client that will be used by the payment
// service to capture approved PayPal orders. When PayPal is disabled or not
// configured, the payment service will continue to fall back to the simulated
// behavior and will not invoke this client.
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentOptions>>().Value;

    var environment = options.PayPalEnvironment?.Equals("Live", StringComparison.OrdinalIgnoreCase) == true
        ? PaypalServerSdk.Standard.Environment.Production
        : PaypalServerSdk.Standard.Environment.Sandbox;

    var authModel = new PaypalServerSdk.Standard.Authentication.ClientCredentialsAuthModel.Builder(
            options.PayPalClientId ?? throw new InvalidOperationException("PayPal ClientId is not configured"),
            options.PayPalClientSecret ?? throw new InvalidOperationException("PayPal ClientSecret is not configured"))
        .Build();

    return new PaypalServerSdk.Standard.PaypalServerSdkClient.Builder()
        .ClientCredentialsAuth(authModel)
        .Environment(environment)
        .Build();
});

// Register a wrapper over the PayPal Orders SDK controller so that the domain
// service can be unit tested without depending directly on SDK types.
builder.Services.AddSingleton<IPayPalOrdersClient, PayPalOrdersClient>();

builder.Services.AddScoped<IPaymentService, PayPalPaymentService>();

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
