// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx;

/// <summary>
/// Named collection of tags used for grouped reads, validation, and scan-class style workflows.
/// </summary>
/// <param name="Name">Unique group name.</param>
/// <param name="TagNames">Ordered tag names belonging to the group.</param>
public sealed record MitsubishiTagGroupDefinition(string Name, IReadOnlyList<string> TagNames)
{
    /// <summary>
    /// Gets the resolved ordered tag names.
    /// </summary>
    public IReadOnlyList<string> ResolvedTagNames => TagNames ?? Array.Empty<string>();
}

/// <summary>
/// Snapshot of heterogeneous tag values returned from a grouped read.
/// </summary>
/// <param name="GroupName">Group name.</param>
/// <param name="Values">Tag values keyed by tag name.</param>
public sealed record MitsubishiTagGroupSnapshot(string GroupName, IReadOnlyDictionary<string, object?> Values)
{
    /// <summary>
    /// Gets the tag names present in this snapshot.
    /// </summary>
    public IReadOnlyList<string> TagNames => Values.Keys.ToArray();

    /// <summary>
    /// Gets a required typed value by tag name.
    /// </summary>
    /// <typeparam name="T">Expected value type.</typeparam>
    /// <param name="tagName">Tag name.</param>
    /// <returns>Typed value.</returns>
    public T GetRequired<T>(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        if (!Values.TryGetValue(tagName, out var value))
        {
            throw new KeyNotFoundException($"Tag '{tagName}' was not present in snapshot '{GroupName}'.");
        }

        if (value is T typed)
        {
            return typed;
        }

        throw new InvalidCastException($"Tag '{tagName}' in snapshot '{GroupName}' is of type '{value?.GetType().Name ?? "null"}', not '{typeof(T).Name}'.");
    }

    /// <summary>
    /// Gets an optional typed value by tag name, returning <c>default</c> when the tag is missing or cannot be cast.
    /// </summary>
    /// <typeparam name="T">Expected value type.</typeparam>
    /// <param name="tagName">Tag name.</param>
    /// <returns>Typed value or <c>default</c>.</returns>
    public T? GetOptional<T>(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
        if (!Values.TryGetValue(tagName, out var value))
        {
            return default;
        }

        return value is T typed ? typed : default;
    }
}
