var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMqEventBus("EventBus")
    .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(nameof(PaymentOptions));

builder.Services.AddSingleton<eShop.PaymentProcessor.Services.PaypalCheckoutService>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapPaypalApi();

await app.RunAsync();
