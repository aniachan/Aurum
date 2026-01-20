using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace Aurum.Services;

public class RateLimiter : IDisposable
{
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    
    // Token Bucket State
    private readonly object _lock = new();
    private double _tokens;
    private readonly double _maxTokens;
    private readonly double _refillRatePerSecond; // Tokens per second
    private DateTime _lastRefillTime;

    // Stats
    public long TotalRequests { get; private set; }
    public long RateLimitedRequests { get; private set; }

    public RateLimiter(IPluginLog log, Configuration config)
    {
        this.log = log;
        this.configuration = config;
        
        // Calculate rate from config or default
        // Config stores requests per MINUTE (e.g., 900)
        // Rate is per SECOND (e.g., 15)
        
        double requestsPerMinute = config.ApiRateLimitPerMinute > 0 ? config.ApiRateLimitPerMinute : 900;
        _refillRatePerSecond = requestsPerMinute / 60.0;
        
        // Burst capacity - usually a few seconds worth of requests
        _maxTokens = Math.Max(25.0, _refillRatePerSecond * 2); 
        
        _tokens = _maxTokens;
        _lastRefillTime = DateTime.UtcNow;
        
        log.Info($"RateLimiter initialized. Rate: {_refillRatePerSecond:F2} req/s, Burst: {_maxTokens:F2}, Config: {requestsPerMinute} req/min");
    }

    /// <summary>
    /// Waits until a token is available to make a request.
    /// Thread-safe.
    /// </summary>
    public async Task WaitForTokenAsync(CancellationToken cancellationToken = default)
    {
        // Simple loop with delay
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_lock)
            {
                RefillTokens();

                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    TotalRequests++;
                    return; // Token acquired
                }
            }

            // Not enough tokens, wait a bit
            // Calculate time to wait for next token
            // If empty, we need 1 token. Refill rate is X/sec. 1/X seconds.
            // Add a small buffer/jitter?
            
            RateLimitedRequests++;
            var waitTimeMs = (int)((1.0 / _refillRatePerSecond) * 1000);
            if (waitTimeMs < 10) waitTimeMs = 10; // Minimum wait
            
            // log.Debug($"Rate limited. Waiting {waitTimeMs}ms...");
            await Task.Delay(waitTimeMs, cancellationToken);
        }
    }

    private void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = (now - _lastRefillTime).TotalSeconds;

        if (elapsedSeconds > 0)
        {
            var newTokens = elapsedSeconds * _refillRatePerSecond;
            
            if (newTokens > 0)
            {
                _tokens = Math.Min(_maxTokens, _tokens + newTokens);
                _lastRefillTime = now;
            }
        }
    }

    public void UpdateConfiguration()
    {
        // TODO: Allow updating rate from config on the fly
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
