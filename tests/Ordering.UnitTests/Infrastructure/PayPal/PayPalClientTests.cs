namespace eShop.Ordering.UnitTests.Infrastructure.PayPal;

using eShop.Ordering.API;
using eShop.Ordering.API.Infrastructure.PayPal;
using Microsoft.Extensions.Options;

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
            StringAssert.Contains(ex.Message.ToLowerInvariant(), "client id");
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
            StringAssert.Contains(ex.Message.ToLowerInvariant(), "client secret");
        }
    }

    [TestMethod]
    public async Task CreateOrderAsync_throws_when_amount_is_not_greater_than_zero()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        try
        {
            await client.CreateOrderAsync(0m, "USD", "basket-1", CancellationToken.None);
            Assert.Fail("Expected ArgumentOutOfRangeException for non-positive amount.");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Assert.AreEqual("amount", ex.ParamName);
        }
    }

    [TestMethod]
    public async Task CreateOrderAsync_throws_when_currency_is_missing()
    {
        // Arrange
        var client = CreateClient();

        // Act & Assert
        try
        {
            await client.CreateOrderAsync(10m, " ", "basket-1", CancellationToken.None);
            Assert.Fail("Expected ArgumentException for missing currency.");
        }
        catch (ArgumentException)
        {
        }
    }

    [TestMethod]
    public async Task CaptureOrderAsync_throws_when_paypal_order_id_is_missing()
    {
        // Arrange
        var client = CreateClient();

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

    private static PayPalClient CreateClient()
    {
        var options = Options.Create(new PayPalOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Environment = "Sandbox",
        });

        var logger = Substitute.For<ILogger<PayPalClient>>();

        return new PayPalClient(options, logger);
    }
}

