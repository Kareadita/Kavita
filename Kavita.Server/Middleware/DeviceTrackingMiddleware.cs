using System;
using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.API.Services;
using Kavita.API.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Middleware;

/// <summary>
/// Middleware that identifies and tracks device activity for authenticated requests.
/// Runs after authentication middleware and ClientInfoMiddleware.
/// Can be skipped on specific endpoints using [SkipDeviceTracking] attribute.
/// </summary>
public class DeviceTrackingMiddleware(RequestDelegate next, ILogger<DeviceTrackingMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IClientDeviceService clientDeviceService,
        IDeviceTrackingService deviceTrackingService,
        IClientInfoAccessor clientInfoAccessor,
        IUserContext userContext)
    {
        var endpoint = context.GetEndpoint();
        var skipTracking = endpoint?.Metadata.GetMetadata<SkipDeviceTrackingAttribute>() != null;

        if (skipTracking || context.Request.Path.Equals("/"))
        {
            await next(context);
            return;
        }

        try
        {
            var userId = userContext.GetUserId();
            var clientInfo = clientInfoAccessor.Current;
            var clientUiFingerprint = clientInfoAccessor.CurrentUiFingerprint; // string from webapp header

            if (userId.HasValue && clientInfo != null)
            {
                var deviceId = await deviceTrackingService.TrackDeviceAsync(
                    userId.Value,
                    clientInfo,
                    clientUiFingerprint,
                    context.RequestAborted);

                ClientInfoAccessor.SetDeviceId(deviceId);
                logger.LogTrace("Device {DeviceId} tracked for user {UserId}", deviceId, userId);
            }
        }
        catch (OperationCanceledException)
        {
            /* Ignore */
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to track device activity");
        }

        await next(context);
    }
}
