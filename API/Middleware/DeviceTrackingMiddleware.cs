using System;
using System.Diagnostics;
using System.Threading.Tasks;
using API.Services;
using API.Services.Reading;
using API.Services.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace API.Middleware;
#nullable enable

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
        try
        {
            var endpoint = context.GetEndpoint();
            var skipTracking = endpoint?.Metadata.GetMetadata<SkipDeviceTrackingAttribute>() != null;

            if (skipTracking)
            {
                await next(context);
                return;
            }

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
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to track device activity");
        }

        await next(context);
    }
}

/// <summary>
/// Attribute to skip device tracking on specific endpoints.
/// Use for high-frequency endpoints where device tracking adds unnecessary overhead.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SkipDeviceTrackingAttribute : Attribute;
