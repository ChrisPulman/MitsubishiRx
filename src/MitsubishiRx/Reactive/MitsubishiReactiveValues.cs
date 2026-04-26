// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MitsubishiRx;

/// <summary>
/// Quality classification for hot reactive PLC value streams.
/// </summary>
public enum MitsubishiReactiveQuality
{
    /// <summary>
    /// The value is fresh and valid.
    /// </summary>
    Good,

    /// <summary>
    /// The value is known to be bad or invalid.
    /// </summary>
    Bad,

    /// <summary>
    /// The stream is stale.
    /// </summary>
    Stale,

    /// <summary>
    /// The item is a heartbeat emission.
    /// </summary>
    Heartbeat,

    /// <summary>
    /// The item represents a failed read or projection.
    /// </summary>
    Error,
}

/// <summary>
/// Immutable value envelope for reactive Mitsubishi streams.
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
/// <param name="Value">Projected value.</param>
/// <param name="TimestampUtc">Emission timestamp.</param>
/// <param name="Quality">Quality classification.</param>
/// <param name="IsHeartbeat">Whether the item represents a heartbeat.</param>
/// <param name="IsStale">Whether the item represents staleness.</param>
/// <param name="Source">Human-readable source.</param>
/// <param name="Error">Error message when present.</param>
/// <param name="ErrorCode">Associated error code when present.</param>
/// <param name="Exception">Associated exception when present.</param>
public sealed record MitsubishiReactiveValue<T>(
    T? Value,
    DateTimeOffset TimestampUtc,
    MitsubishiReactiveQuality Quality,
    bool IsHeartbeat = false,
    bool IsStale = false,
    string Source = "",
    string Error = "",
    int ErrorCode = 0,
    Exception? Exception = null);

/// <summary>
/// Factory helpers for <see cref="MitsubishiReactiveValue{T}"/>.
/// </summary>
public static class MitsubishiReactiveValue
{
    /// <summary>
    /// Wraps a regular response in a reactive value envelope.
    /// </summary>
    /// <typeparam name="T">Payload type.</typeparam>
    /// <param name="response">Response to wrap.</param>
    /// <param name="timestampUtc">Emission time.</param>
    /// <param name="source">Human-readable source.</param>
    /// <returns>Reactive value envelope.</returns>
    public static MitsubishiReactiveValue<T> FromResponse<T>(Responce<T> response, DateTimeOffset timestampUtc, string source)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.IsSucceed
            ? new MitsubishiReactiveValue<T>(response.Value, timestampUtc, MitsubishiReactiveQuality.Good, Source: source ?? string.Empty)
            : new MitsubishiReactiveValue<T>(default, timestampUtc, MitsubishiReactiveQuality.Error, Source: source ?? string.Empty, Error: response.Err, ErrorCode: response.ErrCode, Exception: response.Exception);
    }

    /// <summary>
    /// Creates a heartbeat item based on the last known value.
    /// </summary>
    /// <typeparam name="T">Payload type.</typeparam>
    /// <param name="value">Baseline value.</param>
    /// <param name="timestampUtc">Heartbeat time.</param>
    /// <returns>Heartbeat envelope.</returns>
    public static MitsubishiReactiveValue<T> Heartbeat<T>(MitsubishiReactiveValue<T> value, DateTimeOffset timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value with
        {
            TimestampUtc = timestampUtc,
            Quality = MitsubishiReactiveQuality.Heartbeat,
            IsHeartbeat = true,
            IsStale = false,
        };
    }

    /// <summary>
    /// Creates a stale item based on the last known value.
    /// </summary>
    /// <typeparam name="T">Payload type.</typeparam>
    /// <param name="value">Baseline value.</param>
    /// <param name="timestampUtc">Stale time.</param>
    /// <returns>Stale envelope.</returns>
    public static MitsubishiReactiveValue<T> Stale<T>(MitsubishiReactiveValue<T> value, DateTimeOffset timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value with
        {
            TimestampUtc = timestampUtc,
            Quality = MitsubishiReactiveQuality.Stale,
            IsHeartbeat = false,
            IsStale = true,
        };
    }
}