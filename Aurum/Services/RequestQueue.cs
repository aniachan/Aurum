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
                    // This is tricky with a list, we'd need to re-sort or update the object.
                    // Since the object is ref, updating a property is visible, but we need to re-sort the queue on dequeue.
                    // However, our QueuedRequest is immutable-ish. Let's not mutate Priority for now to keep it simple,
                    // or just accept the existing priority.
                    // For now: First come wins, but maybe we should allow priority upgrade?
                    // Let's just return the existing task.
                }
                return existing.CompletionSource.Task;
            }

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
