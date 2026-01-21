using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aurum.Services;
using Xunit;

namespace Aurum.IntegrationTests
{
    public class RequestQueueDedupTests
    {
        [Fact]
        public async Task EnqueueRequest_DedupsSubset_WhenPendingIsSuperset()
        {
            var queue = new RequestQueue();
            
            // Enqueue {1, 2}
            var t1 = queue.EnqueueRequestAsync("TestWorld", new uint[] { 1, 2 }, RequestPriority.Normal);
            
            // Enqueue {1} - should piggyback on t1
            var t2 = queue.EnqueueRequestAsync("TestWorld", 1, RequestPriority.Normal);

            Assert.Equal(1, queue.Count);
            // Note: Tasks might not be same instance if it returns a new task wrapping the old one, 
            // but in current impl it returns pending.CompletionSource.Task
            Assert.Same(t1, t2);

            var item = queue.Dequeue();
            Assert.NotNull(item);
            Assert.Equal(2, item.ItemIds.Count); // Should be the {1, 2} request
            
            // Completion
            item.CompletionSource.TrySetResult(true);
            
            Assert.True(t1.IsCompletedSuccessfully);
            Assert.True(t2.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task EnqueueRequest_UpgradesSupersetPriority_WhenSubsetIsHigherPriority()
        {
            var queue = new RequestQueue();
            
            // Enqueue {1, 2} at Normal
            var t1 = queue.EnqueueRequestAsync("TestWorld", new uint[] { 1, 2 }, RequestPriority.Normal);
            
            // Enqueue {1} at High
            var t2 = queue.EnqueueRequestAsync("TestWorld", 1, RequestPriority.High);

            Assert.Equal(1, queue.Count);
            
            var item = queue.Dequeue();
            Assert.NotNull(item);
            Assert.Equal(RequestPriority.High, item.Priority); // Should be upgraded
            Assert.Equal(2, item.ItemIds.Count); // Should still be {1, 2}
            
            item.CompletionSource.TrySetResult(true);
            await Task.WhenAll(t1, t2);
        }

        [Fact]
        public async Task DequeueBatch_MergesSeparateRequests_AndCompletesAll()
        {
            var queue = new RequestQueue();
            
            // Enqueue {1}
            var t1 = queue.EnqueueRequestAsync("TestWorld", 1, RequestPriority.Normal);
            // Enqueue {1, 2} (Superset of t1, but current impl only dedups if Pending is Superset of New)
            // So this should add a second request
            var t2 = queue.EnqueueRequestAsync("TestWorld", new uint[] { 1, 2 }, RequestPriority.Normal);
            
            Assert.Equal(2, queue.Count);

            // DequeueBatch should merge them
            var batch = queue.DequeueBatch(100);
            Assert.NotNull(batch);
            Assert.Equal(0, queue.Count); // Both removed
            
            // Batch should contain {1, 2} (union)
            Assert.Equal(2, batch.ItemIds.Count);
            Assert.Contains(1u, batch.ItemIds);
            Assert.Contains(2u, batch.ItemIds);

            // Complete the batch
            batch.CompletionSource.TrySetResult(true);
            
            // Wait a bit for propagation
            await Task.Delay(10);

            Assert.True(t1.IsCompletedSuccessfully);
            Assert.True(t2.IsCompletedSuccessfully);
        }
    }
}
