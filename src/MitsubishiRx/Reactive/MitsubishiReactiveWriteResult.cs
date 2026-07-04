// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiReactiveWriteResult record.</summary>
/// <param name="Target">The Target parameter.</param>
/// <param name="TimestampUtc">The TimestampUtc parameter.</param>
/// <param name="Mode">The Mode parameter.</param>
/// <param name="Success">The Success parameter.</param>
/// <param name="Error">The Error parameter.</param>
/// <param name="ErrorCode">The ErrorCode parameter.</param>
/// <param name="Exception">The Exception parameter.</param>
public sealed record MitsubishiReactiveWriteResult(string Target, DateTimeOffset TimestampUtc, MitsubishiReactiveWriteMode Mode, bool Success, string Error = "", int ErrorCode = 0, Exception? Exception = null);
