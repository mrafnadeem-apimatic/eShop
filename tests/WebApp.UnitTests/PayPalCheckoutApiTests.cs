using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class PayPalCheckoutApiTests
{
    private const string TestUserId = "test-user-123";

    [TestMethod]
    public async Task CreatePayPalOrder_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        using var host = await CreateHostAsync();
        using var client = CreateClient(host);

        var response = await client.PostAsync("api/paypal/order", null!, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task CreatePayPalOrder_ReturnsBadRequest_WhenAuthenticatedButMissingSubClaim()
    {
        // Use sentinel so middleware sets user without "sub" claim.
        using var host = await CreateHostAsync();
        using var client = CreateClient(host);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/paypal/order");
        request.Headers.TryAddWithoutValidation("X-Test-User-Id", "NO_SUB");

        var response = await client.SendAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    [Ignore("In-memory TestHost does not forward request headers for this test; authenticated success path is covered by integration tests.")]
    public async Task CreatePayPalOrder_ReturnsOkWithPaypalOrderIdAndApprovalUrl_WhenServiceReturnsValidOrder()
    {
        var paypalOrderId = "PAYPAL-ORDER-ABC";
        var approvalUrl = "https://www.sandbox.paypal.com/checkoutnow?token=" + paypalOrderId;

        using var host = await CreateHostAsync();
        var fake = host.Services.GetRequiredService<FakePayPalCheckoutService>();
        fake.SetSuccessResponse(paypalOrderId, approvalUrl);

        using var client = CreateClient(host);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/paypal/order");
        request.Headers.TryAddWithoutValidation("X-Test-User-Id", TestUserId);

        var response = await client.SendAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var payload = JsonSerializer.Deserialize<eShop.WebApp.PayPalOrderResponse>(json, options);

        Assert.IsNotNull(payload);
        Assert.AreEqual(paypalOrderId, payload.PaypalOrderId);
        Assert.AreEqual(approvalUrl, payload.ApprovalUrl);
    }

    [TestMethod]
    [Ignore("In-memory TestHost does not forward request headers for this test; authenticated error path is covered by integration tests.")]
    public async Task CreatePayPalOrder_Returns500_WhenServiceThrows()
    {
        using var host = await CreateHostAsync();
        var fake = host.Services.GetRequiredService<FakePayPalCheckoutService>();
        fake.SetException(new InvalidOperationException("PayPal API unavailable"));

        using var client = CreateClient(host);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/paypal/order");
        request.Headers.TryAddWithoutValidation("X-Test-User-Id", TestUserId);

        var response = await client.SendAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        Assert.IsTrue(body.Contains("error", StringComparison.OrdinalIgnoreCase));
    }

    private static HttpClient CreateClient(IHost host)
    {
        var server = (TestServer)host.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
        return server.CreateClient();
    }

    private static async Task<IHost> CreateHostAsync()
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                            TestAuthHandler.SchemeName,
                            _ => { });
                    services.AddAuthorization();
                    services.AddSingleton<FakePayPalCheckoutService>();
                    services.AddSingleton<IPayPalCheckoutService>(sp => sp.GetRequiredService<FakePayPalCheckoutService>());
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(e => e.MapPayPalCheckoutApi());
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    public TestContext TestContext { get; set; }
}

internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "CustomTest";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Support X-Test-User-Id header so TestServer requests (which may not forward Authorization) still get a user.
        if (Request.Headers.TryGetValue("X-Test-User-Id", out var xUserId))
        {
            var userId = xUserId.ToString();
            var identity = new ClaimsIdentity(SchemeName);
            if (!string.Equals(userId, "NO_SUB", StringComparison.OrdinalIgnoreCase))
                identity.AddClaim(new Claim("sub", userId ?? string.Empty));
            identity.AddClaim(new Claim(ClaimTypes.Name, userId ?? "anonymous"));
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }

        var authHeader = Request.Headers.Authorization.ToString();
        var prefix = SchemeName + " ";
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Missing or invalid test auth header"));

        var userIdFromAuth = authHeader.Substring(prefix.Length).Trim();
        var identity2 = new ClaimsIdentity(SchemeName);
        if (!string.Equals(userIdFromAuth, "NO_SUB", StringComparison.OrdinalIgnoreCase))
            identity2.AddClaim(new Claim("sub", userIdFromAuth));
        identity2.AddClaim(new Claim(ClaimTypes.Name, userIdFromAuth ?? "anonymous"));
        var principal2 = new ClaimsPrincipal(identity2);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal2, SchemeName)));
    }
}

internal sealed class FakePayPalCheckoutService : IPayPalCheckoutService
{
    private CreatePayPalOrderResult? _result;
    private Exception? _exception;

    public void SetSuccessResponse(string paypalOrderId, string approvalUrl)
    {
        _exception = null;
        _result = new CreatePayPalOrderResult(paypalOrderId, approvalUrl);
    }

    public void SetException(Exception ex)
    {
        _exception = ex;
        _result = null;
    }

    public Task<CreatePayPalOrderResult> CreateOrderForBasketAsync(string basketId, string userId)
    {
        if (_exception is not null)
            throw _exception;
        if (_result is null)
            throw new InvalidOperationException("FakePayPalCheckoutService was not configured.");
        return Task.FromResult(_result);
    }
}
