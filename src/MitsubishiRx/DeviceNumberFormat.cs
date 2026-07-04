// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace MitsubishiRx.Reactive;
#else

namespace MitsubishiRx;
#endif

/// <summary>Defines the DeviceNumberFormat values.</summary>
public enum DeviceNumberFormat
{
    /// <summary>Represents the Decimal option.</summary>
    Decimal,
    /// <summary>Represents the Hexadecimal option.</summary>
    Hexadecimal,
    /// <summary>Represents the Octal option.</summary>
    Octal,
    /// <summary>Represents the XyVariable option.</summary>
    XyVariable,
}
