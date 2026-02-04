var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMqEventBus("EventBus")
    .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(nameof(PaymentOptions));

builder.Services.AddOptions<PayPalOptions>()
    .BindConfiguration(nameof(PayPalOptions));

builder.Services.AddOptions<OrderingApiOptions>()
    .BindConfiguration(nameof(OrderingApiOptions));

builder.Services.AddHttpClient<IOrderingApiClient, OrderingApiClient>((sp, client) =>
{
    var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OrderingApiOptions>>();
    var options = optionsMonitor.CurrentValue;

    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl);
    }
});

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
