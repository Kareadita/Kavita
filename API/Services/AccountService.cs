﻿using System;
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
        Task<IEnumerable<ApiException>> ValidatePassword(AppUser user, string password);
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
            var passwordValidationIssues = (await ValidatePassword(user, newPassword)).ToList();
            if (passwordValidationIssues.Any()) return passwordValidationIssues;

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

        public async Task<IEnumerable<ApiException>> ValidatePassword(AppUser user, string password)
        {
            foreach (var validator in _userManager.PasswordValidators)
            {
                var validationResult = await validator.ValidateAsync(_userManager, user, password);
                if (!validationResult.Succeeded)
                {
                    return validationResult.Errors.Select(e => new ApiException(400, e.Code, e.Description));
                }
            }

            return Array.Empty<ApiException>();
        }
    }
}
