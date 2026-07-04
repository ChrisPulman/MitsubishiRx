// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiReactiveValue record.</summary>
/// <typeparam name="T">The T type parameter.</typeparam>
/// <param name="Value">The Value parameter.</param>
/// <param name="TimestampUtc">The TimestampUtc parameter.</param>
/// <param name="Quality">The Quality parameter.</param>
/// <param name="IsHeartbeat">The IsHeartbeat parameter.</param>
/// <param name="IsStale">The IsStale parameter.</param>
/// <param name="Source">The Source parameter.</param>
/// <param name="Error">The Error parameter.</param>
/// <param name="ErrorCode">The ErrorCode parameter.</param>
/// <param name="Exception">The Exception parameter.</param>
public sealed record MitsubishiReactiveValue<T>(T? Value, DateTimeOffset TimestampUtc, MitsubishiReactiveQuality Quality, bool IsHeartbeat = false, bool IsStale = false, string Source = "", string Error = "", int ErrorCode = 0, Exception? Exception = null);
