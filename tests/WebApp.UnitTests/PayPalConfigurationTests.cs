namespace eShop.WebApp.UnitTests;

[TestClass]
public class PayPalConfigurationTests
{
    private static IHostApplicationBuilder CreateBuilderWithBaseConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();

        // Required configuration for authentication services used by AddApplicationServices.
        builder.Configuration["IdentityUrl"] = "https://identity.example.com";
        builder.Configuration["CallBackUrl"] = "https://callback.example.com";
        builder.Configuration["SessionCookieLifetimeMinutes"] = "60";

        return builder;
    }

    [TestMethod]
    public void AddApplicationServices_RegistersPayPalOptionsAndClient()
    {
        var builder = CreateBuilderWithBaseConfiguration();

        builder.Configuration["PayPalOptions:ClientId"] = "test-client-id";
        builder.Configuration["PayPalOptions:ClientSecret"] = "test-client-secret";
        builder.Configuration["PayPalOptions:Environment"] = "Sandbox";

        builder.AddApplicationServices();

        using var provider = builder.Services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<PayPalOptions>>().Value;

        Assert.AreEqual("test-client-id", options.ClientId);
        Assert.AreEqual("test-client-secret", options.ClientSecret);
        Assert.AreEqual("Sandbox", options.Environment);

        // IPayPalOrdersClient is registered as a singleton.
        var ordersClient1 = provider.GetRequiredService<IPayPalOrdersClient>();
        var ordersClient2 = provider.GetRequiredService<IPayPalOrdersClient>();

        Assert.IsNotNull(ordersClient1);
        Assert.AreSame(ordersClient1, ordersClient2);

        // IPayPalCheckoutService is registered as a scoped service.
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var serviceScope1First = scope1.ServiceProvider.GetRequiredService<IPayPalCheckoutService>();
        var serviceScope1Second = scope1.ServiceProvider.GetRequiredService<IPayPalCheckoutService>();
        var serviceScope2 = scope2.ServiceProvider.GetRequiredService<IPayPalCheckoutService>();

        Assert.IsNotNull(serviceScope1First);
        Assert.AreSame(serviceScope1First, serviceScope1Second);
        Assert.AreNotSame(serviceScope1First, serviceScope2);
    }

    [TestMethod]
    public void PayPalOptions_ValidationFails_WhenClientIdMissing()
    {
        var builder = CreateBuilderWithBaseConfiguration();

        // Only configure ClientSecret; ClientId is left empty, which should fail validation.
        builder.Configuration["PayPalOptions:ClientSecret"] = "test-client-secret";

        builder.AddApplicationServices();

        using var provider = builder.Services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<PayPalOptions>>();

        try
        {
            var _ = options.Value;
            Assert.Fail("Expected OptionsValidationException when ClientId is missing.");
        }
        catch (OptionsValidationException)
        {
            // Expected exception.
        }
    }

    [TestMethod]
    public void PayPalOptions_ValidationFails_WhenClientSecretMissing()
    {
        var builder = CreateBuilderWithBaseConfiguration();

        // Only configure ClientId; ClientSecret is left empty, which should fail validation.
        builder.Configuration["PayPalOptions:ClientId"] = "test-client-id";

        builder.AddApplicationServices();

        using var provider = builder.Services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<PayPalOptions>>();

        try
        {
            var _ = options.Value;
            Assert.Fail("Expected OptionsValidationException when ClientSecret is missing.");
        }
        catch (OptionsValidationException)
        {
            // Expected exception.
        }
    }
}

