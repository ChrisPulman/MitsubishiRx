// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Defines the XyAddressNotation values.</summary>
public enum XyAddressNotation
{
    /// <summary>Represents the Octal option.</summary>
    Octal,
    /// <summary>Represents the Hexadecimal option.</summary>
    Hexadecimal,
}
