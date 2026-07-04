// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiTransportRequest record.</summary>
/// <param name="Payload">The Payload parameter.</param>
/// <param name="ExpectedResponseLength">The ExpectedResponseLength parameter.</param>
/// <param name="Description">The Description parameter.</param>
public sealed record MitsubishiTransportRequest(byte[] Payload, int? ExpectedResponseLength, string Description);
