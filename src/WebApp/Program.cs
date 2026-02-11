using eShop.WebApp.Components;
using eShop.ServiceDefaults;
using eShop.WebApp.PayPal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaypalServerSdk.Standard.Authentication;
using PaypalEnvironment = PaypalServerSdk.Standard.Environment;
using PaypalServerSdkClient = PaypalServerSdk.Standard.PaypalServerSdkClient;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Bind PayPal options and register them for DI.
builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection("PayPal"));

// Validate critical PayPal configuration early so we fail fast on
// misconfiguration instead of falling back at request-processing time.
// In E2E test mode we deliberately skip real PayPal calls, so credentials
// and redirect URLs are not required there.
var payPalOptions = new PayPalOptions();
builder.Configuration.GetSection("PayPal").Bind(payPalOptions);

if (!payPalOptions.E2ETestMode)
{
    if (string.IsNullOrWhiteSpace(payPalOptions.ClientId) || string.IsNullOrWhiteSpace(payPalOptions.ClientSecret))
    {
        throw new InvalidOperationException(
            "PayPal ClientId or ClientSecret is missing. " +
            "Configure PayPal:ClientId and PayPal:ClientSecret or enable PayPal:E2ETestMode for test-only flows.");
    }

    if (string.IsNullOrWhiteSpace(payPalOptions.RedirectUri) || string.IsNullOrWhiteSpace(payPalOptions.CancelUrl))
    {
        throw new InvalidOperationException(
            "PayPal RedirectUri or CancelUrl is missing. " +
            "Configure PayPal:RedirectUri and PayPal:CancelUrl with absolute URLs.");
    }

    if (!Uri.TryCreate(payPalOptions.RedirectUri, UriKind.Absolute, out _) ||
        !Uri.TryCreate(payPalOptions.CancelUrl, UriKind.Absolute, out _))
    {
        throw new InvalidOperationException(
            "PayPal RedirectUri or CancelUrl is invalid. " +
            "Configure PayPal:RedirectUri and PayPal:CancelUrl with valid absolute URLs.");
    }
}

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
    var options = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<PaypalServerSdkClient>>();

    var clientId = options.ClientId;
    var clientSecret = options.ClientSecret;
    var environment = options.Environment;

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
