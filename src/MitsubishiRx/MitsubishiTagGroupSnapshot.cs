// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTagGroupSnapshot record.</summary>
/// <param name="GroupName">The GroupName parameter.</param>
/// <param name="Values">The Values parameter.</param>
public sealed record MitsubishiTagGroupSnapshot(string GroupName, IReadOnlyDictionary<string, object?> Values)
{
    /// <summary>Gets or sets the TagNames property.</summary>
    public IReadOnlyList<string> TagNames => Values.Keys.ToArray();

    /// <summary>Executes the GetRequired operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="tagName">The tagName parameter.</param>
    /// <returns>The GetRequired operation result.</returns>
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

        throw new InvalidCastException($"Tag '{tagName}' in snapshot '{GroupName}' is of type '{value?.GetType().Name ?? "null"}', not '{nameof(T)}'.");
    }

    /// <summary>Executes the GetOptional operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="tagName">The tagName parameter.</param>
    /// <returns>The GetOptional operation result.</returns>
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
