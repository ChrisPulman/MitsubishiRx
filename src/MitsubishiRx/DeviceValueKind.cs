// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Defines the DeviceValueKind values.</summary>
public enum DeviceValueKind
{
    /// <summary>Represents the Bit option.</summary>
    Bit,
    /// <summary>Represents the Word option.</summary>
    Word,
}
