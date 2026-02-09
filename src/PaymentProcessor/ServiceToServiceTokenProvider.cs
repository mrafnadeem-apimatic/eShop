using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eShop.PaymentProcessor;

/// <summary>
/// Configuration for acquiring service-to-service access tokens from the Identity service.
/// </summary>
public sealed class IdentityServiceOptions
{
    /// <summary>
    /// Base URL for the Identity service (for example, http://identity-api).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Client id used for client credentials flow.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret used for client credentials flow.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Scope to request when acquiring an access token (for example, "orders").
    /// </summary>
    public string Scope { get; set; } = string.Empty;
}

/// <summary>
/// Obtains and caches access tokens for service-to-service calls using the client credentials flow.
/// </summary>
public class ServiceToServiceTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<IdentityServiceOptions> _optionsMonitor;
    private readonly ILogger<ServiceToServiceTokenProvider> _logger;

    private string _accessToken = string.Empty;
    private DateTimeOffset _expiresAtUtc;

    public ServiceToServiceTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<IdentityServiceOptions> optionsMonitor,
        ILogger<ServiceToServiceTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns a valid access token for calling downstream services, or null if one cannot be acquired.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Re-use the current token if it is not close to expiry.
        if (!string.IsNullOrEmpty(_accessToken) && _expiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _accessToken;
        }

        var options = _optionsMonitor.CurrentValue;

        if (string.IsNullOrWhiteSpace(options.Url) ||
            string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret) ||
            string.IsNullOrWhiteSpace(options.Scope))
        {
            _logger.LogWarning(
                "Identity configuration is incomplete. Url, ClientId, ClientSecret and Scope are required to acquire a service-to-service access token.");
            return string.Empty;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Identity");

            using var request = new HttpRequestMessage(HttpMethod.Post, "connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                    ["scope"] = options.Scope
                })
            };

            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Identity service returned non-success status code {StatusCode} when requesting a service-to-service access token.",
                    response.StatusCode);
                return string.Empty;
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var tokenResponse = await JsonSerializer.DeserializeAsync<TokenResponse>(
                contentStream,
                cancellationToken: cancellationToken);

            if (tokenResponse?.AccessToken is null)
            {
                _logger.LogWarning("Identity token response did not contain an access token.");
                return string.Empty;
            }

            _accessToken = tokenResponse.AccessToken;

            var expiresInSeconds = tokenResponse.ExpiresIn > 0
                ? tokenResponse.ExpiresIn
                : 3600;

            _expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);

            return _accessToken;
        }
        catch (OperationCanceledException)
        {
            // Honor cancellation and fall back to existing behavior (no token).
            _logger.LogWarning("Cancellation was requested while acquiring a service-to-service access token.");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while acquiring a service-to-service access token from the Identity service.");
            return string.Empty;
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}

