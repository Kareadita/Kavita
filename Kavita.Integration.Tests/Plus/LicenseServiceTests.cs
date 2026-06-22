using System.Threading.Tasks;
using Flurl.Http;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Integration.Tests.Fixtures;
using Kavita.Models.DTOs.KavitaPlus.License;
using Xunit.Abstractions;

namespace Kavita.Integration.Tests.Plus;

[Collection("KavitaPlus")]
[Trait("Category", "Integration")]
public sealed class LicenseServiceTests(KavitaPlusFixture fixture, ITestOutputHelper output)
{
    private void SkipIfNoLicense() =>
        Skip.If(fixture.LicenseKey is null, fixture.SkipReason ?? "No license key available.");

    [SkippableFact]
    public async Task CheckLicense_WithStoredKey_ReturnsTrue()
    {
        SkipIfNoLicense();
        var license = fixture.LicenseKey!;

        var response = await (fixture.ApiUrl + "/api/license/check")
            .WithKavitaPlusHeaders(license)
            .PostJsonAsync(new LicenseValidDto { License = license, InstallId = HashUtil.ServerToken() })
            .ReceiveString();

        output.WriteLine($"check → {response}");
        Assert.Equal("true", response, ignoreCase: true);
    }

    [SkippableFact]
    public async Task CheckLicense_WithGarbageKey_ReturnsFalse()
    {
        SkipIfNoLicense();
        const string badKey = "NOT-A-REAL-LICENSE-KEY";

        var response = await (fixture.ApiUrl + "/api/license/check")
            .WithKavitaPlusHeaders(badKey)
            .PostJsonAsync(new LicenseValidDto { License = badKey, InstallId = HashUtil.ServerToken() })
            .ReceiveString();

        output.WriteLine($"check (bad) → {response}");
        Assert.Equal("false", response, ignoreCase: true);
    }

    [SkippableFact]
    public async Task GetLicenseInfo_WithStoredKey_ReturnsActiveInfo()
    {
        SkipIfNoLicense();
        var license = fixture.LicenseKey!;

        var info = await (fixture.ApiUrl + "/api/license/info")
            .WithKavitaPlusHeaders(license)
            .GetJsonAsync<LicenseInfoDto>();

        output.WriteLine($"IsActive={info?.IsActive} Email={info?.RegisteredEmail}");
        Assert.NotNull(info);
        Assert.True(info.IsActive);
        Assert.False(string.IsNullOrEmpty(info.RegisteredEmail));
    }

    [SkippableFact]
    public async Task CheckSubscription_WithStoredKey_ReturnsTrue()
    {
        SkipIfNoLicense();
        var license = fixture.LicenseKey!;

        var response = await (fixture.ApiUrl + "/api/license/check-sub")
            .WithKavitaPlusHeaders(license)
            .PostJsonAsync(new LicenseValidDto { License = license, InstallId = HashUtil.ServerToken() })
            .ReceiveString();

        output.WriteLine($"check-sub → {response}");
        Assert.Equal("true", response, ignoreCase: true);
    }
}
