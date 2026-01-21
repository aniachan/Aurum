namespace Aurum.Services;

public interface ICacheConfig
{
    int MarketDataCacheDurationSeconds { get; }
    int MaxCacheEntries { get; }
}
