using System.Collections.Concurrent;

namespace eShop.WebApp.Services.Payments;

/// <summary>
/// Represents a mapping between a PayPal order and the local basket/user context
/// at the time the PayPal checkout session was created.
/// </summary>
public sealed record PayPalCheckoutSession(
    string PaypalOrderId,
    string BasketId,
    string UserId,
    DateTime CreatedAtUtc);

/// <summary>
/// Abstraction for persisting PayPal checkout sessions so that other parts of the
/// application can correlate a PayPal order with the originating basket and user.
/// </summary>
public interface IPayPalCheckoutSessionStore
{
    /// <summary>
    /// Persists or updates a PayPal checkout session mapping.
    /// </summary>
    Task StoreSessionAsync(PayPalCheckoutSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a stored checkout session by PayPal order identifier, if available.
    /// </summary>
    Task<PayPalCheckoutSession?> GetByPaypalOrderIdAsync(string paypalOrderId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple in-memory implementation of <see cref="IPayPalCheckoutSessionStore"/>.
/// This is suitable for the sample application and development scenarios; a more
/// durable store (for example, a database) can replace this implementation later
/// without changing callers.
/// </summary>
public sealed class InMemoryPayPalCheckoutSessionStore : IPayPalCheckoutSessionStore
{
    private readonly ConcurrentDictionary<string, PayPalCheckoutSession> _sessions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<InMemoryPayPalCheckoutSessionStore> _logger;

    public InMemoryPayPalCheckoutSessionStore(ILogger<InMemoryPayPalCheckoutSessionStore> logger)
    {
        _logger = logger;
    }

    public Task StoreSessionAsync(PayPalCheckoutSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        _sessions[session.PaypalOrderId] = session;

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Stored PayPal checkout session for PayPalOrderId {PaypalOrderId}, BasketId {BasketId}, UserId {UserId}",
                session.PaypalOrderId,
                session.BasketId,
                session.UserId);
        }

        return Task.CompletedTask;
    }

    public Task<PayPalCheckoutSession?> GetByPaypalOrderIdAsync(string paypalOrderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paypalOrderId))
        {
            return Task.FromResult<PayPalCheckoutSession?>(null);
        }

        _sessions.TryGetValue(paypalOrderId, out var session);
        return Task.FromResult(session);
    }
}

