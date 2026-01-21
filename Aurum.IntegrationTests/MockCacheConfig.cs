using System;
using Aurum.Services;

namespace Aurum.IntegrationTests;

public class MockCacheConfig : ICacheConfig
{
    public int MarketDataCacheDurationSeconds { get; set; } = 300;
    public int MaxCacheEntries { get; set; } = 1000;
}
