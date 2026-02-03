using System.Net;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.Http;
using eShop.Ordering.API.Application.Models;

namespace eShop.Ordering.FunctionalTests;

public sealed class PayPalApiTests : IClassFixture<OrderingApiFixture>
{
    private readonly HttpClient _httpClient;

    public PayPalApiTests(OrderingApiFixture fixture)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));
        _httpClient = fixture.CreateDefaultClient(handler);
    }

    [Fact]
    public async Task CreatePayPalOrder_endpoint_returns_ok_and_order_id()
    {
        // Arrange
        var payload = new
        {
            amount = 10m,
            currency = "USD",
            basketId = "basket-123"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync("api/payments/paypal/create-order", content);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<CreatePayPalOrderResponse>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.Equal("TEST_PAYPAL_ORDER_ID", result.PayPalOrderId);
    }

    [Fact]
    public async Task CheckoutWithPayPal_endpoint_creates_order_and_returns_ok()
    {
        // Arrange
        var item = new BasketItem
        {
            Id = "1",
            ProductId = 12,
            ProductName = "Test",
            UnitPrice = 10,
            OldUnitPrice = 9,
            Quantity = 1,
            PictureUrl = null
        };

        var payload = new CheckoutWithPayPalRequest(
            UserId: "user-1",
            UserName: "Test User",
            City: "City",
            Street: "Street",
            State: "State",
            Country: "Country",
            ZipCode: "12345",
            PayPalOrderId: "PAYPAL-ORDER-ID",
            Items: new List<BasketItem> { item });

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json")
        {
            Headers = { { "x-requestid", Guid.NewGuid().ToString() } }
        };

        // Act
        var response = await _httpClient.PostAsync("api/orders/checkout-paypal", content);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

