using System;

namespace Kavita.API.Attributes;

/// <summary>
/// Attribute to skip device tracking on specific endpoints.
/// Use for high-frequency endpoints where device tracking adds unnecessary overhead.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SkipDeviceTrackingAttribute : Attribute;
