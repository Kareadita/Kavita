using System;
using System.Collections.Generic;
using System.Threading;

namespace Kavita.Common.Helpers;

/// <summary>
/// Custom X per Y rate limiter
/// </summary>
/// <param name="maxRequests"></param>
/// <param name="duration"></param>
/// <param name="refillBetween"></param>
public class RateLimiter(int maxRequests, TimeSpan duration, bool refillBetween = true)
{
    private readonly Dictionary<string, (int Tokens, DateTime LastRefill)> _tokenBuckets = new();
    private readonly Lock _lock = new();

    public bool TryAcquire(string key)
    {
        lock (_lock)
        {
            if (!_tokenBuckets.TryGetValue(key, out var value))
            {
                value = (Tokens: maxRequests, LastRefill: DateTime.UtcNow);
                _tokenBuckets[key] = value;
            }

            RefillTokens(key);

            // Re-read the bucket, as RefillTokens may have updated the token count
            value = _tokenBuckets[key];

            if (value.Tokens <= 0) return false;

            _tokenBuckets[key] = (Tokens: value.Tokens - 1, LastRefill: value.LastRefill);
            return true;
        }
    }

    /// <remarks>Callers must hold <see cref="_lock"/>.</remarks>
    private void RefillTokens(string key)
    {
        var now = DateTime.UtcNow;
        var timeSinceLastRefill = now - _tokenBuckets[key].LastRefill;
        var tokensToAdd = (int) (timeSinceLastRefill.TotalSeconds / duration.TotalSeconds);

        // Refill the bucket if the elapsed time is greater than or equal to the duration
        if (timeSinceLastRefill >= duration)
        {
            _tokenBuckets[key] = (Tokens: maxRequests, LastRefill: now);
        }
        else if (tokensToAdd > 0 && refillBetween)
        {
            _tokenBuckets[key] = (Tokens: Math.Min(maxRequests, _tokenBuckets[key].Tokens + tokensToAdd), LastRefill: now);
        }
    }
}

