var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMqEventBus("EventBus")
    .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(nameof(PaymentOptions));

builder.Services.AddOptions<PayPalOptions>()
    .BindConfiguration(nameof(PayPalOptions))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId), "PayPal client id is not configured.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientSecret), "PayPal client secret is not configured.")
    .ValidateOnStart();

builder.Services.AddOptions<OrderingApiOptions>()
    .BindConfiguration(nameof(OrderingApiOptions))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.BaseUrl),
        "Ordering API base URL is not configured.")
    .ValidateOnStart();

builder.Services.AddOptions<IdentityServiceOptions>()
    .BindConfiguration("Identity")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Url),
        "Identity service URL is not configured.")
    .ValidateOnStart();

builder.Services.AddHttpClient("Identity", (sp, client) =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("eShop.PaymentProcessor.HttpClient.Identity");

    var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<IdentityServiceOptions>>();
    var options = optionsMonitor.CurrentValue;

    if (string.IsNullOrWhiteSpace(options.Url))
    {
        const string message =
            "Identity service base URL is not configured. Set the 'Identity:Url' configuration value before starting the PaymentProcessor service.";
        logger.LogError(message);
        throw new InvalidOperationException(message);
    }

    if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var baseAddress))
    {
        var message =
            $"Identity service base URL '{options.Url}' is not a valid absolute URI. Fix the 'Identity:Url' configuration value.";
        logger.LogError(message);
        throw new InvalidOperationException(message);
    }

    client.BaseAddress = baseAddress;
});

builder.Services.AddHttpClient<IOrderingApiClient, OrderingApiClient>((sp, client) =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("eShop.PaymentProcessor.HttpClient.Ordering");

    var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OrderingApiOptions>>();
    var options = optionsMonitor.CurrentValue;

    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        const string message =
            "Ordering API base URL is not configured. Set the 'OrderingApiOptions:BaseUrl' configuration value before starting the PaymentProcessor service.";
        logger.LogError(message);
        throw new InvalidOperationException(message);
    }

    if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress))
    {
        var message =
            $"Ordering API base URL '{options.BaseUrl}' is not a valid absolute URI. Fix the 'OrderingApiOptions:BaseUrl' configuration value.";
        logger.LogError(message);
        throw new InvalidOperationException(message);
    }

    client.BaseAddress = baseAddress;
});

builder.Services.AddSingleton<ServiceToServiceTokenProvider>();

builder.Services.AddSingleton<IPayPalClient, PayPalClient>();

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
