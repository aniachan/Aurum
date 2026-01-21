using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aurum.Services;
using Xunit;

namespace Aurum.IntegrationTests
{
    public class RequestQueueCoalescingTests
    {
        [Fact]
        public void DequeueBatchForWorld_ReturnsSpecificWorldRequest()
        {
            var queue = new RequestQueue();
            
            queue.EnqueueRequestAsync("OtherWorld", 1, RequestPriority.Normal);
            queue.EnqueueRequestAsync("TargetWorld", 2, RequestPriority.Normal);
            
            var batch = queue.DequeueBatchForWorld("TargetWorld", 100);
            
            Assert.NotNull(batch);
            Assert.Equal("TargetWorld", batch.WorldName);
            Assert.Single(batch.ItemIds);
            Assert.Contains(2u, batch.ItemIds);
            
            // Other world should remain
            Assert.Equal(1, queue.Count);
        }

        [Fact]
        public void DequeueBatchForWorld_CoalescesMultipleRequests()
        {
            var queue = new RequestQueue();
            
            queue.EnqueueRequestAsync("TargetWorld", 1, RequestPriority.Normal);
            queue.EnqueueRequestAsync("TargetWorld", 2, RequestPriority.Normal);
            
            var batch = queue.DequeueBatchForWorld("TargetWorld", 100);
            
            Assert.NotNull(batch);
            Assert.Equal(2, batch.ItemIds.Count);
            Assert.Contains(1u, batch.ItemIds);
            Assert.Contains(2u, batch.ItemIds);
            
            Assert.Equal(0, queue.Count);
        }

        [Fact]
        public void DequeueBatchForWorld_RespectsMaxItems()
        {
            var queue = new RequestQueue();
            
            queue.EnqueueRequestAsync("TargetWorld", new uint[] { 1, 2, 3 }, RequestPriority.Normal);
            queue.EnqueueRequestAsync("TargetWorld", new uint[] { 4, 5 }, RequestPriority.Normal);
            
            // Limit to 4 items
            var batch = queue.DequeueBatchForWorld("TargetWorld", 4);
            
            Assert.NotNull(batch);
            Assert.Equal(3, batch.ItemIds.Count); // Should only take the first request (3 items), as adding second (2 items) = 5 > 4
            
            Assert.Equal(1, queue.Count); // Second request remains
        }
    }
}
