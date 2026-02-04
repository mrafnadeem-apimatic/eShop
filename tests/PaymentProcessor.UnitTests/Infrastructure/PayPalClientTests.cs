namespace eShop.PaymentProcessor.UnitTests.Infrastructure;

[TestClass]
public sealed class PayPalClientTests
{
    [TestMethod]
    public void Constructor_throws_when_options_is_null()
    {
        // Arrange
        IOptions<PayPalOptions> options = null!;
        var logger = Substitute.For<ILogger<PayPalClient>>();

        // Act & Assert
        try
        {
            _ = new PayPalClient(options, logger);
            Assert.Fail("Expected ArgumentNullException for null options.");
        }
        catch (ArgumentNullException)
        {
        }
    }

    [TestMethod]
    public void Constructor_throws_when_logger_is_null()
    {
        // Arrange
        var options = Options.Create(new PayPalOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Environment = "Sandbox",
            ApiBaseUrl = "https://api-m.sandbox.paypal.com",
        });

        ILogger<PayPalClient> logger = null!;

        // Act & Assert
        try
        {
            _ = new PayPalClient(options, logger);
            Assert.Fail("Expected ArgumentNullException for null logger.");
        }
        catch (ArgumentNullException)
        {
        }
    }

    [TestMethod]
    public void Constructor_throws_when_options_value_is_null()
    {
        // Arrange
        var options = Substitute.For<IOptions<PayPalOptions>>();
        options.Value.Returns((PayPalOptions)null!);
        var logger = Substitute.For<ILogger<PayPalClient>>();

        // Act & Assert
        try
        {
            _ = new PayPalClient(options, logger);
            Assert.Fail("Expected InvalidOperationException when PayPalOptions.Value is null.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [TestMethod]
    public void Constructor_throws_when_client_id_is_missing()
    {
        // Arrange
        var options = Options.Create(new PayPalOptions
        {
            ClientId = "",
            ClientSecret = "client-secret",
            Environment = "Sandbox",
            ApiBaseUrl = "https://api-m.sandbox.paypal.com",
        });

        var logger = Substitute.For<ILogger<PayPalClient>>();

        // Act & Assert
        try
        {
            _ = new PayPalClient(options, logger);
            Assert.Fail("Expected InvalidOperationException when client id is missing.");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(
                ex.Message.Contains("client id", StringComparison.OrdinalIgnoreCase),
                "Expected exception message to mention client id.");
        }
    }

    [TestMethod]
    public void Constructor_throws_when_client_secret_is_missing()
    {
        // Arrange
        var options = Options.Create(new PayPalOptions
        {
            ClientId = "client-id",
            ClientSecret = "   ",
            Environment = "Sandbox",
            ApiBaseUrl = "https://api-m.sandbox.paypal.com",
        });

        var logger = Substitute.For<ILogger<PayPalClient>>();

        // Act & Assert
        try
        {
            _ = new PayPalClient(options, logger);
            Assert.Fail("Expected InvalidOperationException when client secret is missing.");
        }
        catch (InvalidOperationException ex)
        {
            Assert.IsTrue(
                ex.Message.Contains("client secret", StringComparison.OrdinalIgnoreCase),
                "Expected exception message to mention client secret.");
        }
    }

    [TestMethod]
    public async Task CaptureOrderAsync_throws_when_paypal_order_id_is_missing()
    {
        // Arrange
        var options = Options.Create(new PayPalOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Environment = "Sandbox",
            ApiBaseUrl = "https://api-m.sandbox.paypal.com",
        });

        var logger = Substitute.For<ILogger<PayPalClient>>();

        var client = new PayPalClient(options, logger);

        // Act & Assert
        try
        {
            await client.CaptureOrderAsync(" ", CancellationToken.None);
            Assert.Fail("Expected ArgumentException for missing PayPal order id.");
        }
        catch (ArgumentException)
        {
        }
    }
}

