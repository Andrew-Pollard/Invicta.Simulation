// © 2026 Andrew Pollard. All rights reserved.
// Licensed under the MIT License.

using System.Collections;

namespace Invicta.Simulation.Primitives;

/// <summary>
/// A collection whose enumerations each see the items as they were when the enumeration began, so that items can be
/// added and removed while it is being enumerated.
/// </summary>
/// <typeparam name="T">The type of the items in the collection.</typeparam>
/// <remarks>
/// A snapshot is taken only when the collection has changed since the last one, so repeated enumerations of an
/// unchanged collection allocate nothing beyond their enumerator.
/// </remarks>
internal sealed class SnapshotCollection<T> : IReadOnlyCollection<T>
{
    private readonly List<T> _items = [];

    private T[]? _snapshot;

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <summary>Adds an item to the end of the collection.</summary>
    /// <param name="item">The item to add.</param>
    public void Add(T item)
    {
        _items.Add(item);
        _snapshot = null;
    }

    /// <summary>Adds items to the end of the collection, in the order they are enumerated.</summary>
    /// <param name="items">The items to add.</param>
    public void AddRange(IEnumerable<T> items)
    {
        int count = _items.Count;

        _items.AddRange(items);
        if (_items.Count != count)
        {
            _snapshot = null;
        }
    }

    /// <summary>Removes every item that matches a condition.</summary>
    /// <param name="match">The condition an item must meet to be removed.</param>
    public void RemoveAll(Predicate<T> match)
    {
        if (_items.RemoveAll(match) > 0)
        {
            _snapshot = null;
        }
    }

    /// <summary>Returns the items as they are now, reusing the last snapshot until the collection changes.</summary>
    /// <returns>The items, in the order they were added.</returns>
    public T[] GetSnapshot()
    {
        _snapshot ??= [.. _items];

        return _snapshot;
    }

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)GetSnapshot()).GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
