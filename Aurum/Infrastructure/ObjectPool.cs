using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Aurum.Infrastructure;

/// <summary>
/// A generic thread-safe object pool.
/// </summary>
/// <typeparam name="T">The type of object to pool.</typeparam>
public class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _objects;
    private readonly Func<T> _objectGenerator;

    public ObjectPool(Func<T>? objectGenerator = null)
    {
        _objectGenerator = objectGenerator ?? (() => new T());
        _objects = new ConcurrentBag<T>();
    }

    /// <summary>
    /// Gets an item from the pool.
    /// </summary>
    /// <returns>An instance of T.</returns>
    public T Get()
    {
        if (_objects.TryTake(out T? item)) return item;
        return _objectGenerator();
    }

    /// <summary>
    /// Returns an item to the pool.
    /// </summary>
    /// <param name="item">The item to return.</param>
    public void Return(T item)
    {
        _objects.Add(item);
    }
    
    /// <summary>
    /// Clears the pool.
    /// </summary>
    public void Clear()
    {
        _objects.Clear();
    }
}
