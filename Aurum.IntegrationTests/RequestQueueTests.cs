using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aurum.Services;
using Xunit;

namespace Aurum.IntegrationTests
{
    public class RequestQueueTests
    {
        [Fact]
        public async Task EnqueueRequest_SortsByPriority()
        {
            var queue = new RequestQueue();

            _ = queue.EnqueueRequestAsync("TestWorld", 1, RequestPriority.Background);
            _ = queue.EnqueueRequestAsync("TestWorld", 2, RequestPriority.High);
            _ = queue.EnqueueRequestAsync("TestWorld", 3, RequestPriority.Normal);

            var item1 = queue.Dequeue();
            var item2 = queue.Dequeue();
            var item3 = queue.Dequeue();

            Assert.NotNull(item1);
            Assert.NotNull(item2);
            Assert.NotNull(item3);

            Assert.Equal(RequestPriority.High, item1.Priority);
            Assert.Equal(RequestPriority.Normal, item2.Priority);
            Assert.Equal(RequestPriority.Background, item3.Priority);
            
            // Complete the tasks to avoid hanging
            item1.CompletionSource.TrySetResult(true);
            item2.CompletionSource.TrySetResult(true);
            item3.CompletionSource.TrySetResult(true);
            
            await Task.CompletedTask;
        }

        [Fact]
        public async Task EnqueueRequest_UpgradesPriority_ForDuplicate()
        {
            var queue = new RequestQueue();
            var t1 = queue.EnqueueRequestAsync("TestWorld", 1, RequestPriority.Background);
            var t2 = queue.EnqueueRequestAsync("TestWorld", 1, RequestPriority.High);

            // Should be the same task instance
            Assert.Same(t1, t2);

            var item = queue.Dequeue();
            Assert.NotNull(item);
            Assert.Equal(RequestPriority.High, item.Priority);
            Assert.Equal(0, queue.Count);
            
            // Complete the task to avoid hanging
            item.CompletionSource.TrySetResult(true);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task DequeueBatch_MergesRequests_RespectsMaxItems()
        {
             var queue = new RequestQueue();
             // Add 150 items separately for same world
             for(uint i=0; i<150; i++)
             {
                 _ = queue.EnqueueRequestAsync("TestWorld", i, RequestPriority.Normal);
             }

             var batch1 = queue.DequeueBatch(100);
             Assert.NotNull(batch1);
             Assert.Equal(100, batch1.ItemIds.Count);
             Assert.Equal(50, queue.Count);

             var batch2 = queue.DequeueBatch(100);
             Assert.NotNull(batch2);
             Assert.Equal(50, batch2.ItemIds.Count);
             Assert.Equal(0, queue.Count);
             
             // Complete the tasks to avoid hanging
             batch1.CompletionSource.TrySetResult(true);
             batch2.CompletionSource.TrySetResult(true);
             await Task.CompletedTask;
        }
        
         [Fact]
        public async Task DequeueBatch_PrioritizesHighPriority()
        {
             var queue = new RequestQueue();
             
             _ = queue.EnqueueRequestAsync("TestWorld", 1, RequestPriority.Background);
             _ = queue.EnqueueRequestAsync("TestWorld", 2, RequestPriority.High); // Should be picked first
             _ = queue.EnqueueRequestAsync("TestWorld", 3, RequestPriority.Normal);

             var batch = queue.DequeueBatch(100);
             Assert.NotNull(batch);
             // Should include all because they fit in batch, but "primary" driven by High
             Assert.Contains(2u, batch.ItemIds); 
             Assert.Contains(1u, batch.ItemIds); 
             Assert.Contains(3u, batch.ItemIds);
             
             // Verify queue empty
             Assert.Equal(0, queue.Count);
             
             // Complete the task to avoid hanging
             batch.CompletionSource.TrySetResult(true);
             await Task.CompletedTask;
        }
    }
}
