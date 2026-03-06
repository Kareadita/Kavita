using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Common.Extensions;
using Kavita.Models.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Kavita.Server.Attributes;

/// <summary>
/// An attribute restricting access to entities based on the user's access to the reading list.
/// Returns 404 Not Found on failure
/// </summary>
/// <param name="failOnMissing"></param>
/// <param name="readingListIdKey"></param>
public class ReadingListAccessAttribute(bool failOnMissing = true, string readingListIdKey = "readingListId")
    : AccessAttribute(readingListIdKey, failOnMissing, false)
{
    protected override Task<bool> CheckAccess(IUnitOfWork unitOfWork, int userId, int entityId, CancellationToken ct)
    {
        return unitOfWork.UserRepository.HasAccessToReadingList(userId, entityId, ct);
    }
}

/// <summary>
/// An attribute restricting access to entities based on the user's access to the person.
/// Returns 404 Not Found on failure
/// </summary>
/// <param name="failOnMissing"></param>
/// <param name="personIdKey"></param>
public class PersonAccessAttribute(bool failOnMissing = true, string personIdKey = "personId")
    : AccessAttribute(personIdKey, failOnMissing)
{
    protected override Task<bool> CheckAccess(IUnitOfWork unitOfWork, int userId, int entityId, CancellationToken ct)
    {
        return unitOfWork.UserRepository.HasAccessToPerson(userId, entityId, ct);
    }
}

/// <summary>
/// An attribute restricting access to entities based on the user's access to the library.
/// Returns 404 Not Found on failure
/// </summary>
/// <param name="failOnMissing"></param>
/// <param name="libraryIdKey"></param>
public class LibraryAccessAttribute(bool failOnMissing = true, string libraryIdKey = "libraryId")
    : AccessAttribute(libraryIdKey, failOnMissing)
{

    protected override Task<bool> CheckAccess(IUnitOfWork unitOfWork, int userId, int entityId, CancellationToken ct)
    {
        return unitOfWork.UserRepository.HasAccessToLibrary(userId, entityId, ct);
    }
}

/// <summary>
/// An attribute restricting access to entities based on the user's access to the series.
/// Returns 404 Not Found on failure
/// </summary>
/// <param name="failOnMissing"></param>
/// <param name="seriesIdKey"></param>
public class SeriesAccessAttribute(bool failOnMissing = true, string seriesIdKey = "seriesId")
    : AccessAttribute(seriesIdKey, failOnMissing)
{

    protected override Task<bool> CheckAccess(IUnitOfWork unitOfWork, int userId, int entityId, CancellationToken ct)
    {
        return unitOfWork.UserRepository.HasAccessToSeries(userId, entityId, ct);
    }
}

/// <summary>
/// An attribute restricting access to entities based on the user's access to the volume.
/// Returns 404 Not Found on failure
/// </summary>
/// <param name="failOnMissing"></param>
/// <param name="volumeIdKey"></param>
public class VolumeAccessAttribute(bool failOnMissing = true, string volumeIdKey = "volumeId")
    : AccessAttribute(volumeIdKey, failOnMissing)
{

    protected override Task<bool> CheckAccess(IUnitOfWork unitOfWork, int userId, int entityId, CancellationToken ct)
    {
        return unitOfWork.UserRepository.HasAccessToVolume(userId, entityId, ct);
    }
}

/// <summary>
/// An attribute restricting access to entities based on the user's access to the chapter.
/// Returns 404 Not Found on failure
/// </summary>
/// <param name="failOnMissing"></param>
/// <param name="chapterIdKey"></param>
public class ChapterAccessAttribute(bool failOnMissing = true, string chapterIdKey = "chapterId")
    : AccessAttribute(chapterIdKey, failOnMissing)
{

    protected override Task<bool> CheckAccess(IUnitOfWork unitOfWork, int userId, int entityId, CancellationToken ct)
    {
        return unitOfWork.UserRepository.HasAccessToChapter(userId, entityId, ct);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public abstract class AccessAttribute(string idKey, bool failOnMissing = true, bool alwaysAllowAdmin = true) : Attribute, IAsyncAuthorizationFilter
{

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (alwaysAllowAdmin && user.IsInRole(PolicyConstants.AdminRole)) return;

        var userId = user.GetUserId();

        var entityId = ExtractId(context.HttpContext, idKey);

        if (entityId == null)
        {
            if (failOnMissing)
            {
                context.Result = new NotFoundResult();
            }
            return;
        }

        var unitOfWork = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();

        var hasAccess = await CheckAccess(unitOfWork, userId, entityId.Value, context.HttpContext.RequestAborted);
        if (!hasAccess)
        {
            context.Result = new NotFoundResult();
        }
    }

    protected abstract Task<bool> CheckAccess(IUnitOfWork unitOfWork, int userId, int entityId, CancellationToken ct);

    private static int? ExtractId(HttpContext httpContext, string key)
    {
        if (httpContext.Request.RouteValues.TryGetValue(key, out var pathVal) &&
            int.TryParse(pathVal?.ToString(), out var pId)) return pId;

        if (int.TryParse(httpContext.Request.Query[key], out var qId)) return qId;

        return null;
    }
}
