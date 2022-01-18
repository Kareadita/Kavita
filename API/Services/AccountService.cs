using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public interface IAccountService
    {
        Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword);
    }

    public class AccountService : IAccountService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<AccountService> _logger;
        public const string DefaultPassword = "[k.2@RZ!mxCQkJzE";

        public AccountService(UserManager<AppUser> userManager, ILogger<AccountService> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IEnumerable<ApiException>> ChangeUserPassword(AppUser user, string newPassword)
        {
            foreach (var validator in _userManager.PasswordValidators)
            {
                var validationResult = await validator.ValidateAsync(_userManager, user, newPassword);
                if (!validationResult.Succeeded)
                {
                    return validationResult.Errors.Select(e => new ApiException(400, e.Code, e.Description));
                }
            }

            var result = await _userManager.RemovePasswordAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Could not update password");
                return result.Errors.Select(e => new ApiException(400, e.Code, e.Description));
            }


            result = await _userManager.AddPasswordAsync(user, newPassword);
            if (!result.Succeeded)
            {
                _logger.LogError("Could not update password");
                return result.Errors.Select(e => new ApiException(400, e.Code, e.Description));
            }

            return new List<ApiException>();
        }
    }
}
