using eShop.WebApp.Components;
using eShop.ServiceDefaults;
using eShop.WebApp.PayPal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PaypalServerSdk.Standard.Authentication;
using PaypalEnvironment = PaypalServerSdk.Standard.Environment;
using PaypalServerSdkClient = PaypalServerSdk.Standard.PaypalServerSdkClient;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Session is used to persist the PayPal order id between the time an order is
// created in /paypal/pay and when the shopper is redirected back to checkout.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// PayPal SDK client configuration
builder.Services.AddSingleton<PaypalServerSdkClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<PaypalServerSdkClient>>();

    var clientId = configuration["PayPal:ClientId"] ?? string.Empty;
    var clientSecret = configuration["PayPal:ClientSecret"] ?? string.Empty;
    var environment = configuration["PayPal:Environment"];

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

builder.AddApplicationServices();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseAntiforgery();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseSession();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// PayPal OAuth endpoints (Log in with PayPal)
app.MapPayPalEndpoints();

app.MapForwarder("/product-images/{id}", "https+http://catalog-api", "/api/catalog/items/{id}/pic");

app.Run();
