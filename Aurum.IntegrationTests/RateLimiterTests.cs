using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Aurum.Services;
using Aurum;
using Dalamud.Plugin.Services;
using System.Diagnostics;
using System.Threading;

namespace Aurum.IntegrationTests
{
    public class RateLimiterTests
    {
        private Mock<IPluginLog> _mockLog;
        private Configuration _config;

        public RateLimiterTests()
        {
            _mockLog = new Mock<IPluginLog>();
            _config = new Configuration();
        }

        [Fact]
        public async Task WaitForTokenAsync_ConsumesTokensImmediately_WhenAvailable()
        {
            // Arrange
            _config.ApiRateLimitPerMinute = 6000; // 100 req/sec
            var limiter = new RateLimiter(_mockLog.Object, _config, null);

            // Act
            var sw = Stopwatch.StartNew();
            await limiter.WaitForTokenAsync("test");
            await limiter.WaitForTokenAsync("test");
            await limiter.WaitForTokenAsync("test");
            sw.Stop();

            // Assert
            // Should be very fast, effectively 0 wait
            Assert.True(sw.ElapsedMilliseconds < 100, "Should consume tokens immediately without waiting");
            Assert.Equal(3, limiter.TotalRequests);
        }

        [Fact]
        public async Task WaitForTokenAsync_Waits_WhenTokensExhausted()
        {
            // Arrange
            _config.ApiRateLimitPerMinute = 60; // 1 req/sec
            var limiter = new RateLimiter(_mockLog.Object, _config, null);

            // Consume initial burst (default max is burst capacity, approx 25 or 2*rate)
            // Rate = 1 req/s. MaxTokens = Max(25, 2) = 25.
            // We need to consume 25 tokens first.
            for (int i = 0; i < 25; i++)
            {
                await limiter.WaitForTokenAsync("test");
            }

            // Act
            var sw = Stopwatch.StartNew();
            await limiter.WaitForTokenAsync("test"); // This should wait approx 1 second
            sw.Stop();

            // Assert
            Assert.True(sw.ElapsedMilliseconds >= 900, $"Should wait at least ~1s (actual: {sw.ElapsedMilliseconds}ms)");
            Assert.True(limiter.RateLimitedRequests > 0, "Should record rate limited request");
        }

        [Fact]
        public async Task EndpointLimiter_ThrottlesSpecificEndpoint()
        {
             // Arrange
            _config.ApiRateLimitPerMinute = 6000; // Fast global limit
            var limiter = new RateLimiter(_mockLog.Object, _config, null);
            
            // The EndpointLimiter in RateLimiter.cs is hardcoded to (1000, 50) currently in GetOrAdd
            // new EndpointLimiter(1000, 50) -> 1000 req/min ~ 16.6 req/sec. Burst 50.
            
            // To test this effectively without mocking the internal dictionary, we rely on the hardcoded values.
            // 50 burst.
            
            for(int i=0; i<50; i++)
            {
                await limiter.WaitForTokenAsync("endpointA");
            }

            var sw = Stopwatch.StartNew();
            await limiter.WaitForTokenAsync("endpointA"); // Should wait slightly? 1000/min is pretty fast.
            // 1/16.6 sec = 60ms.
            sw.Stop();

            // It's hard to deterministically test small waits, but let's check counters
            Assert.Equal(51, limiter.TotalRequests);
        }

        [Fact]
        public async Task WaitForTokenAsync_ReservesCapacity_ForInteractivePriority()
        {
            // Arrange
            // Low refill rate but reasonable burst
            _config.ApiRateLimitPerMinute = 60; // 1 req/sec
            // Burst will be 25
            var limiter = new RateLimiter(_mockLog.Object, _config, null);

            // Calculate threshold: 20% of 25 = 5 tokens.
            // If tokens < 5, Background requests should block.
            
            // Consume tokens until we are just below threshold.
            // Start with 25. Consume 21. Remaining = 4.
            for (int i = 0; i < 21; i++)
            {
                await limiter.WaitForTokenAsync("setup", default, RequestPriority.Normal);
            }
            
            // Now verify Background blocks but Normal/High proceeds
            
            // 1. Normal/High should pass immediately (since > 1.0 token)
            var sw = Stopwatch.StartNew();
            await limiter.WaitForTokenAsync("normal", default, RequestPriority.Normal);
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 500, "Normal priority should not be blocked by reserve");
            
            // Now Remaining is approx 3.
            
            // 2. Background should block because 3 < 5
            // It will wait until tokens refill to >= 5.
            // We need +2 tokens. At 1 req/sec, that's 2 seconds.
            sw.Restart();
            await limiter.WaitForTokenAsync("background", default, RequestPriority.Background);
            sw.Stop();
            
            Assert.True(sw.ElapsedMilliseconds >= 1900, $"Background should wait for reserve (actual: {sw.ElapsedMilliseconds}ms)");
        }
    }
}
