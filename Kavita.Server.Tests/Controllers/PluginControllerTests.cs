using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.Entities.User;
using Kavita.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Server.Tests.Controllers;

public class PluginControllerTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    [Fact]
    public async Task Authenticate_UserWithoutLoginRole_IsRefused()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var user = new AppUserBuilder("disabled", "disabled@scrubbed.invalid").Build();
        context.AppUser.Add(user);
        await context.SaveChangesAsync();
        context.AppUserAuthKey.Add(new AppUserAuthKey { Key = "disabled-user-key", Name = "plugin", AppUserId = user.Id });
        await context.SaveChangesAsync();
        await AddUserWithRole(context, user.Id, PolicyConstants.PlebRole);

        var tokenService = Substitute.For<ITokenService>();
        var controller = new PluginController(unitOfWork, tokenService, Substitute.For<ILogger<PluginController>>(),
            Substitute.For<ILocalizationService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Authenticate("disabled-user-key", "test-plugin");

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        await tokenService.DidNotReceiveWithAnyArgs().CreateToken(default!, default);
    }
}
