var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMqEventBus("EventBus")
    .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(nameof(PaymentOptions));

builder.Services.AddOptions<PayPalOptions>()
    .BindConfiguration(nameof(PayPalOptions));

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
