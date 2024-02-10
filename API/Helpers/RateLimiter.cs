using System;
using System.Collections.Generic;

namespace API.Helpers;

public class RateLimiter(int maxRequests, TimeSpan duration, bool refillBetween = true)
{
    private readonly Dictionary<string, (int Tokens, DateTime LastRefill)> _tokenBuckets = new();
    private readonly object _lock = new();

    public bool TryAcquire(string key)
    {
        lock (_lock)
        {
            if (!_tokenBuckets.TryGetValue(key, out var bucket))
            {
                bucket = (Tokens: maxRequests, LastRefill: DateTime.UtcNow);
                _tokenBuckets[key] = bucket;
            }

            RefillTokens(key);

            lock (_lock)
            {

                if (_tokenBuckets[key].Tokens > 0)
                {
                    _tokenBuckets[key] = (Tokens: _tokenBuckets[key].Tokens - 1, LastRefill: _tokenBuckets[key].LastRefill);
                    return true;
                }
            }

            return false;
        }
    }

    private void RefillTokens(string key)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var timeSinceLastRefill = now - _tokenBuckets[key].LastRefill;
            var tokensToAdd = (int) (timeSinceLastRefill.TotalSeconds / duration.TotalSeconds);

            // Refill the bucket if the elapsed time is greater than or equal to the duration
            if (timeSinceLastRefill >= duration)
            {
                _tokenBuckets[key] = (Tokens: maxRequests, LastRefill: now);
                Console.WriteLine($"Tokens Refilled to Max: {maxRequests}");
            }
            else if (tokensToAdd > 0 && refillBetween)
            {
                _tokenBuckets[key] = (Tokens: Math.Min(maxRequests, _tokenBuckets[key].Tokens + tokensToAdd), LastRefill: now);
                Console.WriteLine($"Tokens Refilled: {_tokenBuckets[key].Tokens}");
            }
        }
    }
}

