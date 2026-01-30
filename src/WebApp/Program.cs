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

// Bind the existing "PayPal" configuration section to strongly-typed options
// so that the PayPal .NET Server SDK can be configured without changing the
// public configuration surface (FR-6, FR-7, FR-8).
builder.Services.AddOptions<PayPalSdkOptions>()
    .BindConfiguration("PayPal");

// Register a singleton PayPalServerSDK client that will be used by the WebApp
// to create PayPal orders. Credentials and environment are taken from
// configuration; when credentials are missing the client will not be used
// because the WebApp continues to short-circuit in that case.
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PayPalSdkOptions>>().Value;

    // Default to Sandbox when the environment is not explicitly configured.
    var environment = options.Environment?.Equals("Live", StringComparison.OrdinalIgnoreCase) == true
        ? PaypalServerSdk.Standard.Environment.Production
        : PaypalServerSdk.Standard.Environment.Sandbox;

    // Note: when ClientId/ClientSecret are not configured, these will be empty.
    // The WebApp will continue to treat PayPal as disabled in that case and
    // will not invoke the SDK (see FR-5, FR-6, FR-8).
    var authModel = new PaypalServerSdk.Standard.Authentication.ClientCredentialsAuthModel.Builder(
            options.ClientId ?? throw new InvalidOperationException("PayPal ClientId is not configured"),
            options.ClientSecret ?? throw new InvalidOperationException("PayPal ClientSecret is not configured"))
        .Build();

    return new PaypalServerSdk.Standard.PaypalServerSdkClient.Builder()
        .ClientCredentialsAuth(authModel)
        .Environment(environment)
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
