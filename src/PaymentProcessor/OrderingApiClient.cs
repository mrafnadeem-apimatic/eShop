using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace eShop.PaymentProcessor;

public interface IOrderingApiClient
{
    /// <summary>
    /// Returns true when the target order is already marked as paid in the Ordering service.
    /// </summary>
    Task<bool> IsOrderAlreadyPaidAsync(int orderId, CancellationToken cancellationToken = default);
}

public sealed class OrderingApiClient : IOrderingApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderingApiClient> _logger;

    public OrderingApiClient(HttpClient httpClient, ILogger<OrderingApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsOrderAlreadyPaidAsync(int orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"api/orders/{orderId}?api-version=1.0",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Ordering API returned 404 for order {OrderId}. Assuming order does not exist and proceeding with simulated payment.",
                    orderId);
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Ordering API returned non-success status code {StatusCode} for order {OrderId}. Proceeding with simulated payment.",
                    response.StatusCode,
                    orderId);
                return false;
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var order = await JsonSerializer.DeserializeAsync<OrderDto>(
                contentStream,
                SerializerOptions,
                cancellationToken);

            if (order is null)
            {
                _logger.LogWarning(
                    "Ordering API returned an empty response for order {OrderId}. Proceeding with simulated payment.",
                    orderId);
                return false;
            }

            var isPaid = string.Equals(order.Status, "Paid", StringComparison.OrdinalIgnoreCase);

            if (isPaid)
            {
                _logger.LogInformation(
                    "Order {OrderId} is already marked as paid (status: {Status}). Skipping simulated payment.",
                    orderId,
                    order.Status);
            }

            return isPaid;
        }
        catch (OperationCanceledException)
        {
            // Honor cancellation but default to existing behavior (simulated payment) when cancelled.
            _logger.LogWarning(
                "Cancellation was requested while checking payment status for order {OrderId}. Proceeding with simulated payment.",
                orderId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while checking payment status for order {OrderId} from Ordering API. Proceeding with simulated payment.",
                orderId);
            return false;
        }
    }

    private sealed class OrderDto
    {
        public int OrderNumber { get; set; }
        public string Status { get; set; }
    }
}

