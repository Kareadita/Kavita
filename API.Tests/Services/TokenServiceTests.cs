using API.Services;
using Xunit;

namespace API.Tests.Services;

public class TokenServiceTests
{
    [Fact]
    public void HasTokenExpired_OldToken()
    {
        // ValidTo: 1/1/1990
        var result = TokenService.HasTokenExpired("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjYzMzgzMDM5OX0.KM_cUKSaCJL3ts0Qim3ZHUeJT7yf-wKoLdKb0rx0VbU");

        Assert.True(result);
    }

    [Fact]
    public void HasTokenExpired_ValidInFuture()
    {
        // ValidTo: 4/11/2200
        var result = TokenService.HasTokenExpired("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjcyNjg0ODYzOTl9.nZrN5USbUmMYDKwkPoMtEAhTeYTeaikgAeSzDPj5kZQ");
        Assert.False(result);
    }

    [Fact]
    public void HasTokenExpired_NoToken()
    {
        var result = TokenService.HasTokenExpired("");
        Assert.True(result);
    }
}
