using System;
using System.Threading.Tasks;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class RateLimiterTests
{
    [Fact]
    public void AcquireTokens_Successful()
    {
        // Arrange
        var limiter = new RateLimiter(3, TimeSpan.FromSeconds(1));

        // Act & Assert
        Assert.True(limiter.TryAcquire("test_key"));
        Assert.True(limiter.TryAcquire("test_key"));
        Assert.True(limiter.TryAcquire("test_key"));
    }

    [Fact]
    public void AcquireTokens_ExceedLimit()
    {
        // Arrange
        var limiter = new RateLimiter(2, TimeSpan.FromSeconds(10), false);

        // Act
        limiter.TryAcquire("test_key");
        limiter.TryAcquire("test_key");

        // Assert
        Assert.False(limiter.TryAcquire("test_key"));
    }

    [Fact]
    public async Task AcquireTokens_Refill()
    {
        // Arrange
        var limiter = new RateLimiter(2, TimeSpan.FromSeconds(1));

        // Act
        limiter.TryAcquire("test_key");
        limiter.TryAcquire("test_key");

        // Wait for refill
        await Task.Delay(1100);

        // Assert
        Assert.True(limiter.TryAcquire("test_key"));
    }

    [Fact]
    public async Task AcquireTokens_Refill_WithOff()
    {
        // Arrange
        var limiter = new RateLimiter(2, TimeSpan.FromSeconds(10), false);

        // Act
        limiter.TryAcquire("test_key");
        limiter.TryAcquire("test_key");

        // Wait for refill
        await Task.Delay(2100);

        // Assert
        Assert.False(limiter.TryAcquire("test_key"));
    }

    [Fact]
    public void AcquireTokens_MultipleKeys()
    {
        // Arrange
        var limiter = new RateLimiter(2, TimeSpan.FromSeconds(1));

        // Act & Assert
        Assert.True(limiter.TryAcquire("key1"));
        Assert.True(limiter.TryAcquire("key2"));
    }
}
