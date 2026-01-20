using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aurum.Services;

public enum RequestPriority
{
    Background = 0, // Automated refreshes
    Normal = 10,    // Standard requests
    High = 20,      // User-initiated actions
    Critical = 30   // Must-have data (e.g. detailed view open)
}

public class QueuedRequest
{
    public string WorldName { get; init; }
    public List<uint> ItemIds { get; init; }
    public RequestPriority Priority { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public TaskCompletionSource<bool> CompletionSource { get; init; }

    public QueuedRequest(string worldName, IEnumerable<uint> itemIds, RequestPriority priority)
    {
        WorldName = worldName;
        ItemIds = itemIds.ToList();
        Priority = priority;
        Timestamp = DateTimeOffset.UtcNow;
        CompletionSource = new TaskCompletionSource<bool>();
    }
}

public class RequestQueue
{
    private readonly ConcurrentDictionary<string, QueuedRequest> _pendingRequests = new();
    private readonly object _queueLock = new();
    // Using a simple list for queue to allow sorting/priority extraction. 
    // For high volume, a PriorityQueue would be better, but we likely have < 100 pending.
    private readonly List<QueuedRequest> _queue = new(); 

    public int Count
    {
        get
        {
            lock (_queueLock)
            {
                return _queue.Count;
            }
        }
    }

    /// <summary>
    /// Enqueues a request. If a duplicate request exists (same world + same items), 
    /// it merges them or returns the existing task.
    /// </summary>
    public Task EnqueueRequestAsync(string worldName, uint itemId, RequestPriority priority)
    {
        return EnqueueRequestAsync(worldName, new[] { itemId }, priority);
    }

    public Task EnqueueRequestAsync(string worldName, IEnumerable<uint> itemIds, RequestPriority priority)
    {
        var sortedIds = itemIds.OrderBy(x => x).ToList();
        var key = $"{worldName}:{string.Join(",", sortedIds)}";

        lock (_queueLock)
        {
            // Dedup: Check if exact same request is pending
            if (_pendingRequests.TryGetValue(key, out var existing))
            {
                // Upgrade priority if new request is higher
                if (priority > existing.Priority)
                {
                     // Remove from queue, upgrade priority, re-insert
                     _queue.Remove(existing);
                     
                     // Create new request wrapper with same completion source but higher priority
                     // Note: We can't mutate records/init-only easily, so we replace the object in the list
                     // but keep the same CompletionSource so original waiter is notified.
                     var upgraded = new QueuedRequest(worldName, sortedIds, priority)
                     {
                         CompletionSource = existing.CompletionSource,
                         Timestamp = existing.Timestamp // Keep original timestamp to maintain queue position within new priority
                     };
                     
                     _pendingRequests[key] = upgraded;
                     _queue.Add(upgraded);
                     
                     // Re-sort
                     _queue.Sort((a, b) =>
                     {
                         int p = b.Priority.CompareTo(a.Priority);
                         if (p != 0) return p;
                         return a.Timestamp.CompareTo(b.Timestamp);
                     });
                     
                     return upgraded.CompletionSource.Task;
                }
                return existing.CompletionSource.Task;
            }

            // Dedup partial: Check if single item requests already cover this batch
            // For now, complex partial dedup is hard (e.g. batch 1,2,3 requested, but 1,2 is pending).
            // We only do exact match or exact subset match if trivial? 
            // Actually, we can check if any *subset* is pending, but waiting for a subset doesn't satisfy the superset.
            // But if we request {1}, and {1,2} is pending... we could hook into {1,2}?
            // Too complex for now. Exact match is good 80/20 rule.

            var request = new QueuedRequest(worldName, sortedIds, priority);
            _pendingRequests[key] = request;
            _queue.Add(request);
            
            // Sort by priority descending, then timestamp ascending
            _queue.Sort((a, b) =>
            {
                int p = b.Priority.CompareTo(a.Priority);
                if (p != 0) return p;
                return a.Timestamp.CompareTo(b.Timestamp);
            });
            
            return request.CompletionSource.Task;
        }
    }

    /// <summary>
    /// Dequeues the next highest priority request.
    /// </summary>
    public QueuedRequest? Dequeue()
    {
        lock (_queueLock)
        {
            if (_queue.Count == 0) return null;

            var item = _queue[0];
            _queue.RemoveAt(0);

            var key = $"{item.WorldName}:{string.Join(",", item.ItemIds)}";
            _pendingRequests.TryRemove(key, out _);

            return item;
        }
    }

    public void Clear()
    {
        lock (_queueLock)
        {
            foreach (var req in _queue)
            {
                req.CompletionSource.TrySetCanceled();
            }
            _queue.Clear();
            _pendingRequests.Clear();
        }
    }
}
