using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using eShop.WebApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace eShop.WebApp.UnitTests;

[TestClass]
public class PayPalCheckoutApiTests
{
    [TestMethod]
    public void MapPayPalCheckoutApi_MapsCreateOrderEndpoint_WithAuthorizationAndPostVerb()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(Substitute.For<IPayPalCheckoutService>());

        var app = builder.Build();

        // Act
        app.MapPayPalCheckoutApi();

        var routeBuilder = (IEndpointRouteBuilder)app;
        var endpoints = routeBuilder.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        var endpoint = endpoints.Single(e =>
            string.Equals(e.RoutePattern.RawText, "api/paypal/order", StringComparison.OrdinalIgnoreCase));

        // Assert HTTP method metadata
        var httpMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();
        Assert.IsNotNull(httpMethods);
        CollectionAssert.Contains(httpMethods.HttpMethods.ToList(), HttpMethods.Post);

        // Assert authorization metadata applied via RequireAuthorization
        var authorize = endpoint.Metadata.GetMetadata<IAuthorizeData>();
        Assert.IsNotNull(authorize);
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_ReturnsUnauthorized_WhenUserIsUnauthenticated()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated

        var service = Substitute.For<IPayPalCheckoutService>();
        var logger = Substitute.For<ILogger<PayPalCheckoutService>>();

        // Act
        var result = await PayPalCheckoutApi.CreatePayPalOrderAsync(httpContext, service, logger);

        // Assert
        Assert.IsInstanceOfType(result, typeof(IStatusCodeHttpResult));
        var statusCodeResult = (IStatusCodeHttpResult)result;
        Assert.AreEqual(StatusCodes.Status401Unauthorized, statusCodeResult.StatusCode);

        await service.DidNotReceiveWithAnyArgs()
            .CreateOrderForBasketAsync(default!, default!, default);
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_ReturnsBadRequest_WhenSubClaimMissing()
    {
        // Arrange: authenticated identity but no "sub" claim
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.Name, "Test User"));
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var service = Substitute.For<IPayPalCheckoutService>();
        var logger = Substitute.For<ILogger<PayPalCheckoutService>>();

        // Act
        var result = await PayPalCheckoutApi.CreatePayPalOrderAsync(httpContext, service, logger);

        // Assert
        Assert.IsInstanceOfType(result, typeof(IStatusCodeHttpResult));
        var statusCodeResult = (IStatusCodeHttpResult)result;
        Assert.AreEqual(StatusCodes.Status400BadRequest, statusCodeResult.StatusCode);

        Assert.IsInstanceOfType(result, typeof(IValueHttpResult));
        var valueResult = (IValueHttpResult)result;
        Assert.IsInstanceOfType(valueResult.Value, typeof(ProblemDetails));

        var problem = (ProblemDetails)valueResult.Value!;
        Assert.AreEqual("User identifier is missing.", problem.Detail);

        await service.DidNotReceiveWithAnyArgs()
            .CreateOrderForBasketAsync(default!, default!, default);
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_CallsServiceAndReturnsOk_WhenUserIsValid()
    {
        // Arrange
        const string userId = "user-123";
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim("sub", userId));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var expectedResponse = new PayPalOrderResponse(
            PaypalOrderId: "PAYPAL-ORDER-ID",
            ApprovalUrl: "https://example.test/approval");

        var service = Substitute.For<IPayPalCheckoutService>();
        service
            .CreateOrderForBasketAsync(userId, userId, httpContext.RequestAborted)
            .Returns(expectedResponse);

        var logger = Substitute.For<ILogger<PayPalCheckoutService>>();

        // Act
        var result = await PayPalCheckoutApi.CreatePayPalOrderAsync(httpContext, service, logger);

        // Assert: service was called with basketId and userId both equal to sub claim
        await service.Received(1)
            .CreateOrderForBasketAsync(userId, userId, httpContext.RequestAborted);

        Assert.IsInstanceOfType(result, typeof(Ok<PayPalOrderResponse>));
        var okResult = (Ok<PayPalOrderResponse>)result;
        Assert.AreEqual(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.AreEqual(expectedResponse, okResult.Value);
    }

    [TestMethod]
    public async Task CreatePayPalOrderAsync_ReturnsProblem_WhenServiceThrows()
    {
        // Arrange
        const string userId = "user-123";
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim("sub", userId));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var service = Substitute.For<IPayPalCheckoutService>();
        service
            .CreateOrderForBasketAsync(userId, userId, httpContext.RequestAborted)
            .Returns(Task.FromException<PayPalOrderResponse>(new InvalidOperationException("Boom")));

        var logger = Substitute.For<ILogger<PayPalCheckoutService>>();

        // Act
        var result = await PayPalCheckoutApi.CreatePayPalOrderAsync(httpContext, service, logger);

        // Assert
        Assert.IsInstanceOfType(result, typeof(IStatusCodeHttpResult));
        var statusCodeResult = (IStatusCodeHttpResult)result;
        Assert.AreEqual(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);

        Assert.IsInstanceOfType(result, typeof(IValueHttpResult));
        var valueResult = (IValueHttpResult)result;
        Assert.IsInstanceOfType(valueResult.Value, typeof(ProblemDetails));

        var problem = (ProblemDetails)valueResult.Value!;
        Assert.AreEqual("An error occurred while creating the PayPal order.", problem.Detail);
    }
}

