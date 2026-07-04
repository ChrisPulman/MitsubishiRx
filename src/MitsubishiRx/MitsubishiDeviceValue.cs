// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Provides the MitsubishiDeviceValue record.</summary>
/// <param name="Address">The Address parameter.</param>
/// <param name="Value">The Value parameter.</param>
public sealed record MitsubishiDeviceValue(MitsubishiDeviceAddress Address, ushort Value);
