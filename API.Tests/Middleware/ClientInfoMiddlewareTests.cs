using System;
using System.Threading.Tasks;
using API.Constants;
using API.Entities.Enums;
using API.Entities.Progress;
using API.Middleware;
using API.Services.Reading;
using API.Services.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Middleware;
#nullable enable

public class ClientInfoMiddlewareTests
{
    private readonly ILogger<ClientInfoMiddleware> _logger;
    private readonly IUserContext _userContext;

    public ClientInfoMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<ClientInfoMiddleware>>();
        _userContext = Substitute.For<IUserContext>();
    }

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_SetsClientInfo_AndCallsNextMiddleware()
    {

        var nextCalled = false;
        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_SetsClientInfoAccessor_WithExtractedClientInfo()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            // Capture within the async context where it's set
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext(userAgent: "Mozilla/5.0 (Windows NT 10.0) Chrome/120.0");

        _userContext.GetAuthenticationType().Returns(AuthenticationType.JWT);

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0) Chrome/120.0", capturedClientInfo.UserAgent);
        Assert.Equal(AuthenticationType.JWT, capturedClientInfo.AuthType);
    }

    [Fact]
    public async Task InvokeAsync_SetsClientDeviceId_FromHeader()
    {

        string? capturedUiFingerprint = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedUiFingerprint = accessor.CurrentUiFingerprint;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext();
        context.Request.Headers[Headers.ClientDeviceFingerprint] = "device-123";

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.Equal("device-123", capturedUiFingerprint);
    }

    [Fact]
    public async Task InvokeAsync_HandlesEmptyDeviceId()
    {

        string? capturedUiFingerprint = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedUiFingerprint = accessor.CurrentUiFingerprint;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext();
        // No device ID header set

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert - should not throw
        Assert.True(string.IsNullOrEmpty(capturedUiFingerprint));
    }

    #endregion

    #region ExtractClientInfo Tests

    [Fact]
    public async Task ExtractClientInfo_ParsesKavitaClientHeader_WhenPresent()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext(
            userAgent: "Mozilla/5.0 (Windows) Chrome/120.0",
            kavitaClient: "web-app/1.2.3 (Chrome/120.0; Windows; Desktop; 1920x1080; landscape)"
        );

        _userContext.GetAuthenticationType().Returns(AuthenticationType.JWT);

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal(ClientDeviceType.WebApp, capturedClientInfo.ClientType);
        Assert.Equal("1.2.3", capturedClientInfo.AppVersion);
        Assert.Equal("Chrome", capturedClientInfo.Browser);
        Assert.Equal("120.0", capturedClientInfo.BrowserVersion);
        Assert.Equal(ClientDevicePlatform.Windows, capturedClientInfo.Platform);
        Assert.Equal("Desktop", capturedClientInfo.DeviceType);
        Assert.Equal(1920, capturedClientInfo.ScreenWidth);
        Assert.Equal(1080, capturedClientInfo.ScreenHeight);
        Assert.Equal("landscape", capturedClientInfo.Orientation);
        Assert.Equal("Mozilla/5.0 (Windows) Chrome/120.0", capturedClientInfo.UserAgent);
        Assert.Equal(AuthenticationType.JWT, capturedClientInfo.AuthType);
    }

    [Fact]
    public async Task ExtractClientInfo_ParsesKavitaClientHeader_WithoutOrientation()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext(
            kavitaClient: "web-app/2.0.0 (Firefox/121.0; macOS; Mobile; 375x667)"
        );

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal(ClientDeviceType.WebApp, capturedClientInfo.ClientType);
        Assert.Equal("2.0.0", capturedClientInfo.AppVersion);
        Assert.Equal("Firefox", capturedClientInfo.Browser);
        Assert.Equal("121.0", capturedClientInfo.BrowserVersion);
        Assert.Equal(ClientDevicePlatform.MacOs, capturedClientInfo.Platform);
        Assert.Equal("Mobile", capturedClientInfo.DeviceType);
        Assert.Equal(375, capturedClientInfo.ScreenWidth);
        Assert.Equal(667, capturedClientInfo.ScreenHeight);
        Assert.Null(capturedClientInfo.Orientation);
    }

    [Fact]
    public async Task ExtractClientInfo_FallsBackToUserAgent_WhenKavitaHeaderInvalid()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext(
            userAgent: "Mozilla/5.0 (Linux; Android 13) Chrome/120.0",
            kavitaClient: "invalid-format-here"
        );

        _userContext.GetAuthenticationType().Returns(AuthenticationType.AuthKey);

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal(ClientDeviceType.WebApp, capturedClientInfo.ClientType); // Default from fallback
        Assert.Equal("Mozilla/5.0 (Linux; Android 13) Chrome/120.0", capturedClientInfo.UserAgent);
    }

    [Fact]
    public async Task ExtractClientInfo_FallsBackToUserAgent_WhenKavitaHeaderMissing()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext(
            userAgent: "KOReader/2023.10 (Linux; Android 11)"
        );

        _userContext.GetAuthenticationType().Returns(AuthenticationType.OIDC);

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal(ClientDeviceType.KoReader, capturedClientInfo.ClientType); // Detected from UA
        Assert.Equal(ClientDevicePlatform.Android, capturedClientInfo.Platform); // Detected from UA
        Assert.Equal(AuthenticationType.OIDC, capturedClientInfo.AuthType);
    }

    #endregion

    #region IP Address Extraction Tests

    [Fact]
    public async Task ExtractClientInfo_ExtractsIPFromXForwardedFor_WhenPresent()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext();
        context.Request.Headers[Headers.ForwardedFor] = "203.0.113.1, 198.51.100.1";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal("203.0.113.1", capturedClientInfo.IpAddress); // First IP in X-Forwarded-For
    }

    [Fact]
    public async Task ExtractClientInfo_ExtractsIPFromXRealIP_WhenXForwardedForMissing()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext();
        context.Request.Headers[Headers.RealIp] = "203.0.113.5";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal("203.0.113.5", capturedClientInfo.IpAddress);
    }

    [Fact]
    public async Task ExtractClientInfo_FallsBackToRemoteIpAddress_WhenNoProxyHeaders()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal("192.168.1.100", capturedClientInfo.IpAddress);
    }

    [Fact]
    public async Task ExtractClientInfo_ReturnsUnknown_WhenNoIpAddressAvailable()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext();
        context.Connection.RemoteIpAddress = null;

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal(string.Empty, capturedClientInfo.IpAddress);
    }

    [Fact]
    public async Task ExtractClientInfo_TrimsWhitespaceFromXForwardedFor()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext();
        context.Request.Headers[Headers.ForwardedFor] = "  203.0.113.1  ,  198.51.100.1  ";

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal("203.0.113.1", capturedClientInfo.IpAddress);
    }

    #endregion

    #region CapturedAt Timestamp Tests

    [Fact]
    public async Task ExtractClientInfo_SetsCapturedAtToCurrentUtcTime()
    {

        var before = DateTime.UtcNow;
        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context, _userContext);
        var after = DateTime.UtcNow;

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.InRange(capturedClientInfo.CapturedAt, before, after);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ExtractClientInfo_HandlesEmptyUserAgent()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext(userAgent: "");

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert - should not throw
        Assert.NotNull(capturedClientInfo);
    }

    [Fact]
    public async Task ExtractClientInfo_ParsesKavitaHeader_WithNumericValuesInScreenResolution()
    {

        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();

        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext(
            kavitaClient: "web-app/1.0.0 (Chrome/120.0; Windows; Desktop; 3840x2160; portrait)"
        );

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Assert
        Assert.NotNull(capturedClientInfo);
        Assert.Equal(3840, capturedClientInfo.ScreenWidth);
        Assert.Equal(2160, capturedClientInfo.ScreenHeight);
        Assert.Equal("portrait", capturedClientInfo.Orientation);
    }

    [Fact]
    public async Task ExtractClientInfo_HandlesKavitaHeader_WithInvalidScreenResolution()
    {
        ClientInfoData? capturedClientInfo = null;
        var accessor = new ClientInfoAccessor();
        Task Next(HttpContext ctx)
        {
            capturedClientInfo = accessor.Current;
            return Task.CompletedTask;
        }

        var middleware = new ClientInfoMiddleware(Next, _logger);
        var context = CreateHttpContext(
            userAgent: "Mozilla/5.0 (Windows) Chrome/120.0",
            kavitaClient: "web-app/1.0.0 (Chrome/120.0; Windows; Desktop; ABCxDEF)"
        );

        // Act
        await middleware.InvokeAsync(context, _userContext);

        // Client with non-numeric resolution will not match the regex and return default implementation
        Assert.NotNull(capturedClientInfo);
        Assert.Equal("Mozilla/5.0 (Windows) Chrome/120.0", capturedClientInfo.UserAgent);
    }

    #endregion

    #region Helper Methods

    private static DefaultHttpContext CreateHttpContext(
        string userAgent = "Mozilla/5.0 (Windows NT 10.0) Chrome/120.0",
        string? kavitaClient = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.UserAgent = userAgent;

        if (kavitaClient != null)
        {
            context.Request.Headers[Headers.KavitaClient] = kavitaClient;
        }

        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        return context;
    }

    #endregion
}
