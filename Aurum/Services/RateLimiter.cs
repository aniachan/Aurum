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
    private readonly DatabaseService database; // Add dependency for persistent tracking
    
    // Token Bucket State
    private readonly object _lock = new();
    private double _tokens;
    private readonly double _maxTokens;
    private readonly double _refillRatePerSecond; // Tokens per second
    private DateTime _lastRefillTime;

    // Endpoint Specific Limits
    private readonly ConcurrentDictionary<string, EndpointLimiter> _endpointLimiters = new();

    // Stats
    public long TotalRequests { get; private set; }
    public long RateLimitedRequests { get; private set; }
    
    // Usage Monitoring
    private readonly ConcurrentQueue<DateTime> _minuteHistory = new();
    private readonly ConcurrentQueue<DateTime> _hourHistory = new();
    private long _dailyCount;
    private DateTime _lastDailyReset = DateTime.UtcNow.Date;

    public long TotalErrors { get; private set; }
    public long TotalRetries { get; private set; }

    public int RequestsLastMinute 
    { 
        get 
        {
            PruneQueue(_minuteHistory, TimeSpan.FromMinutes(1));
            return _minuteHistory.Count;
        } 
    }

    public int RequestsLastHour 
    { 
        get 
        {
            PruneQueue(_hourHistory, TimeSpan.FromHours(1));
            return _hourHistory.Count;
        } 
    }

    public long RequestsToday
    {
        get
        {
            CheckDailyReset();
            return _dailyCount;
        }
    }

    public RateLimiter(IPluginLog log, Configuration config, DatabaseService database)
    {
        this.log = log;
        this.configuration = config;
        this.database = database;
        
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
    public async Task WaitForTokenAsync(string? endpoint = null, CancellationToken cancellationToken = default)
    {
        // Simple loop with delay
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool globalTokenAcquired = false;
            
            lock (_lock)
            {
                RefillTokens();

                if (_tokens >= 1.0)
                {
                    // Check endpoint limit if applicable
                    if (endpoint != null)
                    {
                        var limit = _endpointLimiters.GetOrAdd(endpoint, ep => 
                        {
                            // Default to high limit if not specified (essentially no limit other than global)
                            // Could implement specific limits per endpoint here
                            return new EndpointLimiter(1000, 50); 
                        });

                        if (limit.TryConsume())
                        {
                            _tokens -= 1.0;
                            globalTokenAcquired = true;
                        }
                    }
                    else
                    {
                        _tokens -= 1.0;
                        globalTokenAcquired = true;
                    }

                    if (globalTokenAcquired)
                    {
                        TotalRequests++;
                        TrackRequest(endpoint ?? "unknown"); // Pass endpoint
                        return; // Token acquired
                    }
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
    
    public void RecordError()
    {
        TotalErrors++;
    }

    public void RecordRetry()
    {
        TotalRetries++;
    }

    private void TrackRequest(string endpoint)
    {
        var now = DateTime.UtcNow;
        _minuteHistory.Enqueue(now);
        _hourHistory.Enqueue(now);
        
        CheckDailyReset();
        Interlocked.Increment(ref _dailyCount);

        // Persistent logging
        if (database != null)
        {
            // We fire and forget this task to avoid blocking the request thread
            // or just rely on the database's internal locking/pooling which is fast enough
            // For now, let's just do it synchronously inside this method as it's already async-context friendly mostly?
            // Actually TrackRequest is called from WaitForTokenAsync which is async, but this method is void.
            // Let's spawn a Task for DB logging to keep rate limiter snappy.
            Task.Run(() => 
            {
               try 
               {
                   database.LogApiRequest(endpoint, now, 0, 0, true);
               }
               catch(Exception ex)
               {
                   // Swallow DB log errors to not crash app, maybe log to file
               }
            });
        }
        
        // Lazy prune
        if (TotalRequests % 10 == 0)
        {
            PruneQueue(_minuteHistory, TimeSpan.FromMinutes(1));
            PruneQueue(_hourHistory, TimeSpan.FromHours(1));
        }
    }

    private void CheckDailyReset()
    {
        var today = DateTime.UtcNow.Date;
        if (today > _lastDailyReset)
        {
            lock (_lock)
            {
                if (today > _lastDailyReset)
                {
                    _lastDailyReset = today;
                    Interlocked.Exchange(ref _dailyCount, 0);
                }
            }
        }
    }

    private void PruneQueue(ConcurrentQueue<DateTime> queue, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        while (queue.TryPeek(out var time) && time < cutoff)
        {
            queue.TryDequeue(out _);
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

    private class EndpointLimiter
    {
        private readonly object _lock = new();
        private double _tokens;
        private readonly double _maxTokens;
        private readonly double _refillRatePerSecond;
        private DateTime _lastRefillTime;

        public EndpointLimiter(double requestsPerMinute, double burst)
        {
            _refillRatePerSecond = requestsPerMinute / 60.0;
            _maxTokens = burst;
            _tokens = _maxTokens;
            _lastRefillTime = DateTime.UtcNow;
        }

        public bool TryConsume()
        {
            lock (_lock)
            {
                RefillTokens();
                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return true;
                }
                return false;
            }
        }

        public double GetWaitTimeSeconds()
        {
            // Simple estimation
            return 1.0 / _refillRatePerSecond;
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
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
