// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiReactiveValue type.</summary>
public static class MitsubishiReactiveValue
{
    /// <summary>Executes the FromResponse operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="response">The response parameter.</param>
    /// <param name="timestampUtc">The timestampUtc parameter.</param>
    /// <param name="source">The source parameter.</param>
    /// <returns>The FromResponse operation result.</returns>
    public static MitsubishiReactiveValue<T> FromResponse<T>(Responce<T> response, DateTimeOffset timestampUtc, string source)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.IsSucceed ? new MitsubishiReactiveValue<T>(response.Value, timestampUtc, MitsubishiReactiveQuality.Good, Source: source ?? string.Empty) : new MitsubishiReactiveValue<T>(default, timestampUtc, MitsubishiReactiveQuality.Error, Source: source ?? string.Empty, Error: response.Err, ErrorCode: response.ErrCode, Exception: response.Exception);
    }

    /// <summary>Executes the Heartbeat operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="value">The value parameter.</param>
    /// <param name="timestampUtc">The timestampUtc parameter.</param>
    /// <returns>The Heartbeat operation result.</returns>
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

    /// <summary>Executes the Stale operation.</summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="value">The value parameter.</param>
    /// <param name="timestampUtc">The timestampUtc parameter.</param>
    /// <returns>The Stale operation result.</returns>
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
