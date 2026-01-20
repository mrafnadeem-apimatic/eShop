#nullable enable
using System.Net.Http.Json;

namespace eShop.PaymentProcessor;

public class OrderingApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderingApiClient> _logger;

    public OrderingApiClient(HttpClient httpClient, ILogger<OrderingApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OrderDto?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ordering.API is versioned; specify api-version=1.0 explicitly.
            var requestUri = $"api/orders/{orderId}?api-version=1.0";
            return await _httpClient.GetFromJsonAsync<OrderDto>(requestUri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order {OrderId} from Ordering.API", orderId);
            return null;
        }
    }
}

public sealed class OrderDto
{
    public int OrderNumber { get; set; }
    public decimal Total { get; set; }
    public string? PayPalOrderId { get; set; }
}


