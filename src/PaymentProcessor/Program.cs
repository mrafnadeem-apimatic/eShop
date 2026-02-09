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
    .BindConfiguration(nameof(OrderingApiOptions));

builder.Services.AddOptions<IdentityServiceOptions>()
    .BindConfiguration("Identity");

builder.Services.AddHttpClient("Identity", (sp, client) =>
{
    var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<IdentityServiceOptions>>();
    var options = optionsMonitor.CurrentValue;

    if (!string.IsNullOrWhiteSpace(options.Url))
    {
        client.BaseAddress = new Uri(options.Url);
    }
});

builder.Services.AddHttpClient<IOrderingApiClient, OrderingApiClient>((sp, client) =>
{
    var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OrderingApiOptions>>();
    var options = optionsMonitor.CurrentValue;

    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl);
    }
});

builder.Services.AddSingleton<ServiceToServiceTokenProvider>();

builder.Services.AddSingleton<IPayPalClient, PayPalClient>();

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
