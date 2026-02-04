namespace eShop.WebApp.Services;

public class OrderingService(HttpClient httpClient)
{
    private readonly string remoteServiceBaseUrl = "/api/Orders/";

    public Task<OrderRecord[]> GetOrders()
    {
        return httpClient.GetFromJsonAsync<OrderRecord[]>(remoteServiceBaseUrl)!;
    }

    public Task CreateOrder(CreateOrderRequest request, Guid requestId)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, remoteServiceBaseUrl);
        requestMessage.Headers.Add("x-requestid", requestId.ToString());
        requestMessage.Content = JsonContent.Create(request);
        return httpClient.SendAsync(requestMessage);
    }

    public async Task<string> CreatePayPalOrderAsync(
        CreatePayPalOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/payments/paypal/create-order",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CreatePayPalOrderResponse>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.PayPalOrderId))
        {
            throw new InvalidOperationException("PayPal did not return an order id.");
        }

        return payload.PayPalOrderId;
    }

    public Task CheckoutWithPayPalAsync(
        CheckoutWithPayPalRequest request,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{remoteServiceBaseUrl}checkout-paypal");
        requestMessage.Headers.Add("x-requestid", requestId.ToString());
        requestMessage.Content = JsonContent.Create(request);

        return httpClient.SendAsync(requestMessage, cancellationToken);
    }
}

public record OrderRecord(
    int OrderNumber,
    DateTime Date,
    string Status,
    decimal Total);

public sealed record CreatePayPalOrderRequest(
    decimal Amount,
    string Currency,
    string? BasketId = null);

public sealed record CreatePayPalOrderResponse(string PayPalOrderId);

public sealed record CheckoutWithPayPalRequest(
    string UserId,
    string UserName,
    string City,
    string Street,
    string State,
    string Country,
    string ZipCode,
    string PayPalOrderId,
    List<BasketItem> Items);
