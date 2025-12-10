using API.Constants;
using API.Entities.Enums;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class BrowserHelperTests
{
    #region DetermineClientType Tests

    [Theory]
    [InlineData("", ClientDeviceType.Unknown)]
    [InlineData(null, ClientDeviceType.Unknown)]
    public void DetermineClientType_ReturnsUnknown_ForEmptyOrNullUserAgent(string userAgent, ClientDeviceType expected)
    {
        var result = BrowserHelper.DetermineClientType(userAgent);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Linux; Android 11; Pixel 5) KOReader/2023.10", ClientDeviceType.KoReader)]
    [InlineData("koreader/1.0", ClientDeviceType.KoReader)]
    [InlineData("KOREADER", ClientDeviceType.KoReader)]
    [InlineData("Mozilla/5.0 (Linux; U; Android 2.0; en-us;) AppleWebKit/533.1 (KHTML, like Gecko) Version/4.0 Mobile Safari/533.1 (Kobo Touch)", ClientDeviceType.KoReader)]
    public void DetermineClientType_ReturnsKOReader_ForKOReaderUserAgents(string userAgent, ClientDeviceType expected)
    {
        var result = BrowserHelper.DetermineClientType(userAgent);
        Assert.Equal(expected, result);
    }


    [Theory]
    [InlineData("Panels/2.0", ClientDeviceType.Panels)]
    [InlineData("panels", ClientDeviceType.Panels)]
    public void DetermineClientType_ReturnsPanels_ForPanelsUserAgents(string userAgent, ClientDeviceType expected)
    {
        var result = BrowserHelper.DetermineClientType(userAgent);
        Assert.Equal(expected, result);
    }


    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0")]
    public void DetermineClientType_ReturnsWebBrowser_ForBrowserUserAgents(string userAgent)
    {
        var result = BrowserHelper.DetermineClientType(userAgent);
        Assert.Equal(ClientDeviceType.WebBrowser, result);
    }

    [Theory]
    [InlineData("curl/7.68.0")]
    [InlineData("PostmanRuntime/7.32.3")]
    [InlineData("SomeCustomClient/1.0")]
    public void DetermineClientType_ReturnsUnknown_ForUnrecognizedUserAgents(string userAgent)
    {
        var result = BrowserHelper.DetermineClientType(userAgent);
        Assert.Equal(ClientDeviceType.Unknown, result);
    }

    [Fact]
    public void DetermineClientType_PrioritizesSpecificClients_OverWebBrowser()
    {
        // KOReader running on Android with Chrome-like UA
        var userAgent = "Mozilla/5.0 (Linux; Android 11) AppleWebKit/537.36 Chrome/91.0 KOReader/2023.10";
        var result = BrowserHelper.DetermineClientType(userAgent);
        Assert.Equal(ClientDeviceType.KoReader, result);
    }

    #endregion

    #region DetectPlatform Tests

    [Theory]
    [InlineData("", ClientDevicePlatform.Unknown)]
    [InlineData(null, ClientDevicePlatform.Unknown)]
    public void DetectPlatform_ReturnsUnknown_ForEmptyOrNullUserAgent(string userAgent, ClientDevicePlatform expected)
    {
        var result = BrowserHelper.DetectPlatform(userAgent);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36", ClientDevicePlatform.Windows)]
    [InlineData("Mozilla/5.0 (Windows NT 6.1; WOW64)", ClientDevicePlatform.Windows)]
    [InlineData("Mozilla/5.0 (win32)", ClientDevicePlatform.Windows)]
    [InlineData("Mozilla/5.0 (win64)", ClientDevicePlatform.Windows)]
    [InlineData("WINDOWS", ClientDevicePlatform.Windows)]
    public void DetectPlatform_ReturnsWindows_ForWindowsUserAgents(string userAgent, ClientDevicePlatform expected)
    {
        var result = BrowserHelper.DetectPlatform(userAgent);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15", ClientDevicePlatform.MacOs)]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 11_6) AppleWebKit/537.36", ClientDevicePlatform.MacOs)]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15", ClientDevicePlatform.MacOs)]
    [InlineData("macintosh", ClientDevicePlatform.MacOs)]
    public void DetectPlatform_ReturnsMacOS_ForMacOSUserAgents(string userAgent, ClientDevicePlatform expected)
    {
        var result = BrowserHelper.DetectPlatform(userAgent);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36", ClientDevicePlatform.Linux)]
    [InlineData("Mozilla/5.0 (X11; Ubuntu; Linux x86_64)", ClientDevicePlatform.Linux)]
    [InlineData("linux", ClientDevicePlatform.Linux)]
    [InlineData("Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:34.0) Gecko/20100101 Firefox/34.0", ClientDevicePlatform.Linux)]
    public void DetectPlatform_ReturnsLinux_ForLinuxUserAgents(string userAgent, ClientDevicePlatform expected)
    {
        var result = BrowserHelper.DetectPlatform(userAgent);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15", ClientDevicePlatform.Ios)]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 17_2 like Mac OS X) AppleWebKit/605.1.15", ClientDevicePlatform.Ios)]
    [InlineData("Mozilla/5.0 (iPod touch; CPU iPhone OS 12_0 like Mac OS X)", ClientDevicePlatform.Ios)]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Mobile/15E148 Safari/604.1", ClientDevicePlatform.Ios)]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 18_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Mobile/15E148 Safari/604.1", ClientDevicePlatform.Ios)]
    [InlineData("iphone", ClientDevicePlatform.Ios)]
    [InlineData("ipad", ClientDevicePlatform.Ios)]
    [InlineData("ipod", ClientDevicePlatform.Ios)]
    [InlineData("mac os", ClientDevicePlatform.Ios)]
    public void DetectPlatform_ReturnsIOS_ForIOSUserAgents(string userAgent, ClientDevicePlatform expected)
    {
        var result = BrowserHelper.DetectPlatform(userAgent);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36", ClientDevicePlatform.Android)]
    [InlineData("Mozilla/5.0 (Linux; Android 11; SM-G991B)", ClientDevicePlatform.Android)]
    [InlineData("android", ClientDevicePlatform.Android)]
    [InlineData("Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Mobile Safari/537.36", ClientDevicePlatform.Android)]
    [InlineData("Mozilla/5.0 (Linux; Android 15; SM-S931B Build/AP3A.240905.015.A2; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/127.0.6533.103 Mobile Safari/537.36", ClientDevicePlatform.Android)]
    public void DetectPlatform_ReturnsAndroid_ForAndroidUserAgents(string userAgent, ClientDevicePlatform expected)
    {
        var result = BrowserHelper.DetectPlatform(userAgent);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetectPlatform_ReturnsAndroid_NotLinux_ForAndroidUserAgents()
    {
        // Android UAs contain "Linux" but should be detected as Android
        const string userAgent = "Mozilla/5.0 (Linux; Android 11; Pixel 5) AppleWebKit/537.36";
        var result = BrowserHelper.DetectPlatform(userAgent);
        Assert.Equal(ClientDevicePlatform.Android, result);
    }

    [Theory]
    [InlineData("curl/7.68.0")]
    [InlineData("PostmanRuntime/7.32.3")]
    [InlineData("FreeBSD")]
    [InlineData("OpenBSD")]
    public void DetectPlatform_ReturnsUnknown_ForUnrecognizedPlatforms(string userAgent)
    {
        var result = BrowserHelper.DetectPlatform(userAgent);
        Assert.Equal(ClientDevicePlatform.Unknown, result);
    }

    [Fact]
    public void DetectPlatform_IsCaseInsensitive()
    {
        Assert.Equal(ClientDevicePlatform.Windows, BrowserHelper.DetectPlatform("WINDOWS NT 10.0"));
        Assert.Equal(ClientDevicePlatform.Android, BrowserHelper.DetectPlatform("ANDROID 13"));
        Assert.Equal(ClientDevicePlatform.MacOs, BrowserHelper.DetectPlatform("MACINTOSH; INTEL MAC OS X"));
        Assert.Equal(ClientDevicePlatform.Ios, BrowserHelper.DetectPlatform("IPHONE"));
        Assert.Equal(ClientDevicePlatform.Linux, BrowserHelper.DetectPlatform("LINUX X86_64"));
    }

    #endregion

    #region Real-World User Agent Tests

    [Theory]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ClientDeviceType.WebBrowser,
        ClientDevicePlatform.Windows)]
    [InlineData(
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ClientDeviceType.WebBrowser,
        ClientDevicePlatform.MacOs)]
    [InlineData(
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ClientDeviceType.WebBrowser,
        ClientDevicePlatform.Linux)]
    [InlineData(
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1",
        ClientDeviceType.WebBrowser,
        ClientDevicePlatform.Ios)]
    [InlineData(
        "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36",
        ClientDeviceType.WebBrowser,
        ClientDevicePlatform.Android)]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
        ClientDeviceType.WebBrowser,
        ClientDevicePlatform.Windows)]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0",
        ClientDeviceType.WebBrowser,
        ClientDevicePlatform.Windows)]

    [InlineData(
        "Mozilla/5.0 (X11; Linux x86_64; Librera) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.2997.373167658 Safari/537.36",
        ClientDeviceType.Librera,
        ClientDevicePlatform.Linux)] // Should be Android but not sure how to actually correct
    public void RealWorld_UserAgents_AreDetectedCorrectly(string userAgent, ClientDeviceType expectedClientType, ClientDevicePlatform expectedPlatform)
    {
        var clientType = BrowserHelper.DetermineClientType(userAgent);
        var platform = BrowserHelper.DetectPlatform(userAgent);

        Assert.Equal(expectedClientType, clientType);
        Assert.Equal(expectedPlatform, platform);
    }

    #endregion
}
