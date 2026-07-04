// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiOperationLog record.</summary>
/// <param name="TimestampUtc">The TimestampUtc parameter.</param>
/// <param name="State">The State parameter.</param>
/// <param name="Description">The Description parameter.</param>
/// <param name="Success">The Success parameter.</param>
/// <param name="RequestPayload">The RequestPayload parameter.</param>
/// <param name="ResponsePayload">The ResponsePayload parameter.</param>
/// <param name="Exception">The Exception parameter.</param>
public sealed record MitsubishiOperationLog(DateTimeOffset TimestampUtc, MitsubishiConnectionState State, string Description, bool Success, ReadOnlyMemory<byte> RequestPayload, ReadOnlyMemory<byte> ResponsePayload, Exception? Exception = null);
