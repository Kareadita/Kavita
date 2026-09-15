using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services.Reading;

public interface IReadingProfileService
{
    /// <summary>
    /// Returns the ReadingProfile that should be applied to the given series, walks up the tree.
    /// Series (Implicit) -> Series (User) -> Library (User) -> Default
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="activeDeviceId"></param>
    /// <param name="skipImplicit"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> GetReadingProfileDtoForSeries(int userId, int libraryId, int seriesId, int? activeDeviceId, bool skipImplicit = false, CancellationToken ct = default);

    /// <summary>
    /// Creates a new reading profile for a user. Name must be unique per user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dto"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> CreateReadingProfile(int userId, UserReadingProfileDto dto, CancellationToken ct = default);

    /// <summary>
    /// Given an implicit profile, promotes it to a profile of kind <see cref="ReadingProfileKind.User"/>, then removes
    /// all links to the series this implicit profile was created for from other reading profiles (if the device id matches
    /// if given)
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <param name="activeDeviceId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> PromoteImplicitProfile(int userId, int profileId, int? activeDeviceId, CancellationToken ct = default);

    /// <summary>
    /// Updates the implicit reading profile for a series, creates one if none exists
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="dto"></param>
    /// <param name="activeDeviceId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> UpdateImplicitReadingProfile(int userId, int libraryId, int seriesId, UserReadingProfileDto dto, int? activeDeviceId, CancellationToken ct = default);

    /// <summary>
    /// Updates the non-implicit reading profile for the given series, and removes implicit profiles
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="dto"></param>
    /// <param name="activeDeviceId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> UpdateParent(int userId, int libraryId, int seriesId, UserReadingProfileDto dto, int? activeDeviceId, CancellationToken ct = default);

    /// <summary>
    /// Updates a given reading profile for a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dto"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <remarks>Does not update connected series and libraries</remarks>
    Task<UserReadingProfileDto> UpdateReadingProfile(int userId, UserReadingProfileDto dto, CancellationToken ct = default);

    /// <summary>
    /// Deletes a given profile for a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="KavitaException">The default profile for the user cannot be deleted</exception>
    Task DeleteReadingProfile(int userId, int profileId, CancellationToken ct = default);

    /// <summary>
    /// Binds the reading profile to the series, and remove the implicit RP from the series if it exists
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileIds"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task SetSeriesProfiles(int userId, List<int> profileIds, int seriesId, CancellationToken ct = default);

    /// <summary>
    /// Binds the reading profile to many series, and remove the implicit RP from the series if it exists
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileIds"></param>
    /// <param name="seriesIds"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task BulkSetSeriesProfiles(int userId, List<int> profileIds, List<int> seriesIds, CancellationToken ct = default);

    /// <summary>
    /// Remove all reading profiles bound to the series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ClearSeriesProfile(int userId, int seriesId, CancellationToken ct = default);

    /// <summary>
    /// Bind the reading profile to the library
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileIds"></param>
    /// <param name="libraryId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task SetLibraryProfiles(int userId, List<int> profileIds, int libraryId, CancellationToken ct = default);

    /// <summary>
    /// Remove the reading profile bound to the library, if it exists
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ClearLibraryProfile(int userId, int libraryId, CancellationToken ct = default);

    /// <summary>
    /// Returns the all bound Reading Profile to a Library
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<List<UserReadingProfileDto>> GetReadingProfileDtosForLibrary(int userId, int libraryId, CancellationToken ct = default);

    /// <summary>
    /// Returns the all bound Reading Profile to a Series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<List<UserReadingProfileDto>> GetReadingProfileDtosForSeries(int userId, int seriesId, CancellationToken ct = default);

    /// <summary>
    /// Set the assigned devices for the given reading profile. Then removes all duplicate links, ensuring each series
    /// and library only has one profile per device
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <param name="deviceIds"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task SetProfileDevices(int userId, int profileId, List<int> deviceIds, CancellationToken ct = default);

    /// <summary>
    /// Remove device ids from all profiles, does **NOT** commit
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="deviceId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task RemoveDeviceLinks(int userId, int deviceId, CancellationToken ct = default);
}
